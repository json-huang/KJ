using FluentAssertions;
using KJ.Core;
using KJ.Diagnostics;
using KJ.Domain;
using KJ.Domain.Services;
using KJ.Drivers;
using KJ.Drivers.Abstractions;
using KJ.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KJ.IntegrationTests;

/// <summary>
/// 集成测试：验证各组件协同工作的完整链路。
/// </summary>
public class IntegrationTests
{
    // ── 辅助 ────────────────────────────────────────────────────────────

    private static InMemoryTagStore CreateTagStore() => new();
    private static AlarmService CreateAlarmService() => new();
    private static DeviceManager CreateDeviceManager() => new();
    private static DiagnosticHub CreateDiagnostics() => new();

    private static DeviceDriverFactory CreateDriverFactory()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var diag = CreateDiagnostics();
        services.AddSingleton(diag);
        services.AddSingleton<TcpDeviceDriver>();
        services.AddSingleton<ModbusTcpDriver>();
        services.AddSingleton<ModbusRtuDriver>();
        services.AddSingleton<OpcUaDriver>();
        var sp = services.BuildServiceProvider();
        return new DeviceDriverFactory(sp);
    }

    // ── 1. DevicePollingService 启动/停止 ───────────────────────────────

    [Fact]
    public async Task DevicePollingService_ShouldStartAndStop()
    {
        var deviceManager = CreateDeviceManager();
        var driverFactory = CreateDriverFactory();
        var tagConfigStore = new FakeTagConfigStore();
        var tagStore = CreateTagStore();
        var diag = CreateDiagnostics();

        // 没有设备配置，应该能正常启动/停止
        var service = new DevicePollingService(deviceManager, driverFactory, tagConfigStore, tagStore, diag);
        await service.StartAsync();
        tagStore.TryGet(new TagId("Heartbeat"), out _).Should().BeFalse(); // 不再有假心跳
        await service.StopAsync();
    }

    // ── 2. RecipeEngine 完整链路 ────────────────────────────────────────

    [Fact]
    public async Task RecipeEngine_FullLifecycle()
    {
        var tagStore = CreateTagStore();
        var engine = new RecipeEngine(tagStore);

        // 保存配方
        await engine.SaveRecipeAsync(new RecipeData("TestRecipe", "1.0",
            new[] { new RecipeParameterData("speed", "100"), new RecipeParameterData("temp", "50.5") },
            DateTimeOffset.Now, "admin"));

        // 查询配方
        var recipe = await engine.GetRecipeAsync("TestRecipe");
        recipe.Should().NotBeNull();
        recipe!.Parameters.Should().HaveCount(2);

        // 应用配方 → 写入 TagStore
        RecipeData? applied = null;
        engine.RecipeApplied += (_, r) => applied = r;
        await engine.ApplyAsync("TestRecipe");

        applied.Should().NotBeNull();
        tagStore.TryGet(new TagId("speed"), out var speed).Should().BeTrue();
        speed.Value.Should().Be(100);
        tagStore.TryGet(new TagId("temp"), out var temp).Should().BeTrue();
        temp.Value.Should().Be(50.5);

        // 删除配方
        await engine.DeleteRecipeAsync("TestRecipe");
        (await engine.GetRecipeAsync("TestRecipe")).Should().BeNull();
    }

    // ── 3. TagManager 完整链路 ──────────────────────────────────────────

    [Fact]
    public void TagManager_FullLifecycle()
    {
        var mgr = new TagManager();

        // 添加标签
        var tag = new TagConfig(Guid.NewGuid(), "temp", "dev1", "HR0", TagValueType.Int32);
        mgr.AddTag(tag);

        // 查询
        mgr.GetAllTags().Should().ContainSingle();
        mgr.GetTagsForDevice("dev1").Should().ContainSingle();
        mgr.GetTag(tag.TagId).Should().NotBeNull();

        // 更新
        mgr.UpdateTag(tag with { Address = "HR100" });
        mgr.GetTag(tag.TagId)!.Address.Should().Be("HR100");

        // 删除
        mgr.RemoveTag(tag.TagId);
        mgr.GetAllTags().Should().BeEmpty();
    }

    // ── 4. AlarmNotificationService 完整链路 ────────────────────────────

    [Fact]
    public async Task AlarmNotification_FullLifecycle()
    {
        var alarmService = CreateAlarmService();
        var notifier = new LogAlarmNotifier();
        var svc = new AlarmNotificationService(alarmService);
        svc.AddNotifier(notifier);

        // 添加规则
        alarmService.AddRule(new AlarmRule("r1", "temp", AlarmCondition.GreaterThan,
            AlarmSeverity.Warning, "High temp", true, HighThreshold: 100));

        // 触发评估
        alarmService.Evaluate("temp", 150);

        // 等待通知
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && notifier.Sent.Count == 0)
            await Task.Delay(50);

        notifier.Sent.Should().ContainSingle();
        notifier.Sent[0].Message.Should().Be("High temp");
        notifier.Sent[0].Severity.Should().Be(AlarmSeverity.Warning);
    }

    // ── 5. WorkflowRuntime + 条件分支 ───────────────────────────────────

    [Fact]
    public async Task WorkflowRuntime_WithBranching()
    {
        var logStore = new InMemoryRunLogStore();
        var handler = new TestStepHandler();
        var runtime = new WorkflowRuntimeService(new[] { handler }, logStore);

        // 构建工作流：Start → Decision → (A 或 B)
        var startStep = new WorkflowStep { Id = Guid.NewGuid(), Title = "Begin", Kind = "Start" };
        var decisionStep = new WorkflowStep
        {
            Id = Guid.NewGuid(),
            Title = "Check",
            Kind = "Action",
            Parameters = new Dictionary<string, string> { ["status"] = "go" },
            Branches = new()
            {
                new WorkflowBranch
                {
                    Label = "Go",
                    NextStepId = Guid.NewGuid(), // 后面设置
                    ConditionType = BranchConditionType.Expression,
                    Condition = "param:status == go",
                }
            }
        };
        var stepA = new WorkflowStep { Id = decisionStep.Branches[0].NextStepId, Title = "StepA", Kind = "Action" };
        var stepB = new WorkflowStep { Id = Guid.NewGuid(), Title = "StepB", Kind = "Action" };

        startStep.NextStepId = decisionStep.Id;
        decisionStep.NextStepId = stepB.Id; // 默认走 B

        var workflow = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            Name = "BranchTest",
            Steps = new() { startStep, decisionStep, stepA, stepB }
        };

        await runtime.StartContinuousAsync(workflow);

        // 等待完成
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && runtime.State is WorkflowRunState.Running or WorkflowRunState.Paused)
            await Task.Delay(50);

        runtime.State.Should().Be(WorkflowRunState.Completed);
        handler.ExecutedSteps.Should().Contain("StepA");
        handler.ExecutedSteps.Should().NotContain("StepB"); // 条件匹配，走 A 不走 B
    }

    // ── 6. TagStore → AlarmService 集成 ─────────────────────────────────

    [Fact]
    public void TagStore_To_AlarmService_Integration()
    {
        var tagStore = CreateTagStore();
        var alarmService = CreateAlarmService();

        // 添加规则
        alarmService.AddRule(new AlarmRule("r1", "pressure", AlarmCondition.GreaterThan,
            AlarmSeverity.Critical, "Pressure too high", true, HighThreshold: 100));

        // TagStore 更新触发告警评估
        tagStore.TagUpdated += (_, tv) => alarmService.Evaluate(tv.Id.Value, tv.Value);

        // 写入超限值
        tagStore.Upsert(new TagValue(new TagId("pressure"), 150, TagQuality.Good, DateTimeOffset.Now));

        alarmService.GetActiveAlarms().Should().ContainSingle();
        alarmService.GetActiveAlarms()[0].Severity.Should().Be(AlarmSeverity.Critical);
    }

    // ── 7. DeviceManager + TagManager 集成 ──────────────────────────────

    [Fact]
    public void DeviceManager_TagManager_Integration()
    {
        var deviceManager = CreateDeviceManager();
        var tagManager = new TagManager();

        // 添加设备
        deviceManager.AddDevice(new DeviceDescriptor("plc1", "PLC-1", "ModbusTcp", Host: "192.168.1.1", Port: 502));

        // 添加标签
        tagManager.AddTag(new TagConfig(Guid.NewGuid(), "temp", "plc1", "HR0", TagValueType.Int32));
        tagManager.AddTag(new TagConfig(Guid.NewGuid(), "pressure", "plc1", "HR2", TagValueType.Float));

        // 按设备查询标签
        var plcTags = tagManager.GetTagsForDevice("plc1");
        plcTags.Should().HaveCount(2);

        // 删除设备后，标签仍在（独立管理）
        deviceManager.RemoveDevice("plc1");
        tagManager.GetAllTags().Should().HaveCount(2);
    }

    // ── 辅助类型 ────────────────────────────────────────────────────────

    private sealed class FakeTagConfigStore : ITagConfigStore
    {
        public IReadOnlyList<TagConfig> GetAllTags() => Array.Empty<TagConfig>();
        public IReadOnlyList<TagConfig> GetTagsForDevice(string deviceId) => Array.Empty<TagConfig>();
    }

    private sealed class TestStepHandler : IWorkflowStepHandler
    {
        private readonly List<string> _executed = new();
        private readonly object _gate = new();
        public string[] ExecutedSteps { get { lock (_gate) return _executed.ToArray(); } }

        public bool CanHandle(string kind) => kind == "Start" || kind == "Action";

        public Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
        {
            lock (_gate) _executed.Add(step.Title);
            ctx.Info(step, "done");
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryRunLogStore : IWorkflowRunLogStore
    {
        public List<WorkflowRunLogEntry> Entries { get; } = new();
        public void Append(WorkflowRunLogEntry entry) => Entries.Add(entry);
        public IReadOnlyList<WorkflowRunLogEntry> GetRecent(int take = 200) => Entries.TakeLast(take).ToList().AsReadOnly();
    }
}

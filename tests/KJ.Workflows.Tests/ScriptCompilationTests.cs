using FluentAssertions;
using KJ.Workflows;
using Xunit;

namespace KJ.Workflows.Tests;

public class ScriptCompilationTests
{
    private const string ValidHandlerScript = @"
using KJ.Workflows;
using System.Threading;
using System.Threading.Tasks;

public sealed class MyHandler : IWorkflowStepHandler
{
    public bool CanHandle(string kind) => kind == ""Custom"";

    public Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        ctx.Info(step, ""Custom handler executed"");
        return Task.CompletedTask;
    }
}";

    private const string InvalidScript = @"
this is not valid C# code!!!";

    private const string NoHandlerScript = @"
public class NotAHandler
{
    public void DoSomething() { }
}";

    [Fact]
    public void Compile_ShouldSucceed_WithValidCode()
    {
        var compiler = new ScriptCompilationService();
        var result = compiler.Compile(ValidHandlerScript);

        result.Success.Should().BeTrue(string.Join(Environment.NewLine, result.Errors));
        result.Assembly.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Compile_ShouldFail_WithInvalidCode()
    {
        var compiler = new ScriptCompilationService();
        var result = compiler.Compile(InvalidScript);

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void FindHandler_ShouldFind_WhenHandlerExists()
    {
        var compiler = new ScriptCompilationService();
        var result = compiler.Compile(ValidHandlerScript);

        var handler = compiler.FindHandler(result.Assembly!);

        handler.Should().NotBeNull();
        handler!.CanHandle("Custom").Should().BeTrue();
        handler.CanHandle("Other").Should().BeFalse();
    }

    [Fact]
    public void FindHandler_ShouldReturnNull_WhenNoHandler()
    {
        var compiler = new ScriptCompilationService();
        var result = compiler.Compile(NoHandlerScript);

        var handler = compiler.FindHandler(result.Assembly!);

        handler.Should().BeNull();
    }

    [Fact]
    public async Task DynamicStepHandler_ShouldLoadAndExecute()
    {
        var compiler = new ScriptCompilationService();
        var dynamic = new DynamicStepHandler(compiler);

        var loaded = dynamic.LoadScript(ValidHandlerScript, "Custom");

        loaded.Should().BeTrue();
        dynamic.IsCompiled.Should().BeTrue();
        dynamic.CanHandle("Custom").Should().BeTrue();

        var logs = new List<WorkflowRunLogEntry>();
        var ctx = new WorkflowExecutionContext(Guid.NewGuid(), e => logs.Add(e));
        var step = new WorkflowStep { Title = "Test", Kind = "Custom" };

        await dynamic.ExecuteAsync(step, ctx, CancellationToken.None);

        logs.Should().Contain(l => l.Message.Contains("Custom handler executed"));
    }

    [Fact]
    public void DynamicStepHandler_ShouldFailLoad_WithInvalidScript()
    {
        var compiler = new ScriptCompilationService();
        var dynamic = new DynamicStepHandler(compiler);

        var loaded = dynamic.LoadScript(InvalidScript);

        loaded.Should().BeFalse();
        dynamic.IsCompiled.Should().BeFalse();
        dynamic.CompilationErrors.Should().NotBeEmpty();
    }

    [Fact]
    public void DynamicStepHandler_ShouldSupportHotReload()
    {
        var compiler = new ScriptCompilationService();
        var dynamic = new DynamicStepHandler(compiler);

        // 第一次加载
        dynamic.LoadScript(ValidHandlerScript, "Custom");
        dynamic.CanHandle("Custom").Should().BeTrue();

        // 热重载为新的 handler
        var newScript = @"
using KJ.Workflows;
using System.Threading;
using System.Threading.Tasks;

public sealed class NewHandler : IWorkflowStepHandler
{
    public bool CanHandle(string kind) => kind == ""NewKind"";
    public Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        ctx.Info(step, ""New handler"");
        return Task.CompletedTask;
    }
}";

        dynamic.LoadScript(newScript, "NewKind");
        dynamic.CanHandle("Custom").Should().BeFalse();
        dynamic.CanHandle("NewKind").Should().BeTrue();
    }

    [Fact]
    public void DynamicStepHandler_ShouldSkipReload_WhenScriptUnchanged()
    {
        var compiler = new ScriptCompilationService();
        var dynamic = new DynamicStepHandler(compiler);

        dynamic.LoadScript(ValidHandlerScript, "Custom");
        var result = dynamic.LoadScript(ValidHandlerScript, "Custom");

        result.Should().BeTrue(); // 跳过重新编译
    }
}

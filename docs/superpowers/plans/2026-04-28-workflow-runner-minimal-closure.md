# WorkflowRunner（最小可运行闭环）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在流程编辑页增加“运行”按钮，运行时按 `NextStepId` 执行步骤链；对 `Plc.Ads.Read/Write` 先用模拟 handler 发布 `TagValueChangedMessage`（TagKey 使用前缀规则 `ads:<symbol>`）；同时把运行日志写入内存 store，并在“运行记录”页展示。

**Architecture:** `KJ.Domain` 增加 workflow 运行内核（Runner + handler 接口 + 运行事件/上下文）；`KJ.Infrastructure` 提供模拟 ADS handler（通过 MassTransit `IPublishEndpoint` 发布 tag 消息）与 `IWorkflowRunLogStore` 内存实现；`KJ.Modules.Monitoring.WinUI` 在编辑页调用 `IWorkflowRunner` 并新增运行按钮，在运行记录页读取日志 store 展示。

**Tech Stack:** WinUI 3 + Prism（DI/导航）+ MassTransit（IPublishEndpoint）+ System.Text.Json（现有工作流模型）

---

## File Structure（本计划涉及的文件）

**Create**
- `src/KJ.Domain/Workflows/WorkflowExecutionContext.cs`
- `src/KJ.Domain/Workflows/IWorkflowStepHandler.cs`
- `src/KJ.Domain/Workflows/IWorkflowRunner.cs`
- `src/KJ.Domain/Workflows/WorkflowRunner.cs`
- `src/KJ.Infrastructure/Workflows/WorkflowRunLogStore.cs`
- `src/KJ.Infrastructure/Workflows/SimAdsHandlers.cs`
- `src/KJ.Infrastructure/Workflows/WorkflowRunLogModels.cs`

**Modify**
- `src/KJ.App/App.xaml.cs`（注册 Runner / Handler / LogStore）
- `src/KJ.Modules.Monitoring.WinUI/Views/WorkflowEditorPage.xaml`（增加“运行”按钮）
- `src/KJ.Modules.Monitoring.WinUI/ViewModels/WorkflowEditorViewModel.cs`（新增 RunCommand）
- `src/KJ.Modules.Monitoring.WinUI/Views/WorkflowRunsPage.xaml`（展示日志）
- `src/KJ.Modules.Monitoring.WinUI/ViewModels/WorkflowRunsViewModel.cs`（读取 log store）

**Build Verification**
- `dotnet build "src/KJ.App/KJ.App.csproj" -c Debug`

---

### Task 1: Domain 侧引入 Runner 与 Handler 接口

**Files:**
- Create: `src/KJ.Domain/Workflows/IWorkflowStepHandler.cs`
- Create: `src/KJ.Domain/Workflows/WorkflowExecutionContext.cs`
- Create: `src/KJ.Domain/Workflows/IWorkflowRunner.cs`
- Create: `src/KJ.Domain/Workflows/WorkflowRunner.cs`

- [ ] **Step 1: 定义 handler 接口与执行上下文**

```csharp
namespace KJ.Domain.Workflows;

public interface IWorkflowStepHandler
{
    bool CanHandle(string kind);
    Task ExecuteAsync(KJ.Modules.Monitoring.Workflows.WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct);
}

public sealed class WorkflowExecutionContext
{
    public WorkflowExecutionContext(Guid runId, Action<WorkflowRunLogEntry> log)
    {
        RunId = runId;
        _log = log;
    }

    private readonly Action<WorkflowRunLogEntry> _log;
    public Guid RunId { get; }

    public void Info(Guid stepId, string kind, string message) =>
        _log(new WorkflowRunLogEntry(DateTimeOffset.Now, RunId, stepId, kind, message, success: true, error: null));

    public void Error(Guid stepId, string kind, string message, string? error) =>
        _log(new WorkflowRunLogEntry(DateTimeOffset.Now, RunId, stepId, kind, message, success: false, error: error));
}
```

- [ ] **Step 2: 定义 Runner 接口与最小实现（按 NextStepId 执行）**

```csharp
namespace KJ.Domain.Workflows;

public interface IWorkflowRunner
{
    Task<WorkflowRunResult> RunAsync(KJ.Modules.Monitoring.Workflows.WorkflowDefinition workflow, CancellationToken ct = default);
}

public sealed record WorkflowRunResult(Guid RunId, bool Success, DateTimeOffset StartedAt, DateTimeOffset EndedAt, string? Error);
```

Runner 实现要点：
- 选择起点：优先 `Kind == "Start"`，否则 `Steps[0]`
- 循环按 `NextStepId` 走，最大步数 `Steps.Count + 5`
- 为每一步找到第一个 `CanHandle(kind)` 的 handler 并执行
- 若无 handler 或异常：记录失败并停止

- [ ] **Step 3: 编译验证（KJ.Domain）**

Run: `dotnet build "src/KJ.Domain/KJ.Domain.csproj" -c Debug`
Expected: `已成功生成。`

---

### Task 2: Infrastructure 侧提供运行日志 store + 模拟 ADS handlers

**Files:**
- Create: `src/KJ.Infrastructure/Workflows/WorkflowRunLogModels.cs`
- Create: `src/KJ.Infrastructure/Workflows/WorkflowRunLogStore.cs`
- Create: `src/KJ.Infrastructure/Workflows/SimAdsHandlers.cs`

- [ ] **Step 1: 定义日志模型与 store 接口**

```csharp
namespace KJ.Infrastructure.Workflows;

public sealed record WorkflowRunLogEntry(
    DateTimeOffset Timestamp,
    Guid RunId,
    Guid StepId,
    string Kind,
    string Message,
    bool Success,
    string? Error);

public interface IWorkflowRunLogStore
{
    void Append(WorkflowRunLogEntry entry);
    IReadOnlyList<WorkflowRunLogEntry> GetRecent(int take = 200);
}
```

- [ ] **Step 2: 实现内存 store（线程安全，保留最近 N 条）**

要求：
- 内部用 lock 或 ConcurrentQueue
- 最大保留 2000 条

- [ ] **Step 3: 实现模拟 ADS handlers（发布 TagValueChangedMessage）**

行为：
- `Plc.Ads.Read`：发布 `TagValueChangedMessage`，TagKey=`ads:<symbol>`，Value=`"sim:<HH:mm:ss.fff>"`（或数字递增）
- `Plc.Ads.Write`：发布 `TagValueChangedMessage`，TagKey=`ads:<symbol>`，Value=`value` 参数

- [ ] **Step 4: 编译验证（KJ.Infrastructure）**

Run: `dotnet build "src/KJ.Infrastructure/KJ.Infrastructure.csproj" -c Debug`

---

### Task 3: DI 注册（App 启动时可 Resolve）

**Files:**
- Modify: `src/KJ.App/App.xaml.cs`

- [ ] **Step 1: 注册 Runner、日志 store、handlers**

要求：
- `IWorkflowRunLogStore` 单例
- `IWorkflowRunner` 单例
- handlers 作为 `IEnumerable<IWorkflowStepHandler>` 注入（多个实现）

- [ ] **Step 2: 编译验证（KJ.App）**

Run: `dotnet build "src/KJ.App/KJ.App.csproj" -c Debug`

---

### Task 4: WorkflowEditor 增加 RunCommand + 按钮

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/Views/WorkflowEditorPage.xaml`
- Modify: `src/KJ.Modules.Monitoring.WinUI/ViewModels/WorkflowEditorViewModel.cs`

- [ ] **Step 1: XAML 增加“运行”按钮并绑定 `RunCommand`**
- [ ] **Step 2: ViewModel 注入 `IWorkflowRunner` 与 `IWorkflowRunLogStore`（或仅 Runner，日志由 runner 通过 ctx 回调写入 store）**
- [ ] **Step 3: `RunCommand` 生成 workflow 快照并调用 runner**

运行状态展示：
- 运行中：`SaveStatusText = "运行中…"`
- 成功/失败：在 `SaveStatusText` 写 `运行完成` / `运行失败：...`

- [ ] **Step 4: 编译验证（Monitoring.WinUI + KJ.App）**

Run: `dotnet build "src/KJ.App/KJ.App.csproj" -c Debug`

---

### Task 5: WorkflowRunsPage 展示运行日志

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/Views/WorkflowRunsPage.xaml`
- Modify: `src/KJ.Modules.Monitoring.WinUI/ViewModels/WorkflowRunsViewModel.cs`

- [ ] **Step 1: ViewModel 注入 `IWorkflowRunLogStore`，提供 `Items` 与 `RefreshCommand`**
- [ ] **Step 2: XAML 用 `ListView` 展示：时间、Kind、Message、Success**
- [ ] **Step 3: 编译验证（KJ.App）**

Run: `dotnet build "src/KJ.App/KJ.App.csproj" -c Debug`

---

### Task 6: 手工验收（本机 UI）

- [ ] **Step 1: 运行 App**

Run: `dotnet run --project "src/KJ.App/KJ.App.csproj" -c Debug`

- [ ] **Step 2: 在流程编辑页点击“运行”**

期望：
- 不崩溃
- “运行记录”页能看到日志
- Tag 监控页/数据库能看到新增 `ads:<symbol>` 的 tag history（由现有 consumer 写入）


## 背景

当前已完成：
- 流程编辑器：`KJ.Modules.Monitoring.WinUI` 的 `WorkflowEditorPage/WorkflowEditorViewModel`
  - 支持自动保存（0.8s debounce）、异常退出草稿恢复（R2）、离开拦截（保存/不保存/取消）
- 流程存储：`IWorkflowStore` + `WorkflowJsonStore`（LocalAppData JSON）
- 消息与标签管道：`MassTransit` + `TagValueChangedConsumer`
  - 能将 TagValueChangedMessage 落库、更新 `ITagStore`，并触发质量告警
- 驱动抽象：`KJ.Drivers.Abstractions`（`IDeviceDriver` / `TagReadRequest` / `TagWriteRequest`）
- Beckhoff ADS 驱动：目前是占位实现（能发布诊断事件，但 Read/Write 不返回真实值）

当前缺口：
- “流程运行逻辑”尚未被任何 UI 调用；`WorkflowRunsPage` 仍为占位
- 没有 `WorkflowRunner`（执行步骤）、没有步骤处理器（按 Kind 调用驱动/模拟）
- 没有最小的“运行记录/诊断”闭环展示

## 目标（最小可用闭环，优先模块封装）

以“流程编辑页的运行按钮”为入口，完成一个可运行闭环：

1. 在 `WorkflowEditorPage` 增加 **运行** 按钮（先做运行，不做单步/暂停）。
2. 点击运行后：
   - 从当前编辑态（内存）生成 workflow 快照并执行（不依赖保存）
   - 逐步执行 `Steps`（按 `NextStepId` 链）
   - 对 `Plc.Ads.Read/Plc.Ads.Write` 先实现 **模拟步骤处理器**：
     - Read：生成一个 `TagValueChangedMessage`（TagKey 使用 `symbol` 或拼接 key），Value 为模拟值
     - Write：同样生成 TagValueChangedMessage（Value 使用 `value` 参数）
   - 通过现有 Tag 管道使 UI 可观察到结果（比如 Tag 监控页/数据库 tag history）
3. 运行过程中产生“运行事件”并可在 `WorkflowRunsPage` 看到最近一次运行日志（先内存/文件，后续再落库）。

## 非目标（本轮不做）

- 真实 ADS 通讯（驱动目前占位，先不接 Twincat）
- 分支/多出口流程模型
- 完整运行控制（单步/暂停/继续/重试）
- 将运行记录落到 EF Core（下一轮再做迁移）

## 架构与模块边界（推荐）

### 运行内核（Domain）

新增 `KJ.Domain.Workflows`（或 `KJ.Domain/Workflows`）：

- `WorkflowRunRequest`：包含 workflowId、可选名称、触发来源（UI）、可选 TraceId
- `WorkflowRunResult`：成功/失败、结束时间、错误
- `IWorkflowRunner`：
  - `Task<WorkflowRunResult> RunAsync(WorkflowDefinition workflow, CancellationToken ct)`
  - 运行时发布事件（见下）
- 运行事件（in-proc）：
  - `WorkflowRunStarted`
  - `WorkflowStepStarted`
  - `WorkflowStepCompleted`
  - `WorkflowRunFailed`
  - `WorkflowRunCompleted`

说明：Domain 只依赖 `WorkflowDefinition` 模型与抽象接口，不依赖 WinUI。

### 步骤处理器（Infrastructure/Drivers 组合）

新增 `IWorkflowStepHandler`（Domain 定义接口，Infrastructure 提供实现）：

- `bool CanHandle(string kind)`
- `Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)`

本轮提供 `SimAdsReadHandler/SimAdsWriteHandler`（放在 `KJ.Infrastructure` 或 `KJ.Modules.Monitoring.WinUI` 的 Services，推荐 Infrastructure）：

- 内部通过 `IPublishEndpoint`（MassTransit）发布 `TagValueChangedMessage`
- 解析 `step.Parameters`：
  - `symbol`：TagKey
  - `type`：仅用于显示/诊断（不做强类型转换）
  - `value`：Write 的 value
  - `amsNetId/amsPort`：本轮只记录到运行日志

### 运行记录（最小闭环：先内存）

新增 `IWorkflowRunLogStore`（Infrastructure 或 Monitoring.WinUI 均可；为后续落库做抽象）：

- `void Append(WorkflowRunLogEntry entry)`
- `IReadOnlyList<WorkflowRunLogEntry> GetRecent(int take = 200)`

`WorkflowRunsPage` 读取该 store 展示最近记录。

后续扩展：将该 store 替换为 EF Core 落库实现，并提供筛选/导出。

## UI 集成点

### WorkflowEditor

- 增加 `RunCommand`（按钮：运行）
- 运行时：
  - 将当前 `_workflow` + `_steps` 组装成快照
  - 调用 `IWorkflowRunner.RunAsync(snapshot)`
  - 将运行状态（Running/Idle/Failed）通过 `SaveStatusText` 或单独字段显示

### WorkflowRuns

- 将占位页面升级为：
  - 最近 200 条日志（时间、runId、step、kind、message、success）
  - 简单刷新按钮（或自动刷新）

## 关键行为约束

- 运行应使用“快照”执行，避免和编辑中的引用对象互相干扰（尤其是 `WorkflowStep` 继承 BindableBase）。
- 运行失败时，必须记录失败原因，并在 UI 给出可见提示（至少在 `WorkflowRunsPage`）。
- 运行执行顺序：
  - 从 `Start` 节点开始（若不存在 Start，使用 Steps[0]）
  - 按 `NextStepId` 迭代，最多执行 `Steps.Count + 5` 步（防止环形造成死循环；先简单保护）。

## 验收标准（本轮）

1. 在流程编辑页点击“运行”，不会崩溃
2. `WorkflowRuns` 页面能看到本次运行的步骤日志
3. Tag 管道有可观察变化：
   - 至少能通过 Tag 监控/数据库看到 `TagValueChanged` 的记录被写入
4. 整体模块边界清晰：Runner 与 Handler 可替换，后续接真实 ADS/落库不返工


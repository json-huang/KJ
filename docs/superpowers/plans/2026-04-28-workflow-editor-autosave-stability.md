# WorkflowEditor（不丢数据：自动保存 + 崩溃恢复）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 `WorkflowEditor` 达到 A2 级“编辑不丢数据”：0.8s 自动保存草稿、异常退出（R2）时提示恢复、脏状态显示与离开拦截。

**Architecture:** 在 `WorkflowEditorViewModel` 中引入统一的 `MarkDirty()` 与 0.8s debounce autosave；在 `WorkflowJsonStore` 增加草稿文件 `*.autosave.json` 与“编辑会话标记”文件，用于异常退出检测与恢复流程。UI 通过现有右侧面板与顶部区域增加保存状态提示，并用 `ContentDialog` 完成恢复/离开确认。

**Tech Stack:** WinUI 3 + Prism（导航/DI）+ .NET 8 + System.Text.Json（现有）

---

## File Structure（本计划涉及的文件）

**Modify**
- `src/KJ.Modules.Monitoring.WinUI/Workflows/WorkflowJsonStore.cs`
- `src/KJ.Modules.Monitoring.WinUI/Workflows/WorkflowModels.cs`
- `src/KJ.Modules.Monitoring.WinUI/ViewModels/WorkflowEditorViewModel.cs`
- `src/KJ.Modules.Monitoring.WinUI/Views/WorkflowEditorPage.xaml`
- `src/KJ.Modules.Monitoring.WinUI/Views/WorkflowEditorPage.xaml.cs`

**Build Verification**
- `dotnet build "src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj" -c Release`

---

### Task 1: Workflow 存储层扩展（正式/草稿/会话标记）

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/Workflows/WorkflowJsonStore.cs`

- [ ] **Step 1: 扩展 `IWorkflowStore`，加入 autosave 与会话标记 API**

目标签名（命名保持一致）：

```csharp
public interface IWorkflowStore
{
    Task<IReadOnlyList<WorkflowDefinition>> ListAsync(CancellationToken cancellationToken = default);
    Task<WorkflowDefinition?> LoadAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // autosave draft
    Task<WorkflowDefinition?> LoadAutosaveAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAutosaveAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);
    Task DeleteAutosaveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasNewerAutosaveAsync(Guid id, CancellationToken cancellationToken = default);

    // abnormal-exit (R2)
    Task MarkEditorSessionOpenAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task MarkEditorSessionClosedAsync(CancellationToken cancellationToken = default);
    Task<Guid?> GetLastUnclosedEditorSessionWorkflowIdAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: `WorkflowJsonStore` 实现上述 API**

实现要点：
- 草稿路径：`{id:N}.autosave.json`
- `ListAsync` 仍只返回正式文件（需排除 `*.autosave.json`）
- `HasNewerAutosaveAsync` 比较文件写入时间或读取 `UpdatedAt`（优先：文件 `LastWriteTimeUtc`，避免反序列化开销）
- 会话标记写到同目录：`editor-session.json`，内容至少包含 `workflowId` 与 `openedAtUtc`
- `MarkEditorSessionClosedAsync` 删除标记文件

- [ ] **Step 3: 编译验证（Monitoring.WinUI）**

Run: `dotnet build "src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj" -c Release`
Expected: `已成功生成。`

---

### Task 2: Workflow 模型小增强（为“变更监听”做铺垫）

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/Workflows/WorkflowModels.cs`

- [ ] **Step 1: 让 `Parameters` 的替换/赋值更安全（可选）**

若需要监听参数变化，建议由 ViewModel 统一入口更新参数（本轮已通过 `SetParam`），因此此任务可只做极小整理：
- 保持向后兼容
- 不引入新依赖

- [ ] **Step 2: 编译验证（Monitoring.WinUI）**

Run: `dotnet build "src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj" -c Release`

---

### Task 3: ViewModel 引入脏状态 + 0.8s 自动保存（写草稿）

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/ViewModels/WorkflowEditorViewModel.cs`

- [ ] **Step 1: 新增保存/脏状态属性（用于 XAML 绑定）**

要求至少包含：
- `bool IsDirty`
- `bool IsSaving`
- `string SaveStatusText`（例如：`● 未保存` / `正在保存…` / `已保存` / `保存失败：...`）

- [ ] **Step 2: 实现 `MarkDirty()` + 0.8s debounce autosave**

核心逻辑：
- 任意编辑变更调用 `MarkDirty()`
- 取消上一次 debounce（`CancellationTokenSource`）
- `await Task.Delay(800, ct)` 后写入 `_store.SaveAutosaveAsync(_workflowSnapshot)`
- 写前把 `_workflow.Steps = _steps.ToList()`（确保用最新步骤）
- autosave 成功后：`IsDirty` 仍可能为 true（若期间有新变更）；否则置为 false，并更新 `SaveStatusText`

- [ ] **Step 3: 覆盖所有变更源**

至少在这些地方调用 `MarkDirty()`：
- `WorkflowName` setter
- `StepTitle`/`StepKind` setter
- `SetParam(...)`
- `AddStep()`、`ConnectToSelected()`
- **拖拽更新 X/Y**：通过订阅每个 `WorkflowStep.PropertyChanged`（监听 `X/Y/NextStepId`）触发 `MarkDirty()`，覆盖画布交互直接改值的情况

- [ ] **Step 4: “保存”按钮写正式文件并清理草稿**

`SaveAsync()` 改为：
- `await _store.SaveAsync(...)`
- 成功后 `await _store.DeleteAutosaveAsync(...)`
- 清理 `IsDirty`/更新 `SaveStatusText`

- [ ] **Step 5: 编译验证（Monitoring.WinUI）**

Run: `dotnet build "src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj" -c Release`

---

### Task 4: R2 崩溃恢复（仅异常退出才提示）

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/ViewModels/WorkflowEditorViewModel.cs`
- Modify: `src/KJ.Modules.Monitoring.WinUI/Views/WorkflowEditorPage.xaml.cs`

- [ ] **Step 1: 页面加载时设置会话标记（open）**

在 `WorkflowEditorPage.OnLoaded` 中：
- 在 `TryLoadFromNavigationAsync()` 之前/之后，调用 `vm.BeginEditorSessionAsync()`（或直接调用 store 方法）

- [ ] **Step 2: 检测未关闭会话 + 新草稿时弹窗**

触发点：打开某个 workflow 后。
规则（R2）：
- 若存在 `editor-session.json` 且 workflowId == 当前 workflowId，视为异常退出
- 若 `HasNewerAutosaveAsync` 为 true，则弹 `ContentDialog`：
  - Primary：恢复草稿（加载 autosave 覆盖）
  - Secondary：丢弃草稿（删除 autosave）
  - Close：取消（保持当前状态）

- [ ] **Step 3: 页面卸载时关闭会话标记（closed）**

在 `WorkflowEditorPage.Unloaded`（新增事件）或合适时机：
- 调用 `MarkEditorSessionClosedAsync()`

- [ ] **Step 4: 编译验证（Monitoring.WinUI）**

Run: `dotnet build "src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj" -c Release`

---

### Task 5: 离开拦截（脏状态提示）

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/ViewModels/WorkflowEditorViewModel.cs`

- [ ] **Step 1: 实现 Prism 导航离开拦截**

实现方式（满足其一即可）：
- `IConfirmNavigationRequest`：脏时弹窗 “保存 / 不保存 / 取消”，并通过 callback 决定是否允许离开

行为：
- **保存**：写正式文件（`SaveAsync`），成功后允许离开
- **不保存**：删除 autosave 草稿并允许离开（视为显式放弃）
- **取消**：阻止离开

- [ ] **Step 2: 编译验证（Monitoring.WinUI）**

Run: `dotnet build "src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj" -c Release`

---

### Task 6: UI 显示保存状态（可见且不打扰）

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/Views/WorkflowEditorPage.xaml`

- [ ] **Step 1: 在标题区域加一行保存状态**

示例目标：
- 标题下方或右上角显示 `SaveStatusText`
- `IsDirty` 时显著一点，但不需要红色（保持工业风克制）

- [ ] **Step 2: 编译验证（Monitoring.WinUI）**

Run: `dotnet build "src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj" -c Release`

---

## 执行方式（本会话）

本会话按 **Inline Execution** 执行以上任务：边改边 build，确保每一步可编译。


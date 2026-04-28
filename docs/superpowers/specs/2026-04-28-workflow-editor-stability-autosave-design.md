## 背景与目标

当前 `KJ.Modules.Monitoring.WinUI` 已有基础流程编辑器：

- `Views/WorkflowEditorPage.xaml`：画布（Canvas）+ 属性面板 + “加一步/保存/连线”
- `ViewModels/WorkflowEditorViewModel.cs`：维护 `WorkflowDefinition` + `ObservableCollection<WorkflowStep>`，并支持从导航参数加载
- `Workflows/WorkflowJsonStore.cs`：将 workflow 以 JSON 写入 LocalAppData（`%LOCALAPPDATA%\KJ\workflows\{id}.json`）
- `Workflows/WorkflowModels.cs`：`WorkflowDefinition` + `WorkflowStep(NextStepId + Parameters + X/Y)`

现状主要短板集中在 **编辑可靠性**（A4 优先级中先做 A2“不丢数据”）：

- 仅手动点击“保存”才落盘；崩溃/误关闭容易丢失编辑内容
- 缺少脏状态（unsaved changes）提示与离开页面拦截
- 缺少崩溃恢复（draft/autosave）机制
- 保存失败没有明确 UI 反馈与重试策略

本设计目标：把“流程编辑”做到 **A2：不丢数据**，再逐步补齐撤销重做与结构校验。

## 非目标（本轮不做）

- 流程表达能力升级（分支/多出口/条件）——仍保持 `NextStepId`
- 运行引擎/单步调试/实时执行
- 完整撤销重做（可在下一轮基于本轮架构补）
- 将 workflow 持久化迁移到 EF Core / 数据库

## 术语

- **正式文件**：`{id}.json`，代表用户明确“保存”的版本（也可由自动保存升级写入，取决于实现策略）
- **草稿文件（autosave）**：`{id}.autosave.json`，高频落盘用于崩溃恢复
- **脏状态**：编辑内容与最后一次成功落盘状态存在差异
- **异常退出**：编辑器未走“正常关闭/正常离开”路径（比如进程崩溃、强退、系统重启）

## 总体方案（方案 1：最小侵入，优先可靠性）

在现有 `WorkflowJsonStore` 的基础上，扩展出“自动保存草稿 + 崩溃恢复 + 脏状态提示”的编辑会话能力：

1. 统一收敛所有会改变 workflow 的操作到 `MarkDirty()`。
2. `MarkDirty()` 触发 `ScheduleAutosave()`，用 **0.8s** 节流（debounce）。
3. 自动保存写入 `*.autosave.json`（草稿），并更新 UI 状态（正在保存/已保存/失败）。
4. 进入编辑器时，如果检测到“上次异常退出”且存在更新的 autosave 草稿，弹出提示（R2）。
5. 离开编辑器/切换页面：若脏，则弹出“保存/不保存/取消离开”。

## 验收标准（A2：不丢数据）

### 自动保存（节流 0.8s）

- 任意编辑行为（改流程名、改步骤属性、改参数、拖拽位置、连线、加一步）都应触发脏状态。
- 最后一次编辑后 **0.8 秒内**，至少完成一次 autosave 落盘（写入 `*.autosave.json`）。
- UI 显示保存状态：
  - **未保存**：有脏数据且尚未成功 autosave
  - **正在保存**：autosave 进行中
  - **已保存**：autosave 成功，且无新脏数据
  - **保存失败**：autosave 报错，展示简要错误，并允许重试（再次编辑或点击重试）

### 崩溃恢复（R2）

- 仅当检测到“上次异常退出”时才提示恢复。
- 当提示出现且 `autosave` 比正式文件新：提供
  - **恢复草稿**：加载 `*.autosave.json` 覆盖当前编辑状态
  - **丢弃草稿**：删除 `*.autosave.json` 并继续使用正式文件
- 若无异常退出标记：不打扰用户（即使草稿更“新”也默认不弹窗）。

### 离开拦截（防误丢）

- 在导航离开/切换页面时：
  - 若无脏数据：直接离开
  - 若有脏数据：弹窗提供 **保存/不保存/取消**
- “保存”应优先写正式文件（或至少确保 autosave 成功并记录清晰的状态）；并在成功后允许离开。

### 打开稳定还原

- 从流程列表进入编辑器并指定 `workflowId` 时，应稳定加载并还原画布（步骤/坐标/连线/属性）。
- 没有 `workflowId` 时可以创建默认流程，但必须避免覆盖已有工作流文件。

## 数据与存储设计

### 文件命名与目录

沿用现有目录（`WorkflowJsonStore`）：

- 目录：`%LOCALAPPDATA%\KJ\workflows\`
- 正式：`{id}.json`
- 草稿：`{id}.autosave.json`
- 异常退出标记：`editor-session.json` 或 `editor-crash.flag`（位于同目录或 `%LOCALAPPDATA%\KJ\` 根下）

### JSON 结构

保持 `WorkflowDefinition` 与 `WorkflowStep` 原结构不破坏既有文件；可新增可选字段（向后兼容）：

- `WorkflowDefinition` 可选新增 `Editor` 或 `Meta`（例如最后选中节点、画布缩放），但本轮不是必须。
- `UpdatedAt` 在正式/草稿保存时均更新。

## 触发 autosave 的变更源（必须覆盖）

- `WorkflowName` 修改
- `SelectedStep` 的 `Title/Kind/Parameters` 修改
- `WorkflowStep.X/Y` 改变（拖拽）
- `WorkflowStep.NextStepId` 改变（连线）
- `AddStep` 新增步骤
- 未来的删除/复制/粘贴等操作（留接口）

实现原则：避免散点触发；尽量集中到 ViewModel 的统一入口。

## 错误处理与可观测性

- autosave IO/序列化失败：
  - UI 显示 “保存失败（点击查看/重试）”
  - 不清除脏状态
  - 后续编辑继续触发重试
- 正式保存失败：
  - 明确提示；不允许“保存后离开”
- 记录关键日志（Debug/Serilog 视项目现状）：保存开始/成功/失败、恢复草稿、丢弃草稿。

## 迁移与兼容

- 现有 `{id}.json` 文件可直接读取，不需要迁移。
- 新增 `*.autosave.json` 不应影响列表展示（`ListAsync` 只枚举 `*.json` 需避免把 autosave 当正式文件列出；或改为排除 `*.autosave.json`）。

## 后续路线（不在本轮）

1. **撤销/重做**：基于统一的 `MarkDirty()`/操作入口，引入命令栈（先覆盖：文本、拖拽、连线、加一步）
2. **结构校验**：断链/孤儿/循环/参数必填，并提供可视化提示与保存阻止策略
3. **交互增强**：多选、对齐、缩放、吸附、快捷键、迷你地图


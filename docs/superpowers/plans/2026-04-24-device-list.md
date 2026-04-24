# DeviceList（工业风设备列表）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在监控模块实现一个“工业风”的 `DeviceList` 页面：从数据库读取设备列表并可刷新/筛选，同时预留“实时状态覆盖层”接口，未来接入 `IDeviceManager`/消息后可覆盖状态列。

**Architecture:** `DeviceListViewModel` 使用 `IServiceScopeFactory` 创建 scope，从 `KjDbContext.Devices` 拉取基线数据，映射为 UI 模型 `DeviceListItem`。UI 用 `ListView + Grid` 实现紧凑表格样式；状态列采用“色块/胶囊”样式。实时覆盖层通过 `IDeviceStatusProvider`（只读）注入，当前可为空实现，渲染时优先用覆盖数据回退到 DB 字段。

**Tech Stack:** WinUI 3 + Prism（导航/DI）+ EF Core（`KjDbContext`）+ .NET 8

---

## File Structure（本计划涉及的文件）

**Create**
- `src/KJ.Modules.Monitoring.WinUI/Models/DeviceListItem.cs`
- `src/KJ.Modules.Monitoring.WinUI/Services/IDeviceStatusProvider.cs`
- `src/KJ.Modules.Monitoring.WinUI/Services/NullDeviceStatusProvider.cs`
- `src/KJ.Modules.Monitoring.WinUI/ViewModels/DeviceListViewModel.cs`

**Modify**
- `src/KJ.Modules.Monitoring.WinUI/MonitoringModule.cs`
- `src/KJ.Modules.Monitoring.WinUI/Views/DeviceListView.xaml`
- `src/KJ.Modules.Monitoring.WinUI/Views/DeviceListView.xaml.cs`（若需要）

**Build Verification**
- `dotnet build -c Release`

---

### Task 1: 为 DeviceList 引入“实时状态覆盖层”最小契约

**Files:**
- Create: `src/KJ.Modules.Monitoring.WinUI/Services/IDeviceStatusProvider.cs`
- Create: `src/KJ.Modules.Monitoring.WinUI/Services/NullDeviceStatusProvider.cs`

- [ ] **Step 1: 定义只读状态模型与 provider 接口**

```csharp
namespace KJ.Modules.Monitoring.Services;

public sealed record DeviceStatusSnapshot(
    Guid DeviceId,
    KJ.Infrastructure.Data.Entities.ConnectionState State,
    DateTimeOffset? LastSeenUtc);

public interface IDeviceStatusProvider
{
    bool TryGet(Guid deviceId, out DeviceStatusSnapshot snapshot);
}
```

- [ ] **Step 2: 提供空实现（不覆盖任何状态）**

```csharp
namespace KJ.Modules.Monitoring.Services;

public sealed class NullDeviceStatusProvider : IDeviceStatusProvider
{
    public bool TryGet(Guid deviceId, out DeviceStatusSnapshot snapshot)
    {
        snapshot = default!;
        return false;
    }
}
```

- [ ] **Step 3: 编译验证（仅 Monitoring.WinUI 项目）**

Run: `dotnet build "src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj" -c Release`
Expected: `已成功生成。`

---

### Task 2: 设备列表 UI 模型与 ViewModel（数据库基线 + 筛选 + 刷新）

**Files:**
- Create: `src/KJ.Modules.Monitoring.WinUI/Models/DeviceListItem.cs`
- Create: `src/KJ.Modules.Monitoring.WinUI/ViewModels/DeviceListViewModel.cs`

- [ ] **Step 1: 定义 UI 行模型（工业表格字段）**

```csharp
namespace KJ.Modules.Monitoring.Models;

public sealed class DeviceListItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty; // host:port
    public string StateText { get; init; } = string.Empty;
    public string LastConnectedText { get; init; } = string.Empty;
}
```

- [ ] **Step 2: 编写 ViewModel（加载/错误/筛选/刷新）**

实现要点（代码应包含以下成员，命名保持一致）：
- `ObservableCollection<DeviceListItem> Items`
- `string FilterText`（变更时触发本地筛选）
- `bool IsLoading`
- `string ErrorMessage`
- `DelegateCommand RefreshCommand`
- `Task LoadAsync()`：使用 `IServiceScopeFactory.CreateAsyncScope()`，解析 `KjDbContext`，`AsNoTracking()` 拉取 `Devices` 并映射到 `DeviceListItem`
- 映射时若 `IDeviceStatusProvider.TryGet(...)` 成功，则用 snapshot 覆盖 `StateText`/`LastConnectedText`（或 `LastSeenUtc` 的显示）；否则回退 DB 字段

- [ ] **Step 3: 编译验证（Monitoring.WinUI）**

Run: `dotnet build "src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj" -c Release`
Expected: `已成功生成。`

---

### Task 3: DeviceListView 工业风表格 UI（紧凑、状态胶囊、无弹窗错误）

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/Views/DeviceListView.xaml`
- (Optional) Modify: `src/KJ.Modules.Monitoring.WinUI/Views/DeviceListView.xaml.cs`

- [ ] **Step 1: 页面布局**

UI 结构建议：
- 顶部：标题“设备列表”、`TextBox`（搜索）、`Button`（刷新）
- 中部：表头行（Name/Type/Endpoint/State/LastConnected）
- 列表：`ListView ItemsSource="{Binding Items}"`，`ItemTemplate` 用 `Grid` 做 5 列对齐
- 状态：`Border` + `TextBlock`，根据 `StateText` 显示；（颜色映射可以先做最小：Connected=Green, Disconnected=Gray, Connecting=Orange）
- 错误：`InfoBar`（或 TextBlock）绑定 `ErrorMessage`，仅当非空显示
- 加载：`ProgressRing IsActive="{Binding IsLoading}"`

- [ ] **Step 2: 页面加载触发首次 Load**

方案：在 `DeviceListView.xaml.cs` 的 `Loaded` 事件中调用 VM 的 `LoadAsync()`（或在 VM 构造时触发一次并做并发保护）。

- [ ] **Step 3: 编译验证（全局）**

Run: `dotnet build -c Release`
Expected: `已成功生成。`

---

### Task 4: 依赖注入与导航注册对齐（确保 RegisterForNavigation 绑定 VM）

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/MonitoringModule.cs`

- [ ] **Step 1: 将 DeviceList 注册调整为带 ViewModel 的导航**

示例形态：
- `RegisterForNavigation<Views.DeviceListView, ViewModels.DeviceListViewModel>("DeviceList");`

- [ ] **Step 2: 注册 `IDeviceStatusProvider` 默认实现**

示例形态：
- `containerRegistry.RegisterSingleton<IDeviceStatusProvider, NullDeviceStatusProvider>();`

- [ ] **Step 3: 编译验证**

Run: `dotnet build -c Release`
Expected: `已成功生成。`


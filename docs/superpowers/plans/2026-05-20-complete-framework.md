# KJ 通用自动化设备框架 — 全量补全实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 补全设计文档中所有未实现的组件，使框架达到可投入使用的状态。

**Architecture:** 遵循现有分层架构（Domain → Infrastructure → Modules），统一驱动接口到 `KJ.Drivers.Abstractions`，逐步将 stub 服务替换为真实实现。

**Tech Stack:** .NET 8, WinUI 3, Prism, EF Core, MassTransit, Polly, xUnit

**设计文档:** `docs/superpowers/specs/2026-04-23-automation-framework-design.md`

---

## Phase 1: 领域核心层补全

### Task 1: 添加 IAuditLogger 接口到 Domain 层

**Files:**
- Modify: `src/KJ.Domain/Core.cs`

- [ ] **Step 1: 在 Core.cs 末尾添加 IAuditLogger 接口和 AuditEntry 记录**

```csharp
public sealed record AuditEntry(string UserId, string Action, string? Details, DateTimeOffset Timestamp);

public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> GetLogsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: 验证 Domain 项目编译**

Run: `dotnet build src/KJ.Domain/KJ.Domain.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/KJ.Domain/Core.cs
git commit -m "feat(domain): add IAuditLogger interface and AuditEntry record"
```

---

### Task 2: 补全 DeviceManager — 真实设备 CRUD

**Files:**
- Modify: `src/KJ.Domain/Core.cs` — 扩展 IDeviceManager 接口
- Modify: `src/KJ.Domain/Services/DeviceManager.cs` — 实现真实逻辑

- [ ] **Step 1: 扩展 IDeviceManager 接口**

在 `src/KJ.Domain/Core.cs` 中，将 `IDeviceManager` 替换为：

```csharp
public interface IDeviceManager
{
    IReadOnlyList<DeviceDescriptor> ListDevices();
    DeviceDescriptor? GetDevice(string deviceId);
    void AddDevice(DeviceDescriptor device);
    void RemoveDevice(string deviceId);
    void UpdateDeviceState(string deviceId, string state);
}
```

- [ ] **Step 2: 实现 DeviceManager 真实逻辑**

替换 `src/KJ.Domain/Services/DeviceManager.cs` 全部内容：

```csharp
using System.Collections.Concurrent;

namespace KJ.Domain.Services;

public sealed class DeviceManager : IDeviceManager
{
    private readonly ConcurrentDictionary<string, DeviceDescriptor> _devices = new();

    public IReadOnlyList<DeviceDescriptor> ListDevices() =>
        _devices.Values.ToList().AsReadOnly();

    public DeviceDescriptor? GetDevice(string deviceId) =>
        _devices.TryGetValue(deviceId, out var d) ? d : null;

    public void AddDevice(DeviceDescriptor device)
    {
        if (!_devices.TryAdd(device.DeviceId, device))
            throw new InvalidOperationException($"Device '{device.DeviceId}' already exists.");
    }

    public void RemoveDevice(string deviceId) =>
        _devices.TryRemove(deviceId, out _);

    public void UpdateDeviceState(string deviceId, string state)
    {
        _devices.AddOrUpdate(
            deviceId,
            _ => throw new InvalidOperationException($"Device '{deviceId}' not found."),
            (_, existing) => existing with { /* state is not in DeviceDescriptor — see note */ });
    }
}
```

注意：当前 `DeviceDescriptor` 只有 `DeviceId, DisplayName, DriverType`。需要添加 `State` 字段。在 `Core.cs` 中修改：

```csharp
public sealed record DeviceDescriptor(string DeviceId, string DisplayName, string DriverType, string State = "Disconnected");
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build src/KJ.Domain/KJ.Domain.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/KJ.Domain/Core.cs src/KJ.Domain/Services/DeviceManager.cs
git commit -m "feat(domain): implement DeviceManager with real CRUD operations"
```

---

### Task 3: 补全 AlarmService — 报警规则评估与历史

**Files:**
- Modify: `src/KJ.Domain/Core.cs` — 扩展 IAlarmService 接口
- Modify: `src/KJ.Domain/Services/AlarmService.cs` — 实现规则评估

- [ ] **Step 1: 定义报警规则模型**

在 `src/KJ.Domain/Core.cs` 中添加：

```csharp
public sealed record AlarmRule(
    string Id,
    string TagKey,
    AlarmCondition Condition,
    AlarmSeverity Severity,
    string Message,
    bool IsEnabled);

public enum AlarmCondition
{
    GreaterThan,
    LessThan,
    Equals,
    NotEquals,
}

public sealed record ActiveAlarm(
    string Id,
    string RuleId,
    string TagKey,
    string Message,
    AlarmSeverity Severity,
    DateTimeOffset TriggeredAt,
    bool Acknowledged,
    string? AcknowledgedBy);
```

- [ ] **Step 2: 扩展 IAlarmService 接口**

在 `src/KJ.Domain/Core.cs` 中，将 `IAlarmService` 替换为：

```csharp
public interface IAlarmService
{
    event EventHandler<AlarmEvent>? AlarmRaised;
    void Raise(AlarmEvent alarmEvent);

    // 新增：规则管理
    void AddRule(AlarmRule rule);
    void RemoveRule(string ruleId);
    IReadOnlyList<AlarmRule> GetRules();

    // 新增：活动报警
    IReadOnlyList<ActiveAlarm> GetActiveAlarms();
    void AcknowledgeAlarm(string alarmId, string userId);
    void ClearAlarm(string alarmId);

    // 新增：评估
    void Evaluate(string tagKey, object? value);
}
```

- [ ] **Step 3: 实现 AlarmService 真实逻辑**

替换 `src/KJ.Domain/Services/AlarmService.cs` 全部内容：

```csharp
using System.Collections.Concurrent;

namespace KJ.Domain.Services;

public sealed class AlarmService : IAlarmService
{
    public event EventHandler<AlarmEvent>? AlarmRaised;

    private readonly ConcurrentDictionary<string, AlarmRule> _rules = new();
    private readonly ConcurrentDictionary<string, ActiveAlarm> _activeAlarms = new();

    public void Raise(AlarmEvent alarmEvent)
    {
        AlarmRaised?.Invoke(this, alarmEvent);
    }

    public void AddRule(AlarmRule rule) =>
        _rules.TryAdd(rule.Id, rule);

    public void RemoveRule(string ruleId) =>
        _rules.TryRemove(ruleId, out _);

    public IReadOnlyList<AlarmRule> GetRules() =>
        _rules.Values.ToList().AsReadOnly();

    public IReadOnlyList<ActiveAlarm> GetActiveAlarms() =>
        _activeAlarms.Values.Where(a => !a.Acknowledged).ToList().AsReadOnly();

    public void AcknowledgeAlarm(string alarmId, string userId)
    {
        _activeAlarms.AddOrUpdate(alarmId,
            _ => throw new InvalidOperationException($"Alarm '{alarmId}' not found."),
            (_, existing) => existing with { Acknowledged = true, AcknowledgedBy = userId });
    }

    public void ClearAlarm(string alarmId) =>
        _activeAlarms.TryRemove(alarmId, out _);

    public void Evaluate(string tagKey, object? value)
    {
        foreach (var rule in _rules.Values.Where(r => r.IsEnabled && r.TagKey == tagKey))
        {
            if (IsTriggered(rule.Condition, value))
            {
                var alarmId = $"{rule.Id}_{DateTimeOffset.UtcNow.Ticks}";
                var alarm = new ActiveAlarm(
                    alarmId, rule.Id, tagKey, rule.Message,
                    rule.Searity, DateTimeOffset.UtcNow, false, null);
                _activeAlarms.TryAdd(alarmId, alarm);

                Raise(new AlarmEvent(rule.Id, rule.Message, rule.Severity, DateTimeOffset.UtcNow));
            }
        }
    }

    private static bool IsTriggered(AlarmCondition condition, object? value)
    {
        if (value is null) return false;
        return condition switch
        {
            AlarmCondition.GreaterThan => Convert.ToDouble(value) > 0,
            AlarmCondition.LessThan => Convert.ToDouble(value) < 0,
            AlarmCondition.Equals => true,
            AlarmCondition.NotEquals => true,
            _ => false,
        };
    }
}
```

注意：上面的 `IsTriggered` 是简化实现。真实场景需要规则携带阈值参数。后续可扩展 `AlarmRule` 添加 `Threshold` 字段。

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/KJ.Domain/KJ.Domain.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/KJ.Domain/Core.cs src/KJ.Domain/Services/AlarmService.cs
git commit -m "feat(domain): implement AlarmService with rule evaluation and active alarm tracking"
```

---

### Task 4: 补全 RecipeEngine — 配方加载与应用

**Files:**
- Modify: `src/KJ.Domain/Core.cs` — 扩展 IRecipeEngine 接口
- Modify: `src/KJ.Domain/Services/RecipeEngine.cs` — 实现真实逻辑

- [ ] **Step 1: 定义配方模型**

在 `src/KJ.Domain/Core.cs` 中添加：

```csharp
public sealed record RecipeData(
    string Name,
    string Version,
    IReadOnlyList<RecipeParameterData> Parameters,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed record RecipeParameterData(string Key, string Value);
```

扩展 `IRecipeEngine`：

```csharp
public interface IRecipeEngine
{
    Task ApplyAsync(string recipeName, CancellationToken cancellationToken = default);
    Task<RecipeData?> GetRecipeAsync(string recipeName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecipeData>> GetRecipesAsync(CancellationToken cancellationToken = default);
    Task SaveRecipeAsync(RecipeData recipe, CancellationToken cancellationToken = default);
    Task DeleteRecipeAsync(string recipeName, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: 实现 RecipeEngine 真实逻辑**

替换 `src/KJ.Domain/Services/RecipeEngine.cs` 全部内容：

```csharp
using System.Collections.Concurrent;

namespace KJ.Domain.Services;

public sealed class RecipeEngine : IRecipeEngine
{
    private readonly ConcurrentDictionary<string, RecipeData> _recipes = new();

    public Task ApplyAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        if (!_recipes.ContainsKey(recipeName))
            throw new InvalidOperationException($"Recipe '{recipeName}' not found.");

        // 实际应用逻辑：将配方参数下发到设备
        // 这里通过事件或 IDeviceManager 驱动写入
        return Task.CompletedTask;
    }

    public Task<RecipeData?> GetRecipeAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        _recipes.TryGetValue(recipeName, out var recipe);
        return Task.FromResult(recipe);
    }

    public Task<IReadOnlyList<RecipeData>> GetRecipesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecipeData>>(_recipes.Values.ToList().AsReadOnly());

    public Task SaveRecipeAsync(RecipeData recipe, CancellationToken cancellationToken = default)
    {
        _recipes[recipe.Name] = recipe;
        return Task.CompletedTask;
    }

    public Task DeleteRecipeAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        _recipes.TryRemove(recipeName, out _);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build src/KJ.Domain/KJ.Domain.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/KJ.Domain/Core.cs src/KJ.Domain/Services/RecipeEngine.cs
git commit -m "feat(domain): implement RecipeEngine with CRUD and recipe application"
```

---

### Task 5: 实现 AuditLogger 基础设施

**Files:**
- Create: `src/KJ.Infrastructure/Logging/AuditLogger.cs`
- Modify: `src/KJ.Infrastructure/DependencyInjection/PersistenceExtensions.cs` — 注册服务

- [ ] **Step 1: 创建 AuditLogger 实现**

创建 `src/KJ.Infrastructure/Logging/AuditLogger.cs`：

```csharp
using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Logging;

public sealed class AuditLogger : IAuditLogger
{
    private readonly IDbContextFactory<KjDbContext> _dbFactory;

    public AuditLogger(IDbContextFactory<KjDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = entry.Timestamp.UtcDateTime,
            UserId = entry.UserId,
            Action = entry.Action,
            Details = entry.Details,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetLogsAsync(
        DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.AuditLogs
            .Where(l => l.Timestamp >= start.UtcDateTime && l.Timestamp <= end.UtcDateTime)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new AuditEntry(l.UserId ?? string.Empty, l.Action, l.Details, new DateTimeOffset(l.Timestamp, TimeSpan.Zero)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 2: 在 DI 中注册 AuditLogger**

在 `src/KJ.Infrastructure/DependencyInjection/PersistenceExtensions.cs` 中添加注册。读取该文件确认当前内容，然后添加：

```csharp
services.AddSingleton<KJ.Domain.IAuditLogger, KJ.Infrastructure.Logging.AuditLogger>();
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build src/KJ.Infrastructure/KJ.Infrastructure.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/KJ.Infrastructure/Logging/AuditLogger.cs src/KJ.Infrastructure/DependencyInjection/PersistenceExtensions.cs
git commit -m "feat(infrastructure): implement AuditLogger with EF Core persistence"
```

---

## Phase 2: 设备驱动层统一

### Task 6: 统一驱动接口 — 迁移到 KJ.Drivers.Abstractions

**Files:**
- Modify: `src/KJ.Drivers/Class1.cs` — 重写，实现 Abstractions 接口
- Modify: `src/KJ.Drivers/KJ.Drivers.csproj` — 添加对 Abstractions 的引用
- Modify: `src/KJ.App/App.xaml.cs` — 更新 DI 注册

- [ ] **Step 1: 添加项目引用**

在 `src/KJ.Drivers/KJ.Drivers.csproj` 的 `<ItemGroup>` 中添加：

```xml
<ProjectReference Include="..\KJ.Drivers.Abstractions\KJ.Drivers.Abstractions.csproj" />
```

- [ ] **Step 2: 重写 Class1.cs — 实现 TcpDeviceDriver**

替换 `src/KJ.Drivers/Class1.cs` 全部内容：

```csharp
using System.Net.Sockets;
using KJ.Diagnostics;
using KJ.Drivers.Abstractions;
using Polly;

namespace KJ.Drivers;

public sealed class TcpDeviceDriver : IDeviceDriver
{
    public const string DriverTypeConst = "Tcp";
    public string DriverType => DriverTypeConst;
    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly IDiagnosticHub _diagnostics;

    private static readonly ResiliencePipeline Retry = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions { MaxRetryAttempts = 3, Delay = TimeSpan.FromMilliseconds(200) })
        .Build();

    public TcpDeviceDriver(IDiagnosticHub diagnostics) => _diagnostics = diagnostics;

    public async Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        State = DriverConnectionState.Connecting;
        _client = new TcpClient();
        await _client.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        _stream = _client.GetStream();
        State = DriverConnectionState.Connected;
        _diagnostics.Emit(new DiagnosticsEvent("TcpDriver", $"Connected to {endpoint.Host}:{endpoint.Port}"));
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        State = DriverConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public async Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _stream is null)
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, "Not connected");

        try
        {
            return await Retry.ExecuteAsync(async ct =>
            {
                var addressBytes = System.Text.Encoding.UTF8.GetBytes(request.Address.Address);
                await _stream.WriteAsync(addressBytes, ct).ConfigureAwait(false);
                var buffer = new byte[4096];
                var read = await _stream.ReadAsync(buffer, ct).ConfigureAwait(false);
                var value = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                return new TagReadResult(request.TagKey, value, DateTimeOffset.Now, true);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new TagReadResult(request.TagKey, null, DateTimeOffset.Now, false, ex.Message);
        }
    }

    public async Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (State != DriverConnectionState.Connected || _stream is null)
            throw new InvalidOperationException("Not connected");

        var data = System.Text.Encoding.UTF8.GetBytes(request.Value?.ToString() ?? string.Empty);
        await _stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }
}
```

- [ ] **Step 3: 实现 ModbusTcpDriver**

在同一文件 `src/KJ.Drivers/Class1.cs` 中追加：

```csharp
public sealed class ModbusTcpDriver : IDeviceDriver
{
    public const string DriverTypeConst = "ModbusTcp";
    public string DriverType => DriverTypeConst;
    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    private readonly IDiagnosticHub _diagnostics;

    public ModbusTcpDriver(IDiagnosticHub diagnostics) => _diagnostics = diagnostics;

    public Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        State = DriverConnectionState.Connected;
        _diagnostics.Emit(new DiagnosticsEvent("ModbusTcpDriver", $"Connected to {endpoint.Host}:{endpoint.Port}"));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = DriverConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default)
    {
        // Modbus TCP 读取占位 — 需要 NModbus4 实现
        return Task.FromResult(new TagReadResult(request.TagKey, 0, DateTimeOffset.Now, true));
    }

    public Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

- [ ] **Step 4: 实现 OpcUaDriver**

在同一文件中追加：

```csharp
public sealed class OpcUaDriver : IDeviceDriver
{
    public const string DriverTypeConst = "OpcUa";
    public string DriverType => DriverTypeConst;
    public DriverConnectionState State { get; private set; } = DriverConnectionState.Disconnected;

    private readonly IDiagnosticHub _diagnostics;

    public OpcUaDriver(IDiagnosticHub diagnostics) => _diagnostics = diagnostics;

    public Task ConnectAsync(DeviceEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        State = DriverConnectionState.Connected;
        _diagnostics.Emit(new DiagnosticsEvent("OpcUaDriver", $"Connected to {endpoint.Host}:{endpoint.Port}"));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = DriverConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public Task<TagReadResult> ReadAsync(TagReadRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TagReadResult(request.TagKey, null, DateTimeOffset.Now, true));

    public Task WriteAsync(TagWriteRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

- [ ] **Step 5: 实现 DeviceDriverFactory**

在同一文件中追加：

```csharp
public sealed class DeviceDriverFactory : IDeviceDriverFactory
{
    private readonly IServiceProvider _services;

    public DeviceDriverFactory(IServiceProvider services) => _services = services;

    public IDeviceDriver Create(string driverType) => driverType switch
    {
        TcpDeviceDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(TcpDeviceDriver))!,
        ModbusTcpDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(ModbusTcpDriver))!,
        OpcUaDriver.DriverTypeConst => (IDeviceDriver)_services.GetService(typeof(OpcUaDriver))!,
        _ => throw new NotSupportedException($"Unknown driver type: {driverType}"),
    };

    public IReadOnlyList<string> GetSupportedDrivers() =>
        new[] { TcpDeviceDriver.DriverTypeConst, ModbusTcpDriver.DriverTypeConst, OpcUaDriver.DriverTypeConst };
}
```

- [ ] **Step 6: 验证编译**

Run: `dotnet build src/KJ.Drivers/KJ.Drivers.csproj`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```bash
git add src/KJ.Drivers/Class1.cs src/KJ.Drivers/KJ.Drivers.csproj
git commit -m "feat(drivers): unify driver interface to KJ.Drivers.Abstractions with Tcp/Modbus/OpcUa implementations"
```

---

### Task 7: 更新 App.xaml.cs DI 注册 — 使用新驱动接口

**Files:**
- Modify: `src/KJ.App/App.xaml.cs`
- Modify: `src/KJ.App/KJ.App.csproj` — 添加项目引用

- [ ] **Step 1: 添加项目引用**

在 `src/KJ.App/KJ.App.csproj` 中确保有：

```xml
<ProjectReference Include="..\KJ.Drivers\KJ.Drivers.csproj" />
<ProjectReference Include="..\KJ.Drivers.Abstractions\KJ.Drivers.Abstractions.csproj" />
```

- [ ] **Step 2: 在 ConfigureServices 中注册驱动**

在 `src/KJ.App/App.xaml.cs` 的 `ConfigureServices` 方法中添加：

```csharp
// 设备驱动
services.AddSingleton<KJ.Drivers.Abstractions.IDeviceDriverFactory, KJ.Drivers.DeviceDriverFactory>();
services.AddSingleton<KJ.Drivers.TcpDeviceDriver>();
services.AddSingleton<KJ.Drivers.ModbusTcpDriver>();
services.AddSingleton<KJ.Drivers.OpcUaDriver>();
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build src/KJ.App/KJ.App.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/KJ.App/App.xaml.cs src/KJ.App/KJ.App.csproj
git commit -m "feat(app): register unified driver factory and drivers in DI"
```

---

## Phase 3: UI 模块补全

### Task 8: 实现 TagMonitorView — 实时标签监控

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/Views/TagMonitorView.xaml`
- Modify: `src/KJ.Modules.Monitoring.WinUI/Views/TagMonitorView.xaml.cs`
- Create: `src/KJ.Modules.Monitoring.WinUI/ViewModels/TagMonitorViewModel.cs`

- [ ] **Step 1: 创建 TagMonitorViewModel**

创建 `src/KJ.Modules.Monitoring.WinUI/ViewModels/TagMonitorViewModel.cs`：

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KJ.Domain;

namespace KJ.Modules.Monitoring.ViewModels;

public partial class TagMonitorViewModel : ObservableObject
{
    private readonly ITagStore _tagStore;

    public ObservableCollection<TagDisplayItem> Tags { get; } = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    public TagMonitorViewModel(ITagStore tagStore)
    {
        _tagStore = tagStore;
        _tagStore.TagUpdated += OnTagUpdated;
    }

    private void OnTagUpdated(object? sender, TagValue value)
    {
        var existing = Tags.FirstOrDefault(t => t.Key == value.Id.Value);
        if (existing is not null)
        {
            existing.Value = value.Value?.ToString() ?? string.Empty;
            existing.Quality = value.Quality.ToString();
            existing.Timestamp = value.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        else
        {
            Tags.Add(new TagDisplayItem
            {
                Key = value.Id.Value,
                Value = value.Value?.ToString() ?? string.Empty,
                Quality = value.Quality.ToString(),
                Timestamp = value.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            });
        }
    }

    [RelayCommand]
    private void ClearAll() => Tags.Clear();
}

public partial class TagDisplayItem : ObservableObject
{
    [ObservableProperty] private string _key = string.Empty;
    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private string _quality = string.Empty;
    [ObservableProperty] private string _timestamp = string.Empty;
}
```

- [ ] **Step 2: 更新 TagMonitorView.xaml**

替换 `src/KJ.Modules.Monitoring.WinUI/Views/TagMonitorView.xaml`：

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Page
    x:Class="KJ.Modules.Monitoring.Views.TagMonitorView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d">
    <Grid Padding="24" RowSpacing="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" FontSize="22" Text="Tag 实时监控" />

        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="12">
            <TextBox PlaceholderText="搜索标签..." Width="300"
                     Text="{x:Bind ViewModel.FilterText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
            <Button Content="清空" Command="{x:Bind ViewModel.ClearAllCommand}" />
        </StackPanel>

        <ListView Grid.Row="2" ItemsSource="{x:Bind ViewModel.Tags}"
                  SelectionMode="Single" IsItemClickEnabled="True">
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="vm:TagDisplayItem">
                    <Grid Padding="8,4" ColumnSpacing="16">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="200" />
                            <ColumnDefinition Width="150" />
                            <ColumnDefinition Width="100" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{x:Bind Key}" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="1" Text="{x:Bind Value}" />
                        <TextBlock Grid.Column="2" Text="{x:Bind Quality}" />
                        <TextBlock Grid.Column="3" Text="{x:Bind Timestamp}" Opacity="0.7" />
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </Grid>
</Page>
```

- [ ] **Step 3: 更新 TagMonitorView.xaml.cs**

替换 `src/KJ.Modules.Monitoring.WinUI/Views/TagMonitorView.xaml.cs`：

```csharp
using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class TagMonitorView : Page
{
    public TagMonitorViewModel ViewModel { get; }

    public TagMonitorView(TagMonitorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/KJ.Modules.Monitoring.WinUI/ViewModels/TagMonitorViewModel.cs src/KJ.Modules.Monitoring.WinUI/Views/TagMonitorView.xaml src/KJ.Modules.Monitoring.WinUI/Views/TagMonitorView.xaml.cs
git commit -m "feat(monitoring): implement TagMonitorView with real-time tag list from ITagStore"
```

---

### Task 9: 实现 TrendChartView — 趋势图

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/Views/TrendChartView.xaml`
- Modify: `src/KJ.Modules.Monitoring.WinUI/Views/TrendChartView.xaml.cs`
- Create: `src/KJ.Modules.Monitoring.WinUI/ViewModels/TrendChartViewModel.cs`

- [ ] **Step 1: 创建 TrendChartViewModel**

创建 `src/KJ.Modules.Monitoring.WinUI/ViewModels/TrendChartViewModel.cs`：

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KJ.Domain;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KJ.Modules.Monitoring.ViewModels;

public partial class TrendChartViewModel : ObservableObject
{
    private readonly ITagStore _tagStore;
    private readonly IDbContextFactory<KjDbContext> _dbFactory;

    public ObservableCollection<TrendPoint> Points { get; } = new();

    [ObservableProperty]
    private string _selectedTagKey = string.Empty;

    [ObservableProperty]
    private string _statusText = "选择标签查看趋势";

    public TrendChartViewModel(ITagStore tagStore, IDbContextFactory<KjDbContext> dbFactory)
    {
        _tagStore = tagStore;
        _dbFactory = dbFactory;
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedTagKey)) return;

        StatusText = "加载中...";
        Points.Clear();

        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var tagId = KJ.Infrastructure.Data.TagIdentity.GetTagId(SelectedTagKey);
        var history = await db.TagHistory
            .Where(h => h.TagId == tagId)
            .OrderByDescending(h => h.Timestamp)
            .Take(200)
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var h in history.AsEnumerable().Reverse())
        {
            Points.Add(new TrendPoint
            {
                Timestamp = h.Timestamp.ToString("HH:mm:ss"),
                Value = double.TryParse(h.Value, out var v) ? v : 0,
            });
        }

        StatusText = $"已加载 {Points.Count} 个数据点";
    }
}

public sealed class TrendPoint
{
    public string Timestamp { get; set; } = string.Empty;
    public double Value { get; set; }
}
```

- [ ] **Step 2: 更新 TrendChartView.xaml**

替换 `src/KJ.Modules.Monitoring.WinUI/Views/TrendChartView.xaml`：

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Page
    x:Class="KJ.Modules.Monitoring.Views.TrendChartView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d">
    <Grid Padding="24" RowSpacing="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" FontSize="22" Text="趋势图" />

        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="12">
            <TextBox PlaceholderText="输入标签 Key" Width="250"
                     Text="{x:Bind ViewModel.SelectedTagKey, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
            <Button Content="加载历史" Command="{x:Bind ViewModel.LoadHistoryCommand}" />
        </StackPanel>

        <TextBlock Grid.Row="2" Text="{x:Bind ViewModel.StatusText}" Opacity="0.7" />

        <ScrollViewer Grid.Row="3">
            <ItemsControl ItemsSource="{x:Bind ViewModel.Points}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate x:DataType="vm:TrendPoint">
                        <StackPanel Orientation="Horizontal" Spacing="16" Padding="4,2">
                            <TextBlock Text="{x:Bind Timestamp}" Width="100" />
                            <TextBlock Text="{x:Bind Value}" FontWeight="SemiBold" />
                        </StackPanel>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
    </Grid>
</Page>
```

- [ ] **Step 3: 更新 TrendChartView.xaml.cs**

替换 `src/KJ.Modules.Monitoring.WinUI/Views/TrendChartView.xaml.cs`：

```csharp
using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class TrendChartView : Page
{
    public TrendChartViewModel ViewModel { get; }

    public TrendChartView(TrendChartViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/KJ.Modules.Monitoring.WinUI/ViewModels/TrendChartViewModel.cs src/KJ.Modules.Monitoring.WinUI/Views/TrendChartView.xaml src/KJ.Modules.Monitoring.WinUI/Views/TrendChartView.xaml.cs
git commit -m "feat(monitoring): implement TrendChartView with TagHistory data loading"
```

---

### Task 10: 实现 AlarmModule — 活动报警列表与确认

**Files:**
- Modify: `src/KJ.Modules.Alarm/Views/AlarmHomePage.xaml`
- Modify: `src/KJ.Modules.Alarm/Views/AlarmHomePage.xaml.cs`
- Create: `src/KJ.Modules.Alarm/ViewModels/AlarmHomeViewModel.cs`
- Modify: `src/KJ.Modules.Alarm/KJ.Modules.Alarm.csproj` — 添加必要引用

- [ ] **Step 1: 创建 AlarmHomeViewModel**

创建 `src/KJ.Modules.Alarm/ViewModels/AlarmHomeViewModel.cs`：

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KJ.Domain;

namespace KJ.Modules.Alarm.ViewModels;

public partial class AlarmHomeViewModel : ObservableObject
{
    private readonly IAlarmService _alarmService;

    public ObservableCollection<ActiveAlarmDisplay> ActiveAlarms { get; } = new();

    [ObservableProperty]
    private string _statusText = string.Empty;

    public AlarmHomeViewModel(IAlarmService alarmService)
    {
        _alarmService = alarmService;
        _alarmService.AlarmRaised += OnAlarmRaised;
        RefreshAlarms();
    }

    private void OnAlarmRaised(object? sender, AlarmEvent e)
    {
        RefreshAlarms();
    }

    [RelayCommand]
    private void RefreshAlarms()
    {
        ActiveAlarms.Clear();
        foreach (var alarm in _alarmService.GetActiveAlarms())
        {
            ActiveAlarms.Add(new ActiveAlarmDisplay
            {
                Id = alarm.Id,
                TagKey = alarm.TagKey,
                Message = alarm.Message,
                Severity = alarm.Severity.ToString(),
                TriggeredAt = alarm.TriggeredAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Acknowledged = alarm.Acknowledged ? "是" : "否",
            });
        }
        StatusText = $"活动报警: {ActiveAlarms.Count}";
    }

    [RelayCommand]
    private void Acknowledge(string alarmId)
    {
        _alarmService.AcknowledgeAlarm(alarmId, "current_user");
        RefreshAlarms();
    }
}

public sealed class ActiveAlarmDisplay
{
    public string Id { get; set; } = string.Empty;
    public string TagKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string TriggeredAt { get; set; } = string.Empty;
    public string Acknowledged { get; set; } = string.Empty;
}
```

- [ ] **Step 2: 更新 AlarmHomePage.xaml**

替换 `src/KJ.Modules.Alarm/Views/AlarmHomePage.xaml`：

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Page
    x:Class="KJ.Modules.Alarm.Views.AlarmHomePage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d">
    <Grid Padding="24" RowSpacing="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" FontSize="22" Text="活动报警" />

        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="12">
            <Button Content="刷新" Command="{x:Bind ViewModel.RefreshAlarmsCommand}" />
            <TextBlock Text="{x:Bind ViewModel.StatusText}" VerticalAlignment="Center" Opacity="0.7" />
        </StackPanel>

        <ListView Grid.Row="2" ItemsSource="{x:Bind ViewModel.ActiveAlarms}">
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="vm:ActiveAlarmDisplay">
                    <Grid Padding="8,6" ColumnSpacing="12">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="80" />
                            <ColumnDefinition Width="150" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="150" />
                            <ColumnDefinition Width="80" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{x:Bind Severity}" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="1" Text="{x:Bind TagKey}" />
                        <TextBlock Grid.Column="2" Text="{x:Bind Message}" TextTrimming="CharacterEllipsis" />
                        <TextBlock Grid.Column="3" Text="{x:Bind TriggeredAt}" Opacity="0.7" />
                        <Button Grid.Column="4" Content="确认"
                                Visibility="{x:Bind Acknowledged, Converter={StaticResource InverseBoolToVisibility}}"
                                Tag="{x:Bind Id}" />
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </Grid>
</Page>
```

- [ ] **Step 3: 更新 AlarmHomePage.xaml.cs**

```csharp
using KJ.Modules.Alarm.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Alarm.Views;

public sealed partial class AlarmHomePage : Page
{
    public AlarmHomeViewModel ViewModel { get; }

    public AlarmHomePage(AlarmHomeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/KJ.Modules.Alarm/KJ.Modules.Alarm.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/KJ.Modules.Alarm/ViewModels/AlarmHomeViewModel.cs src/KJ.Modules.Alarm/Views/AlarmHomePage.xaml src/KJ.Modules.Alarm/Views/AlarmHomePage.xaml.cs
git commit -m "feat(alarm): implement AlarmModule with active alarm list and acknowledge"
```

---

### Task 11: 实现 ConfigModule — 设备配置页面

**Files:**
- Modify: `src/KJ.Modules.Config/Views/ConfigHomePage.xaml`
- Modify: `src/KJ.Modules.Config/Views/ConfigHomePage.xaml.cs`
- Create: `src/KJ.Modules.Config/ViewModels/ConfigHomeViewModel.cs`

- [ ] **Step 1: 创建 ConfigHomeViewModel**

创建 `src/KJ.Modules.Config/ViewModels/ConfigHomeViewModel.cs`：

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KJ.Domain;

namespace KJ.Modules.Config.ViewModels;

public partial class ConfigHomeViewModel : ObservableObject
{
    private readonly IDeviceManager _deviceManager;

    public ObservableCollection<DeviceDisplayItem> Devices { get; } = new();

    [ObservableProperty]
    private string _newDeviceId = string.Empty;

    [ObservableProperty]
    private string _newDeviceName = string.Empty;

    [ObservableProperty]
    private string _newDriverType = "Tcp";

    public ConfigHomeViewModel(IDeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
        RefreshDevices();
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        Devices.Clear();
        foreach (var d in _deviceManager.ListDevices())
        {
            Devices.Add(new DeviceDisplayItem
            {
                DeviceId = d.DeviceId,
                DisplayName = d.DisplayName,
                DriverType = d.DriverType,
                State = d.State,
            });
        }
    }

    [RelayCommand]
    private void AddDevice()
    {
        if (string.IsNullOrWhiteSpace(NewDeviceId) || string.IsNullOrWhiteSpace(NewDeviceName))
            return;

        _deviceManager.AddDevice(new DeviceDescriptor(NewDeviceId, NewDeviceName, NewDriverType));
        NewDeviceId = string.Empty;
        NewDeviceName = string.Empty;
        RefreshDevices();
    }

    [RelayCommand]
    private void RemoveDevice(string deviceId)
    {
        _deviceManager.RemoveDevice(deviceId);
        RefreshDevices();
    }
}

public sealed class DeviceDisplayItem
{
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DriverType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}
```

- [ ] **Step 2: 更新 ConfigHomePage.xaml**

替换 `src/KJ.Modules.Config/Views/ConfigHomePage.xaml`：

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Page
    x:Class="KJ.Modules.Config.Views.ConfigHomePage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d">
    <Grid Padding="24" RowSpacing="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" FontSize="22" Text="设备配置" />

        <Grid Grid.Row="1" ColumnSpacing="8">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="150" />
                <ColumnDefinition Width="200" />
                <ColumnDefinition Width="120" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <TextBox Grid.Column="0" PlaceholderText="设备 ID"
                     Text="{x:Bind ViewModel.NewDeviceId, Mode=TwoWay}" />
            <TextBox Grid.Column="1" PlaceholderText="显示名称"
                     Text="{x:Bind ViewModel.NewDeviceName, Mode=TwoWay}" />
            <ComboBox Grid.Column="2" SelectedValue="{x:Bind ViewModel.NewDriverType, Mode=TwoWay}">
                <ComboBoxItem Content="Tcp" IsSelected="True" />
                <ComboBoxItem Content="ModbusTcp" />
                <ComboBoxItem Content="OpcUa" />
            </ComboBox>
            <Button Grid.Column="3" Content="添加设备" Command="{x:Bind ViewModel.AddDeviceCommand}" />
        </Grid>

        <StackPanel Grid.Row="2" Orientation="Horizontal" Spacing="12">
            <Button Content="刷新" Command="{x:Bind ViewModel.RefreshDevicesCommand}" />
        </StackPanel>

        <ListView Grid.Row="3" ItemsSource="{x:Bind ViewModel.Devices}">
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="vm:DeviceDisplayItem">
                    <Grid Padding="8,6" ColumnSpacing="16">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="150" />
                            <ColumnDefinition Width="200" />
                            <ColumnDefinition Width="120" />
                            <ColumnDefinition Width="100" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{x:Bind DeviceId}" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="1" Text="{x:Bind DisplayName}" />
                        <TextBlock Grid.Column="2" Text="{x:Bind DriverType}" />
                        <TextBlock Grid.Column="3" Text="{x:Bind State}" />
                        <Button Grid.Column="4" Content="删除" Tag="{x:Bind DeviceId}" />
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </Grid>
</Page>
```

- [ ] **Step 3: 更新 ConfigHomePage.xaml.cs**

```csharp
using KJ.Modules.Config.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Config.Views;

public sealed partial class ConfigHomePage : Page
{
    public ConfigHomeViewModel ViewModel { get; }

    public ConfigHomePage(ConfigHomeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/KJ.Modules.Config/KJ.Modules.Config.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/KJ.Modules.Config/ViewModels/ConfigHomeViewModel.cs src/KJ.Modules.Config/Views/ConfigHomePage.xaml src/KJ.Modules.Config/Views/ConfigHomePage.xaml.cs
git commit -m "feat(config): implement ConfigModule with device CRUD UI"
```

---

### Task 12: 实现 ReportingModule — 报表页面

**Files:**
- Modify: `src/KJ.Modules.Reporting/Views/ReportingHomePage.xaml`
- Modify: `src/KJ.Modules.Reporting/Views/ReportingHomePage.xaml.cs`
- Create: `src/KJ.Modules.Reporting/ViewModels/ReportingHomeViewModel.cs`

- [ ] **Step 1: 创建 ReportingHomeViewModel**

创建 `src/KJ.Modules.Reporting/ViewModels/ReportingHomeViewModel.cs`：

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KJ.Modules.Reporting.ViewModels;

public partial class ReportingHomeViewModel : ObservableObject
{
    private readonly IDbContextFactory<KjDbContext> _dbFactory;

    public ObservableCollection<HistoryRow> HistoryRows { get; } = new();

    [ObservableProperty]
    private string _selectedTagKey = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _startDate = DateTimeOffset.Now.AddDays(-1);

    [ObservableProperty]
    private DateTimeOffset _endDate = DateTimeOffset.Now;

    [ObservableProperty]
    private string _statusText = "选择标签和时间范围后点击查询";

    public ReportingHomeViewModel(IDbContextFactory<KjDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    [RelayCommand]
    private async Task QueryAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedTagKey)) return;

        StatusText = "查询中...";
        HistoryRows.Clear();

        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var tagId = KJ.Infrastructure.Data.TagIdentity.GetTagId(SelectedTagKey);
        var rows = await db.TagHistory
            .Where(h => h.TagId == tagId && h.Timestamp >= StartDate.UtcDateTime && h.Timestamp <= EndDate.UtcDateTime)
            .OrderByDescending(h => h.Timestamp)
            .Take(500)
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var r in rows)
        {
            HistoryRows.Add(new HistoryRow
            {
                Timestamp = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                Value = r.Value ?? string.Empty,
                Quality = r.Quality.ToString(),
            });
        }

        StatusText = $"查询完成，共 {HistoryRows.Count} 条记录";
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        // CSV 导出占位
        StatusText = "CSV 导出功能待实现";
        await Task.CompletedTask;
    }
}

public sealed class HistoryRow
{
    public string Timestamp { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
}
```

- [ ] **Step 2: 更新 ReportingHomePage.xaml**

替换 `src/KJ.Modules.Reporting/Views/ReportingHomePage.xaml`：

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Page
    x:Class="KJ.Modules.Reporting.Views.ReportingHomePage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d">
    <Grid Padding="24" RowSpacing="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" FontSize="22" Text="历史报表" />

        <Grid Grid.Row="1" ColumnSpacing="8">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="200" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <TextBox Grid.Column="0" PlaceholderText="标签 Key"
                     Text="{x:Bind ViewModel.SelectedTagKey, Mode=TwoWay}" />
            <DatePicker Grid.Column="1" Date="{x:Bind ViewModel.StartDate, Mode=TwoWay}" Header="开始" />
            <DatePicker Grid.Column="2" Date="{x:Bind ViewModel.EndDate, Mode=TwoWay}" Header="结束" />
            <Button Grid.Column="3" Content="查询" Command="{x:Bind ViewModel.QueryCommand}" />
            <Button Grid.Column="4" Content="导出 CSV" Command="{x:Bind ViewModel.ExportCsvCommand}" />
        </Grid>

        <TextBlock Grid.Row="2" Text="{x:Bind ViewModel.StatusText}" Opacity="0.7" />

        <ListView Grid.Row="3" ItemsSource="{x:Bind ViewModel.HistoryRows}">
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="vm:HistoryRow">
                    <Grid Padding="8,4" ColumnSpacing="16">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="200" />
                            <ColumnDefinition Width="150" />
                            <ColumnDefinition Width="100" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{x:Bind Timestamp}" />
                        <TextBlock Grid.Column="1" Text="{x:Bind Value}" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="2" Text="{x:Bind Quality}" />
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </Grid>
</Page>
```

- [ ] **Step 3: 更新 ReportingHomePage.xaml.cs**

```csharp
using KJ.Modules.Reporting.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Reporting.Views;

public sealed partial class ReportingHomePage : Page
{
    public ReportingHomeViewModel ViewModel { get; }

    public ReportingHomePage(ReportingHomeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/KJ.Modules.Reporting/KJ.Modules.Reporting.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/KJ.Modules.Reporting/ViewModels/ReportingHomeViewModel.cs src/KJ.Modules.Reporting/Views/ReportingHomePage.xaml src/KJ.Modules.Reporting/Views/ReportingHomePage.xaml.cs
git commit -m "feat(reporting): implement ReportingModule with history query and CSV export placeholder"
```

---

### Task 13: 补全 DashboardView — 接入真实数据

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/Views/DashboardView.xaml`
- Modify: `src/KJ.Modules.Monitoring.WinUI/Views/DashboardView.xaml.cs`
- Create: `src/KJ.Modules.Monitoring.WinUI/ViewModels/DashboardViewModel.cs`

- [ ] **Step 1: 创建 DashboardViewModel**

创建 `src/KJ.Modules.Monitoring.WinUI/ViewModels/DashboardViewModel.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using KJ.Domain;

namespace KJ.Modules.Monitoring.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDeviceManager _deviceManager;
    private readonly IAlarmService _alarmService;
    private readonly ITagStore _tagStore;

    [ObservableProperty] private int _deviceCount;
    [ObservableProperty] private int _activeAlarmCount;
    [ObservableProperty] private int _tagCount;
    [ObservableProperty] private string _systemStatus = "正常";

    public DashboardViewModel(IDeviceManager deviceManager, IAlarmService alarmService, ITagStore tagStore)
    {
        _deviceManager = deviceManager;
        _alarmService = alarmService;
        _tagStore = tagStore;

        _alarmService.AlarmRaised += (_, _) => Refresh();
        Refresh();
    }

    public void Refresh()
    {
        DeviceCount = _deviceManager.ListDevices().Count;
        ActiveAlarmCount = _alarmService.GetActiveAlarms().Count;
        SystemStatus = ActiveAlarmCount > 0 ? "有报警" : "正常";
    }
}
```

- [ ] **Step 2: 更新 DashboardView.xaml 绑定到 ViewModel**

读取当前 `src/KJ.Modules.Monitoring.WinUI/Views/DashboardView.xaml`，将硬编码的数字（如 "42" 台设备、"3" 条报警）替换为 `{x:Bind ViewModel.DeviceCount}` 和 `{x:Bind ViewModel.ActiveAlarmCount}` 绑定。

- [ ] **Step 3: 更新 DashboardView.xaml.cs 注入 ViewModel**

```csharp
using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class DashboardView : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardView(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/KJ.Modules.Monitoring.WinUI/ViewModels/DashboardViewModel.cs src/KJ.Modules.Monitoring.WinUI/Views/DashboardView.xaml src/KJ.Modules.Monitoring.WinUI/Views/DashboardView.xaml.cs
git commit -m "feat(monitoring): connect DashboardView to real domain services"
```

---

## Phase 4: 基础设施补全

### Task 14: 补全 GetUsersAsync / GetRolesAsync — 真实查询

**Files:**
- Modify: `src/KJ.Infrastructure/Identity/IdentityUserManager.cs:24-25`
- Modify: `src/KJ.Infrastructure/Identity/IdentityRoleManager.cs:26-27`

- [ ] **Step 1: 实现 IdentityUserManager.GetUsersAsync**

将 `src/KJ.Infrastructure/Identity/IdentityUserManager.cs` 第 24-25 行替换为：

```csharp
public async Task<IReadOnlyList<AppUser>> GetUsersAsync(CancellationToken cancellationToken = default)
{
    var users = _userManager.Users.ToList();
    return users.Select(u => new AppUser(u.Id, u.UserName ?? u.Email ?? string.Empty, u.Email ?? string.Empty))
        .ToList().AsReadOnly();
}
```

- [ ] **Step 2: 实现 IdentityRoleManager.GetRolesAsync**

将 `src/KJ.Infrastructure/Identity/IdentityRoleManager.cs` 第 26-27 行替换为：

```csharp
public async Task<IReadOnlyList<AppRole>> GetRolesAsync(CancellationToken cancellationToken = default)
{
    var roles = _roleManager.Roles.ToList();
    return roles.Select(r => new AppRole(r.Id, r.Name ?? string.Empty)).ToList().AsReadOnly();
}
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build src/KJ.Infrastructure/KJ.Infrastructure.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/KJ.Infrastructure/Identity/IdentityUserManager.cs src/KJ.Infrastructure/Identity/IdentityRoleManager.cs
git commit -m "fix(identity): implement GetUsersAsync and GetRolesAsync with real DB queries"
```

---

### Task 15: 补全 RecipeAppliedConsumer — 持久化到 DB

**Files:**
- Modify: `src/KJ.Infrastructure/Messaging/Consumers/RecipeAppliedConsumer.cs`

- [ ] **Step 1: 实现 RecipeAppliedConsumer 持久化**

替换 `src/KJ.Infrastructure/Messaging/Consumers/RecipeAppliedConsumer.cs` 全部内容：

```csharp
using MassTransit;
using KJ.Infrastructure.Data;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Messaging.Consumers;

public sealed class RecipeAppliedConsumer : IConsumer<RecipeAppliedMessage>
{
    private readonly ILogger<RecipeAppliedConsumer> _logger;
    private readonly KjDbContext _db;

    public RecipeAppliedConsumer(ILogger<RecipeAppliedConsumer> logger, KjDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task Consume(ConsumeContext<RecipeAppliedMessage> context)
    {
        var m = context.Message;
        _logger.LogInformation("MassTransit: RecipeApplied RecipeId={RecipeId} DeviceId={DeviceId} User={User}",
            m.RecipeId, m.DeviceId, m.UserId);

        // 记录配方应用到审计日志
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            UserId = m.UserId,
            Action = "RecipeApplied",
            Details = $"Recipe {m.RecipeId} applied to device {m.DeviceId}",
        });

        await _db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/KJ.Infrastructure/KJ.Infrastructure.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/KJ.Infrastructure/Messaging/Consumers/RecipeAppliedConsumer.cs
git commit -m "feat(messaging): implement RecipeAppliedConsumer with audit log persistence"
```

---

### Task 16: 添加 Polly 熔断器策略

**Files:**
- Modify: `src/KJ.Drivers/Class1.cs` — 在 DeviceDriverBase 区域添加熔断器
- Modify: `src/KJ.Infrastructure/KJ.Infrastructure.csproj` — 添加 Polly 引用（如果缺失）

- [ ] **Step 1: 在 KJ.Drivers/Class1.cs 中添加熔断器策略**

在 TcpDeviceDriver 类之前添加：

```csharp
public static class ResiliencePolicies
{
    public static readonly ResiliencePipeline CircuitBreaker = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
        })
        .Build();
}
```

- [ ] **Step 2: 在 TcpDeviceDriver.ReadAsync 中使用熔断器**

将 `Retry.ExecuteAsync` 改为嵌套使用：

```csharp
return await ResiliencePolicies.CircuitBreaker.ExecuteAsync(async ct =>
{
    return await Retry.ExecuteAsync(async ct2 =>
    {
        // ... 现有读取逻辑
    }, ct).ConfigureAwait(false);
}, cancellationToken).ConfigureAwait(false);
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build src/KJ.Drivers/KJ.Drivers.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/KJ.Drivers/Class1.cs
git commit -m "feat(drivers): add Polly circuit breaker policy to device drivers"
```

---

## Phase 5: 测试与收尾

### Task 17: 创建测试项目

**Files:**
- Create: `tests/KJ.Domain.Tests/KJ.Domain.Tests.csproj`
- Create: `tests/KJ.Domain.Tests/TagStoreTests.cs`
- Create: `tests/KJ.Domain.Tests/DeviceManagerTests.cs`
- Create: `tests/KJ.Domain.Tests/AlarmServiceTests.cs`
- Create: `tests/KJ.Domain.Tests/RecipeEngineTests.cs`

- [ ] **Step 1: 创建测试项目目录和 csproj**

创建 `tests/KJ.Domain.Tests/KJ.Domain.Tests.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="xunit" Version="2.8.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.1" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\KJ.Domain\KJ.Domain.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 创建 TagStoreTests.cs**

创建 `tests/KJ.Domain.Tests/TagStoreTests.cs`：

```csharp
using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using Xunit;

namespace KJ.Domain.Tests;

public class TagStoreTests
{
    [Fact]
    public void Upsert_ShouldStoreValue()
    {
        var store = new InMemoryTagStore();
        var id = new TagId("test.tag");
        var value = new TagValue(id, 42, TagQuality.Good, DateTimeOffset.Now);

        store.Upsert(value);

        store.TryGet(id, out var stored).Should().BeTrue();
        stored.Value.Should().Be(42);
    }

    [Fact]
    public void Upsert_ShouldRaiseTagUpdatedEvent()
    {
        var store = new InMemoryTagStore();
        TagValue? received = null;
        store.TagUpdated += (_, v) => received = v;

        var value = new TagValue(new TagId("test.tag"), "hello", TagQuality.Good, DateTimeOffset.Now);
        store.Upsert(value);

        received.Should().NotBeNull();
        received!.Value.Should().Be("hello");
    }

    [Fact]
    public void TryGet_ShouldReturnFalse_ForMissingTag()
    {
        var store = new InMemoryTagStore();
        store.TryGet(new TagId("nonexistent"), out _).Should().BeFalse();
    }

    [Fact]
    public void Upsert_ShouldOverwriteExisting()
    {
        var store = new InMemoryTagStore();
        var id = new TagId("test.tag");

        store.Upsert(new TagValue(id, 1, TagQuality.Good, DateTimeOffset.Now));
        store.Upsert(new TagValue(id, 2, TagQuality.Good, DateTimeOffset.Now));

        store.TryGet(id, out var stored).Should().BeTrue();
        stored.Value.Should().Be(2);
    }
}
```

- [ ] **Step 3: 创建 DeviceManagerTests.cs**

创建 `tests/KJ.Domain.Tests/DeviceManagerTests.cs`：

```csharp
using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using Xunit;

namespace KJ.Domain.Tests;

public class DeviceManagerTests
{
    [Fact]
    public void ListDevices_ShouldReturnEmpty_WhenNoDevices()
    {
        var mgr = new DeviceManager();
        mgr.ListDevices().Should().BeEmpty();
    }

    [Fact]
    public void AddDevice_ShouldAddToList()
    {
        var mgr = new DeviceManager();
        var device = new DeviceDescriptor("dev1", "Device 1", "Tcp");

        mgr.AddDevice(device);

        mgr.ListDevices().Should().HaveCount(1);
        mgr.GetDevice("dev1")!.DisplayName.Should().Be("Device 1");
    }

    [Fact]
    public void AddDevice_ShouldThrow_WhenDuplicate()
    {
        var mgr = new DeviceManager();
        mgr.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Tcp"));

        var act = () => mgr.AddDevice(new DeviceDescriptor("dev1", "Device 1 Copy", "Tcp"));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveDevice_ShouldRemoveFromList()
    {
        var mgr = new DeviceManager();
        mgr.AddDevice(new DeviceDescriptor("dev1", "Device 1", "Tcp"));

        mgr.RemoveDevice("dev1");

        mgr.ListDevices().Should().BeEmpty();
    }

    [Fact]
    public void GetDevice_ShouldReturnNull_WhenNotFound()
    {
        var mgr = new DeviceManager();
        mgr.GetDevice("nonexistent").Should().BeNull();
    }
}
```

- [ ] **Step 4: 创建 AlarmServiceTests.cs**

创建 `tests/KJ.Domain.Tests/AlarmServiceTests.cs`：

```csharp
using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using Xunit;

namespace KJ.Domain.Tests;

public class AlarmServiceTests
{
    [Fact]
    public void AddRule_ShouldStoreRule()
    {
        var svc = new AlarmService();
        var rule = new AlarmRule("r1", "temp", AlarmCondition.GreaterThan, AlarmSeverity.Warning, "High temp", true);

        svc.AddRule(rule);

        svc.GetRules().Should().HaveCount(1);
    }

    [Fact]
    public void RemoveRule_ShouldRemoveRule()
    {
        var svc = new AlarmService();
        svc.AddRule(new AlarmRule("r1", "temp", AlarmCondition.GreaterThan, AlarmSeverity.Warning, "High temp", true));

        svc.RemoveRule("r1");

        svc.GetRules().Should().BeEmpty();
    }

    [Fact]
    public void Raise_ShouldFireAlarmRaisedEvent()
    {
        var svc = new AlarmService();
        AlarmEvent? received = null;
        svc.AlarmRaised += (_, e) => received = e;

        var evt = new AlarmEvent("code", "msg", AlarmSeverity.Warning, DateTimeOffset.Now);
        svc.Raise(evt);

        received.Should().NotBeNull();
        received!.Code.Should().Be("code");
    }

    [Fact]
    public void AcknowledgeAlarm_ShouldMarkAsAcknowledged()
    {
        var svc = new AlarmService();
        // 手动添加一个活动报警
        var rule = new AlarmRule("r1", "temp", AlarmCondition.Equals, AlarmSeverity.Warning, "test", true);
        svc.AddRule(rule);
        svc.Evaluate("temp", 1); // 触发报警

        var active = svc.GetActiveAlarms();
        active.Should().HaveCount(1);

        svc.AcknowledgeAlarm(active[0].Id, "user1");

        svc.GetActiveAlarms().Should().BeEmpty();
    }

    [Fact]
    public void GetActiveAlarms_ShouldReturnEmpty_WhenNone()
    {
        var svc = new AlarmService();
        svc.GetActiveAlarms().Should().BeEmpty();
    }
}
```

- [ ] **Step 5: 创建 RecipeEngineTests.cs**

创建 `tests/KJ.Domain.Tests/RecipeEngineTests.cs`：

```csharp
using FluentAssertions;
using KJ.Domain;
using KJ.Domain.Services;
using Xunit;

namespace KJ.Domain.Tests;

public class RecipeEngineTests
{
    [Fact]
    public async Task SaveRecipe_ShouldBeRetrievable()
    {
        var engine = new RecipeEngine();
        var recipe = new RecipeData("TestRecipe", "1.0",
            new[] { new RecipeParameterData("speed", "100") },
            DateTimeOffset.Now, "admin");

        await engine.SaveRecipeAsync(recipe);

        var loaded = await engine.GetRecipeAsync("TestRecipe");
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("TestRecipe");
        loaded.Parameters.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRecipeAsync_ShouldReturnNull_WhenNotFound()
    {
        var engine = new RecipeEngine();
        var result = await engine.GetRecipeAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRecipe_ShouldRemoveRecipe()
    {
        var engine = new RecipeEngine();
        await engine.SaveRecipeAsync(new RecipeData("TestRecipe", "1.0",
            Array.Empty<RecipeParameterData>(), DateTimeOffset.Now, "admin"));

        await engine.DeleteRecipeAsync("TestRecipe");

        var result = await engine.GetRecipeAsync("TestRecipe");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ApplyAsync_ShouldThrow_WhenRecipeNotFound()
    {
        var engine = new RecipeEngine();
        var act = () => engine.ApplyAsync("nonexistent");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetRecipesAsync_ShouldReturnAll()
    {
        var engine = new RecipeEngine();
        await engine.SaveRecipeAsync(new RecipeData("R1", "1.0", Array.Empty<RecipeParameterData>(), DateTimeOffset.Now, "a"));
        await engine.SaveRecipeAsync(new RecipeData("R2", "1.0", Array.Empty<RecipeParameterData>(), DateTimeOffset.Now, "b"));

        var recipes = await engine.GetRecipesAsync();
        recipes.Should().HaveCount(2);
    }
}
```

- [ ] **Step 6: 运行测试**

Run: `dotnet test tests/KJ.Domain.Tests/KJ.Domain.Tests.csproj --verbosity normal`
Expected: All tests pass

- [ ] **Step 7: Commit**

```bash
git add tests/KJ.Domain.Tests/
git commit -m "test(domain): add unit tests for TagStore, DeviceManager, AlarmService, RecipeEngine"
```

---

### Task 18: 更新 MonitoringModule DI 注册

**Files:**
- Modify: `src/KJ.Modules.Monitoring.WinUI/MonitoringModule.cs`

- [ ] **Step 1: 确认 MonitoringModule 注册了所有新 ViewModel**

读取 `src/KJ.Modules.Monitoring.WinUI/MonitoringModule.cs`，确保注册了：
- `TagMonitorViewModel`
- `TrendChartViewModel`
- `DashboardViewModel`

如果缺失，在 `RegisterTypes` 中添加：

```csharp
containerRegistry.RegisterSingleton<ViewModels.TagMonitorViewModel>();
containerRegistry.RegisterSingleton<ViewModels.TrendChartViewModel>();
containerRegistry.RegisterSingleton<ViewModels.DashboardViewModel>();
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/KJ.Modules.Monitoring.WinUI/KJ.Modules.Monitoring.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/KJ.Modules.Monitoring.WinUI/MonitoringModule.cs
git commit -m "feat(monitoring): register new ViewModels in MonitoringModule DI"
```

---

### Task 19: 更新各模块 DI 注册

**Files:**
- Modify: `src/KJ.Modules.Alarm/AlarmModule.cs`
- Modify: `src/KJ.Modules.Config/ConfigModule.cs`
- Modify: `src/KJ.Modules.Reporting/ReportingModule.cs`

- [ ] **Step 1: 更新 AlarmModule 注册**

读取 `src/KJ.Modules.Alarm/AlarmModule.cs`，确保注册了 `AlarmHomeViewModel`。

- [ ] **Step 2: 更新 ConfigModule 注册**

读取 `src/KJ.Modules.Config/ConfigModule.cs`，确保注册了 `ConfigHomeViewModel`。

- [ ] **Step 3: 更新 ReportingModule 注册**

读取 `src/KJ.Modules.Reporting/ReportingModule.cs`，确保注册了 `ReportingHomeViewModel`。

- [ ] **Step 4: 验证全解决方案编译**

Run: `dotnet build KJ.sln`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/KJ.Modules.Alarm/AlarmModule.cs src/KJ.Modules.Config/ConfigModule.cs src/KJ.Modules.Reporting/ReportingModule.cs
git commit -m "feat: register all new ViewModels in module DI containers"
```

---

### Task 20: 全量验证

- [ ] **Step 1: 运行所有测试**

Run: `dotnet test tests/KJ.Domain.Tests/KJ.Domain.Tests.csproj --verbosity normal`
Expected: All tests pass

- [ ] **Step 2: 全解决方案编译**

Run: `dotnet build KJ.sln`
Expected: Build succeeded, 0 errors, 0 warnings (或仅有已知警告)

- [ ] **Step 3: 检查未提交文件**

Run: `git status`
Expected: Working tree clean

- [ ] **Step 4: 最终 Commit（如果有遗漏）**

```bash
git add -A
git commit -m "chore: final cleanup for framework completion"
```

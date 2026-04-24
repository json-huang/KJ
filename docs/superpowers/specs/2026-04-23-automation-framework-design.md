# KJ 通用自动化设备框架设计文档

## 1. 概述

### 1.1 项目背景

KJ 是一个通用的自动化设备管理框架，旨在为工业自动化设备提供统一的监控、配置、报警和数据管理平台。该框架支持多种设备类型、通信协议，并提供完整的用户权限管理和数据存储功能。

### 1.2 设计目标

- **通用性**：支持多种自动化设备类型（PLC、传感器、仪表、机器人等）
- **可扩展性**：模块化架构，功能可插拔
- **可维护性**：清晰的分层架构，职责明确
- **安全性**：完整的用户认证和权限控制系统
- **可靠性**：完善的错误处理和重试机制

### 1.3 技术栈

| 层次 | 技术选型 | 用途 |
|------|----------|------|
| UI 框架 | WinUI 3 | 现代化 Windows 桌面 UI |
| MVVM 框架 | Prism | 模块化、导航、依赖注入 |
| IoC 容器 | DryIoc | 依赖注入管理 |
| 消息总线 | MassTransit | 进程内/分布式消息通信 |
| ORM | EF Core | 数据库访问和对象映射 |
| 数据库 | SQL Server | 数据持久化存储 |
| 身份认证 | ASP.NET Identity | 用户管理和认证 |
| 令牌 | JWT | 无状态身份验证 |
| 设备通信 | TCP/Modbus/OPC UA | 工业设备通信协议 |
| 日志 | Serilog | 结构化日志记录 |
| 测试 | xUnit + Moq | 单元测试和模拟 |

## 2. 架构设计

### 2.1 整体架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        KJ.App (WinUI 3 Shell)                  │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐           │
│  │   导航框架    │ │   区域管理    │ │   对话框服务  │           │
│  └──────────────┘ └──────────────┘ └──────────────┘           │
├─────────────────────────────────────────────────────────────────┤
│                     业务模块层 (Prism Modules)                  │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐              │
│  │ Auth Module │ │Monitoring   │ │ Config      │              │
│  │ 用户认证模块 │ │ 监控模块     │ │ 配置管理模块 │              │
│  └─────────────┘ └─────────────┘ └─────────────┘              │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐              │
│  │ Alarm Module│ │ Recipe      │ │ Reporting   │              │
│  │ 报警模块     │ │ 配方模块     │ │ 报表模块     │              │
│  └─────────────┘ └─────────────┘ └─────────────┘              │
├─────────────────────────────────────────────────────────────────┤
│                     基础设施层 (Infrastructure)                  │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐              │
│  │ MassTransit │ │ EF Core     │ │ ASP.NET     │              │
│  │ 消息总线     │ │ 数据访问     │ │ Identity    │              │
│  └─────────────┘ └─────────────┘ └─────────────┘              │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐              │
│  │ 设备驱动管理 │ │ 协议解析器   │ │ 日志服务     │              │
│  └─────────────┘ └─────────────┘ └─────────────┘              │
├─────────────────────────────────────────────────────────────────┤
│                     领域核心层 (Domain Core)                    │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐              │
│  │ TagStore    │ │DeviceManager│ │ RecipeEngine│              │
│  │ 标签存储     │ │ 设备管理器   │ │ 配方引擎     │              │
│  └─────────────┘ └─────────────┘ └─────────────┘              │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐              │
│  │ AlarmService│ │ AuditLogger │ │ UserManager │              │
│  │ 报警服务     │ │ 审计日志     │ │ 用户管理     │              │
│  └─────────────┘ └─────────────┘ └─────────────┘              │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 分层职责

- **Shell 层**：WinUI 3 主窗口、导航、区域管理
- **模块层**：各业务功能模块，可独立开发和部署
- **基础设施层**：技术实现（通信、数据库、身份认证）
- **领域核心层**：业务逻辑和领域服务

## 3. 项目结构

```
KJ.sln
├── src/
│   ├── KJ.App/                          # WinUI 3 主应用程序
│   │   ├── KJ.App.csproj
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MainWindow.xaml / MainWindow.xaml.cs
│   │   ├── Regions/                     # 区域定义
│   │   └── Services/                    # Shell 级服务
│   │
│   ├── KJ.Domain/                       # 领域核心层
│   │   ├── KJ.Domain.csproj
│   │   ├── Entities/                    # 领域实体
│   │   ├── ValueObjects/                # 值对象
│   │   ├── Interfaces/                  # 领域接口
│   │   ├── Events/                      # 领域事件
│   │   └── Services/                    # 领域服务
│   │
│   ├── KJ.Infrastructure/               # 基础设施层
│   │   ├── KJ.Infrastructure.csproj
│   │   ├── Data/                        # 数据访问
│   │   ├── Identity/                    # 身份认证
│   │   ├── Messaging/                   # 消息通信
│   │   ├── Drivers/                     # 设备驱动
│   │   ├── Protocols/                   # 协议解析
│   │   └── Logging/                     # 日志服务
│   │
│   ├── KJ.Modules.Auth/                 # 用户认证模块
│   ├── KJ.Modules.Monitoring/           # 监控模块
│   ├── KJ.Modules.Config/               # 配置管理模块
│   ├── KJ.Modules.Alarm/                # 报警模块
│   └── KJ.Modules.Reporting/            # 报表模块
│
├── tests/                               # 测试项目
│   ├── KJ.Domain.Tests/
│   ├── KJ.Infrastructure.Tests/
│   ├── KJ.Modules.Tests/
│   └── KJ.IntegrationTests/
│
└── docs/                                # 文档
```

## 4. 领域模型

### 4.1 核心实体

```csharp
// 设备实体
public class Device
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DeviceType Type { get; set; }
    public DeviceAddress Address { get; set; }
    public ConnectionState State { get; set; }
    public DateTime LastConnected { get; set; }
    public List<Tag> Tags { get; set; }
    public Dictionary<string, string> Properties { get; set; }
}

// 标签实体
public class Tag
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string Name { get; set; }
    public TagDataType DataType { get; set; }
    public string Address { get; set; }
    public object? Value { get; set; }
    public QualityCode Quality { get; set; }
    public DateTime Timestamp { get; set; }
    public TagDirection Direction { get; set; }
}

// 配方实体
public class Recipe
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Version { get; set; }
    public List<RecipeParameter> Parameters { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
}

// 报警实体
public class Alarm
{
    public Guid Id { get; set; }
    public Guid TagId { get; set; }
    public string Name { get; set; }
    public AlarmCondition Condition { get; set; }
    public AlarmLevel Level { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
}
```

### 4.2 核心接口

```csharp
// 设备驱动接口
public interface IDeviceDriver : IAsyncDisposable
{
    string DriverType { get; }
    ConnectionState State { get; }
    event EventHandler<ConnectionState>? StateChanged;
    
    Task ConnectAsync(DeviceAddress address, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task<TagValue> ReadTagAsync(Tag tag, CancellationToken ct = default);
    Task WriteTagAsync(Tag tag, object value, CancellationToken ct = default);
    Task<IReadOnlyList<TagValue>> ReadAllTagsAsync(CancellationToken ct = default);
}

// 协议接口
public interface IProtocol
{
    string ProtocolName { get; }
    ReadOnlyMemory<byte> Encode(object message);
    T Decode<T>(ReadOnlyMemory<byte> data);
    bool CanHandle(string protocolName);
}

// 标签存储接口
public interface ITagStore
{
    event EventHandler<TagValueChanged>? TagChanged;
    
    TagValue? GetTagValue(Guid tagId);
    IReadOnlyList<TagValue> GetDeviceTagValues(Guid deviceId);
    void UpdateTagValue(TagValue value);
    Task SaveHistoryAsync(TagValue value, CancellationToken ct = default);
    Task<IReadOnlyList<TagHistory>> GetHistoryAsync(
        Guid tagId, DateTime start, DateTime end, CancellationToken ct = default);
}

// 报警服务接口
public interface IAlarmService
{
    event EventHandler<AlarmTriggered>? AlarmTriggered;
    event EventHandler<AlarmAcknowledged>? AlarmAcknowledged;
    
    Task CheckAlarmsAsync(TagValue value, CancellationToken ct = default);
    Task AcknowledgeAlarmAsync(Guid alarmId, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<Alarm>> GetActiveAlarmsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Alarm>> GetAlarmHistoryAsync(
        DateTime start, DateTime end, CancellationToken ct = default);
}

// 配方引擎接口
public interface IRecipeEngine
{
    Task<Recipe?> GetRecipeAsync(Guid recipeId, CancellationToken ct = default);
    Task<IReadOnlyList<Recipe>> GetRecipesAsync(CancellationToken ct = default);
    Task SaveRecipeAsync(Recipe recipe, CancellationToken ct = default);
    Task ApplyRecipeAsync(Guid recipeId, Guid deviceId, CancellationToken ct = default);
    Task<RecipeValidationResult> ValidateRecipeAsync(Recipe recipe, CancellationToken ct = default);
}
```

### 4.3 领域事件

```csharp
public record TagValueChanged(Guid TagId, object? OldValue, object? NewValue, DateTime Timestamp);
public record AlarmTriggered(Guid AlarmId, Guid TagId, AlarmLevel Level, string Message, DateTime Timestamp);
public record AlarmAcknowledged(Guid AlarmId, string UserId, DateTime Timestamp);
public record DeviceStateChanged(Guid DeviceId, ConnectionState OldState, ConnectionState NewState, DateTime Timestamp);
```

## 5. 基础设施层

### 5.1 数据库设计 (EF Core + SQL Server)

```csharp
// DbContext
public class KJDbContext : DbContext
{
    public DbSet<Device> Devices { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<TagHistory> TagHistory { get; set; }
    public DbSet<Recipe> Recipes { get; set; }
    public DbSet<RecipeParameter> RecipeParameters { get; set; }
    public DbSet<Alarm> Alarms { get; set; }
    public DbSet<AlarmHistory> AlarmHistory { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 设备配置
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasMany(e => e.Tags).WithOne().HasForeignKey(t => t.DeviceId);
            entity.OwnsOne(e => e.Address);
        });
        
        // 标签配置
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => new { e.DeviceId, e.Name }).IsUnique();
        });
        
        // 标签历史（时序数据）
        modelBuilder.Entity<TagHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TagId, e.Timestamp });
            entity.Property(e => e.Timestamp).HasColumnType("datetime2(3)");
        });
        
        // 报警配置
        modelBuilder.Entity<Alarm>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<Tag>().WithMany().HasForeignKey(e => e.TagId);
        });
        
        // 用户配置
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.HasMany(e => e.Roles).WithMany(r => r.Users);
        });
        
        // 审计日志
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Timestamp).HasColumnType("datetime2(3)");
        });
    }
}
```

### 5.2 身份认证系统

```csharp
// 用户管理服务
public interface IUserManager
{
    Task<User?> GetUserAsync(Guid userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<IReadOnlyList<User>> GetUsersAsync();
    Task<User> CreateUserAsync(User user, string password);
    Task UpdateUserAsync(User user);
    Task DeleteUserAsync(Guid userId);
    Task<bool> CheckPasswordAsync(User user, string password);
    Task ChangePasswordAsync(User user, string oldPassword, string newPassword);
}

// 角色权限服务
public interface IRoleManager
{
    Task<Role?> GetRoleAsync(Guid roleId);
    Task<IReadOnlyList<Role>> GetRolesAsync();
    Task<Role> CreateRoleAsync(Role role);
    Task UpdateRoleAsync(Role role);
    Task DeleteRoleAsync(Guid roleId);
    Task<bool> HasPermissionAsync(Guid userId, string permission);
    Task GrantPermissionAsync(Guid roleId, string permission);
    Task RevokePermissionAsync(Guid roleId, string permission);
}

// JWT 令牌服务
public interface ITokenService
{
    Task<TokenResult> GenerateTokenAsync(User user);
    Task<TokenValidationResult> ValidateTokenAsync(string token);
    Task RevokeTokenAsync(string token);
    Task<bool> IsTokenRevokedAsync(string token);
}

// 权限定义
public static class Permissions
{
    // 设备管理
    public const string DeviceView = "device:view";
    public const string DeviceConfigure = "device:configure";
    public const string DeviceControl = "device:control";
    
    // 标签管理
    public const string TagView = "tag:view";
    public const string TagWrite = "tag:write";
    public const string TagConfigure = "tag:configure";
    
    // 报警管理
    public const string AlarmView = "alarm:view";
    public const string AlarmAcknowledge = "alarm:acknowledge";
    public const string AlarmConfigure = "alarm:configure";
    
    // 配方管理
    public const string RecipeView = "recipe:view";
    public const string RecipeEdit = "recipe:edit";
    public const string RecipeApply = "recipe:apply";
    
    // 用户管理
    public const string UserView = "user:view";
    public const string UserManage = "user:manage";
    public const string RoleManage = "role:manage";
    
    // 系统管理
    public const string SystemConfigure = "system:configure";
    public const string AuditView = "audit:view";
}
```

### 5.3 消息通信 (MassTransit)

```csharp
// 消息定义
public record TagValueChangedMessage(Guid TagId, object? Value, DateTime Timestamp);
public record AlarmTriggeredMessage(Guid AlarmId, Guid TagId, AlarmLevel Level, string Message);
public record DeviceStateChangedMessage(Guid DeviceId, ConnectionState State);
public record RecipeAppliedMessage(Guid RecipeId, Guid DeviceId, string UserId);

// MassTransit 配置
public static class MassTransitConfig
{
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddMassTransit(x =>
        {
            // 消费者注册
            x.AddConsumer<TagValueChangedConsumer>();
            x.AddConsumer<AlarmTriggeredConsumer>();
            x.AddConsumer<DeviceStateChangedConsumer>();
            
            // 使用内存传输（初期）
            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        });
        
        return services;
    }
}

// 消费者示例
public class TagValueChangedConsumer : IConsumer<TagValueChangedMessage>
{
    private readonly ITagStore _tagStore;
    private readonly IAlarmService _alarmService;
    
    public TagValueChangedConsumer(ITagStore tagStore, IAlarmService alarmService)
    {
        _tagStore = tagStore;
        _alarmService = alarmService;
    }
    
    public async Task Consume(ConsumeContext<TagValueChangedMessage> context)
    {
        var message = context.Message;
        
        // 更新标签存储
        _tagStore.UpdateTagValue(new TagValue
        {
            TagId = message.TagId,
            Value = message.Value,
            Timestamp = message.Timestamp
        });
        
        // 检查报警
        await _alarmService.CheckAlarmsAsync(new TagValue
        {
            TagId = message.TagId,
            Value = message.Value,
            Timestamp = message.Timestamp
        });
    }
}
```

### 5.4 设备驱动管理

```csharp
// 驱动工厂
public interface IDeviceDriverFactory
{
    IDeviceDriver CreateDriver(string driverType);
    IReadOnlyList<string> GetSupportedDrivers();
}

public class DeviceDriverFactory : IDeviceDriverFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _driverTypes;
    
    public DeviceDriverFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _driverTypes = new Dictionary<string, Type>
        {
            ["Tcp"] = typeof(TcpDeviceDriver),
            ["ModbusTcp"] = typeof(ModbusTcpDriver),
            ["ModbusRtu"] = typeof(ModbusRtuDriver),
            ["OpcUa"] = typeof(OpcUaDriver)
        };
    }
    
    public IDeviceDriver CreateDriver(string driverType)
    {
        if (!_driverTypes.TryGetValue(driverType, out var type))
            throw new ArgumentException($"Unsupported driver type: {driverType}");
            
        return (IDeviceDriver)_serviceProvider.GetRequiredService(type);
    }
    
    public IReadOnlyList<string> GetSupportedDrivers() => _driverTypes.Keys.ToList();
}
```

## 6. 模块设计

### 6.1 模块基类

```csharp
// 模块基类
public abstract class ModuleBase : IModule
{
    protected IContainerProvider ContainerProvider { get; private set; }
    protected IRegionManager RegionManager { get; private set; }
    
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        RegisterServices(containerRegistry);
        RegisterViews(containerRegistry);
    }
    
    public void OnInitialized(IContainerProvider containerProvider)
    {
        ContainerProvider = containerProvider;
        RegionManager = containerProvider.Resolve<IRegionManager>();
        
        RegisterRegions();
        InitializeModule();
    }
    
    protected abstract void RegisterServices(IContainerRegistry containerRegistry);
    protected abstract void RegisterViews(IContainerRegistry containerRegistry);
    protected abstract void RegisterRegions();
    protected abstract void InitializeModule();
}
```

### 6.2 认证模块

```csharp
public class AuthModule : ModuleBase
{
    protected override void RegisterServices(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IAuthService, AuthService>();
        containerRegistry.RegisterSingleton<IUserManager, UserManager>();
        containerRegistry.RegisterSingleton<IRoleManager, RoleManager>();
        containerRegistry.RegisterSingleton<ITokenService, JwtTokenService>();
    }
    
    protected override void RegisterViews(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<LoginView, LoginViewModel>();
        containerRegistry.RegisterForNavigation<UserManagementView, UserManagementViewModel>();
        containerRegistry.RegisterForNavigation<RoleManagementView, RoleManagementViewModel>();
    }
    
    protected override void RegisterRegions()
    {
        RegionManager.RegisterViewWithRegion(Regions.MainNavigation, () =>
            ContainerProvider.Resolve<NavigationView>());
    }
    
    protected override void InitializeModule()
    {
        // 检查登录状态
        var authService = ContainerProvider.Resolve<IAuthService>();
        if (!authService.IsLoggedIn)
        {
            RegionManager.RequestNavigate(Regions.MainContent, nameof(LoginView));
        }
    }
}
```

### 6.3 监控模块

```csharp
public class MonitoringModule : ModuleBase
{
    protected override void RegisterServices(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IDeviceManager, DeviceManager>();
        containerRegistry.RegisterSingleton<ITagMonitorService, TagMonitorService>();
    }
    
    protected override void RegisterViews(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<DeviceListView, DeviceListViewModel>();
        containerRegistry.RegisterForNavigation<TagMonitorView, TagMonitorViewModel>();
        containerRegistry.RegisterForNavigation<TrendChartView, TrendChartViewModel>();
        containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>();
    }
    
    protected override void RegisterRegions()
    {
        RegionManager.RegisterViewWithRegion(Regions.MainNavigation, () =>
            ContainerProvider.Resolve<MonitoringNavigationView>());
    }
}
```

## 7. 错误处理与测试

### 7.1 错误处理架构

```csharp
// 全局异常处理
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IEventAggregator _eventAggregator;
    
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception occurred");
        
        // 发布异常事件
        await _eventAggregator.PublishAsync(new ExceptionOccurred(exception));
        
        // 返回适当的错误响应
        httpContext.Response.StatusCode = exception switch
        {
            UnauthorizedAccessException => 401,
            ForbiddenException => 403,
            NotFoundException => 404,
            ValidationException => 400,
            _ => 500
        };
        
        return true;
    }
}

// 重试策略
public static class RetryPolicies
{
    public static IAsyncPolicy<T> CreateDeviceRetryPolicy<T>()
    {
        return Policy<T>
            .Handle<DeviceCommunicationException>()
            .Or<SocketException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, delay, attempt, context) =>
                {
                    // 记录重试日志
                });
    }
    
    public static IAsyncPolicy CreateDatabaseRetryPolicy()
    {
        return Policy
            .Handle<SqlException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
    }
}

// 熔断器策略
public static class CircuitBreakerPolicies
{
    public static IAsyncPolicy CreateDeviceCircuitBreakerPolicy()
    {
        return Policy
            .Handle<DeviceCommunicationException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}
```

### 7.2 测试策略

```
测试金字塔：
┌─────────────────────────────────────────┐
│           集成测试 (20%)                 │
│  测试模块间交互、数据库、消息总线        │
├─────────────────────────────────────────┤
│           组件测试 (30%)                 │
│  测试单个模块、服务、ViewModel          │
├─────────────────────────────────────────┤
│           单元测试 (50%)                 │
│  测试领域逻辑、工具类、纯函数           │
└─────────────────────────────────────────┘
```

```csharp
// 单元测试示例
public class TagStoreTests
{
    [Fact]
    public void Upsert_ShouldUpdateValueAndRaiseEvent()
    {
        // Arrange
        var tagStore = new TagStore();
        var tagId = new TagId("TestTag");
        var value = new TagValue(tagId, 42, DateTimeOffset.Now);
        
        TagValue? receivedValue = null;
        tagStore.TagChanged += (_, e) => receivedValue = e;
        
        // Act
        tagStore.Upsert(value);
        
        // Assert
        Assert.True(tagStore.TryGet(tagId, out var storedValue));
        Assert.Equal(42, storedValue.Value);
        Assert.NotNull(receivedValue);
    }
}
```

## 8. 部署与配置

### 8.1 配置管理

```json
{
  "Database": {
    "ConnectionString": "Server=localhost;Database=KJ;Trusted_Connection=True;TrustServerCertificate=True;",
    "CommandTimeout": 30,
    "MaxRetryCount": 3
  },
  "Identity": {
    "Jwt": {
      "Secret": "your-secret-key-here",
      "Issuer": "KJ.App",
      "Audience": "KJ.Client",
      "ExpirationMinutes": 60,
      "RefreshExpirationDays": 7
    },
    "Password": {
      "RequiredLength": 8,
      "RequireDigit": true,
      "RequireLowercase": true,
      "RequireUppercase": true,
      "RequireNonAlphanumeric": true
    }
  },
  "Messaging": {
    "Provider": "InMemory",
    "RabbitMQ": {
      "Host": "localhost",
      "Username": "guest",
      "Password": "guest"
    }
  },
  "Devices": {
    "DefaultPollingInterval": 1000,
    "ConnectionTimeout": 5000,
    "MaxRetryCount": 3,
    "Drivers": {
      "Tcp": {
        "DefaultPort": 502,
        "BufferSize": 4096
      },
      "ModbusTcp": {
        "DefaultPort": 502,
        "UnitId": 1
      },
      "OpcUa": {
        "DefaultPort": 4840,
        "SecurityMode": "SignAndEncrypt"
      }
    }
  },
  "Alarms": {
    "CheckInterval": 1000,
    "MaxActiveAlarms": 100,
    "NotificationEnabled": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "KJ": "Debug"
    }
  }
}
```

### 8.2 依赖注入配置

```csharp
// App.xaml.cs
public sealed partial class App : PrismApplication
{
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 配置
        containerRegistry.RegisterInstance(Configuration.GetSection("Database").Get<DatabaseConfig>());
        containerRegistry.RegisterInstance(Configuration.GetSection("Identity").Get<IdentityConfig>());
        containerRegistry.RegisterInstance(Configuration.GetSection("Messaging").Get<MessagingConfig>());
        containerRegistry.RegisterInstance(Configuration.GetSection("Devices").Get<DevicesConfig>());
        
        // 数据库
        containerRegistry.RegisterDbContext<KJDbContext>();
        containerRegistry.RegisterScoped(typeof(IRepository<>), typeof(Repository<>));
        
        // 身份认证
        containerRegistry.RegisterSingleton<IUserManager, UserManager>();
        containerRegistry.RegisterSingleton<IRoleManager, RoleManager>();
        containerRegistry.RegisterSingleton<ITokenService, JwtTokenService>();
        containerRegistry.RegisterSingleton<IAuthService, AuthService>();
        
        // 消息通信
        containerRegistry.AddMessaging();
        
        // 设备驱动
        containerRegistry.RegisterSingleton<IDeviceDriverFactory, DeviceDriverFactory>();
        containerRegistry.Register<TcpDeviceDriver>();
        containerRegistry.Register<ModbusTcpDriver>();
        containerRegistry.Register<OpcUaDriver>();
        
        // 领域服务
        containerRegistry.RegisterSingleton<ITagStore, TagStore>();
        containerRegistry.RegisterSingleton<IAlarmService, AlarmService>();
        containerRegistry.RegisterSingleton<IRecipeEngine, RecipeEngine>();
        containerRegistry.RegisterSingleton<IDeviceManager, DeviceManager>();
        
        // 日志
        containerRegistry.RegisterSingleton<IAuditLogger, AuditLogger>();
    }
    
    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<AuthModule>();
        moduleCatalog.AddModule<MonitoringModule>();
        moduleCatalog.AddModule<ConfigModule>();
        moduleCatalog.AddModule<AlarmModule>();
        moduleCatalog.AddModule<ReportingModule>();
    }
}
```

## 9. 实施路线图

### 9.1 分阶段实施计划

```
阶段 1：基础架构 (2-3 周)
├── 搭建项目结构
├── 实现领域核心层
├── 配置 EF Core + SQL Server
├── 实现基础身份认证
└── 搭建 WinUI 3 Shell

阶段 2：通信层 (2-3 周)
├── 实现设备驱动框架
├── 实现 TCP/Modbus 驱动
├── 配置 MassTransit 消息总线
├── 实现标签存储和事件
└── 实现基础监控界面

阶段 3：业务功能 (3-4 周)
├── 实现设备管理模块
├── 实现报警系统
├── 实现配方管理
├── 实现数据报表
└── 完善用户权限系统

阶段 4：高级功能 (2-3 周)
├── 实现 OPC UA 驱动
├── 实现趋势图表
├── 实现数据导出
├── 性能优化
└── 完善错误处理

阶段 5：测试与部署 (2 周)
├── 单元测试
├── 集成测试
├── 性能测试
├── 打包安装程序
└── 编写文档
```

### 9.2 关键依赖包

```xml
<!-- KJ.App.csproj -->
<ItemGroup>
    <!-- WinUI 3 -->
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.5.*" />
    
    <!-- Prism -->
    <PackageReference Include="Prism.DryIoc" Version="9.0.*" />
    <PackageReference Include="Prism.WinUI" Version="9.0.*" />
    
    <!-- 数据库 -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.*" />
    
    <!-- 身份认证 -->
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.*" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.*" />
    
    <!-- 消息通信 -->
    <PackageReference Include="MassTransit" Version="8.0.*" />
    <PackageReference Include="MassTransit.InMemory" Version="8.0.*" />
    <PackageReference Include="MassTransit.RabbitMQ" Version="8.0.*" />
    
    <!-- 设备通信 -->
    <PackageReference Include="NModbus4" Version="2.1.*" />
    <PackageReference Include="OPCFoundation.NetStandard.Opc.Ua" Version="1.4.*" />
    
    <!-- 日志 -->
    <PackageReference Include="Serilog" Version="3.0.*" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.*" />
    
    <!-- 工具 -->
    <PackageReference Include="Polly" Version="8.0.*" />
    <PackageReference Include="AutoMapper" Version="12.0.*" />
    <PackageReference Include="FluentValidation" Version="11.0.*" />
</ItemGroup>
```

## 10. 总结

KJ 通用自动化设备框架采用分层架构设计，具有以下特点：

1. **模块化设计**：基于 Prism 框架，功能模块可独立开发和部署
2. **消息驱动**：使用 MassTransit 实现进程内/分布式消息通信
3. **领域驱动**：清晰的领域模型和业务逻辑分离
4. **安全可靠**：完整的用户认证、权限控制和错误处理机制
5. **可扩展性**：支持多种设备类型和通信协议

该框架为工业自动化设备管理提供了完整的解决方案，可满足从单机到分布式部署的各种需求。

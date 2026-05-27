using KJ.Domain;
using KJ.Domain.Services;
using KJ.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KJ.Infrastructure.Data;

public sealed class DashboardDemoDataEnsurer : IDashboardDemoDataEnsurer
{
    public static readonly Guid DemoDeviceLineA = Guid.Parse("a1000001-0001-4001-8001-000000000001");
    public static readonly Guid DemoDeviceMixer = Guid.Parse("a1000001-0001-4001-8001-000000000002");
    public static readonly Guid DemoDeviceSensor = Guid.Parse("a1000001-0001-4001-8001-000000000003");
    public static readonly Guid DemoDeviceRobot = Guid.Parse("a1000001-0001-4001-8001-000000000004");
    public static readonly Guid DemoDeviceOpc = Guid.Parse("a1000001-0001-4001-8001-000000000005");

    private readonly IDbContextFactory<KjDbContext> _dbFactory;
    private readonly DeviceManager _deviceManager;
    private readonly AlarmService _alarmService;
    private readonly ITagStore _tagStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DashboardDemoDataEnsurer> _logger;

    public DashboardDemoDataEnsurer(
        IDbContextFactory<KjDbContext> dbFactory,
        DeviceManager deviceManager,
        AlarmService alarmService,
        ITagStore tagStore,
        IConfiguration configuration,
        ILogger<DashboardDemoDataEnsurer> logger)
    {
        _dbFactory = dbFactory;
        _deviceManager = deviceManager;
        _alarmService = alarmService;
        _tagStore = tagStore;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled())
            return;

        EnsureInMemoryDevices();
        SeedTagValues();
        _alarmService.EnsureDemoActiveAlarmsIfEmpty();

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await EnsureDatabaseDevicesAsync(db, cancellationToken).ConfigureAwait(false);
            await EnsureAuditLogsAsync(db, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard demo DB sync skipped; in-memory demo data is still available.");
        }
    }

    private bool IsEnabled()
    {
        var value = _configuration["Seed:DashboardDemo"];
        return string.IsNullOrWhiteSpace(value) ||
               !bool.TryParse(value, out var enabled) ||
               enabled;
    }

    private static IReadOnlyList<DeviceDescriptor> GetDemoDescriptors() =>
    [
        new(DemoDeviceLineA.ToString(), "1号线 PLC", "Plc", "Connected", "10.0.0.12", 502),
        new(DemoDeviceMixer.ToString(), "混合罐-02", "Robot", "Connected", "10.0.0.21", 9000),
        new(DemoDeviceSensor.ToString(), "车间温湿度站", "Sensor", "Connected", "10.0.0.30", 1883),
        new(DemoDeviceRobot.ToString(), "包装机器人", "Robot", "Connecting", "10.0.0.45", 44818),
        new(DemoDeviceOpc.ToString(), "OPC 网关", "Other", "Faulted", "10.0.0.50", 4840),
    ];

    private void EnsureInMemoryDevices()
    {
        foreach (var descriptor in GetDemoDescriptors())
        {
            if (_deviceManager.GetDevice(descriptor.DeviceId) is null)
            {
                try
                {
                    _deviceManager.AddDevice(descriptor);
                }
                catch (InvalidOperationException)
                {
                    _deviceManager.UpdateDeviceState(descriptor.DeviceId, descriptor.State);
                }
            }
            else
            {
                _deviceManager.UpdateDeviceState(descriptor.DeviceId, descriptor.State);
            }
        }
    }

    private void SeedTagValues()
    {
        var now = DateTimeOffset.UtcNow;
        UpsertTag("LineA.Temp", 86.4, now);
        UpsertTag("LineA.Speed", 1240, now);
        UpsertTag("Mixer02.Pressure", 1.82, now);
        UpsertTag("Mixer02.Level", 68.5, now);
        UpsertTag("Shop.Humidity", 55.2, now);
        UpsertTag("Shop.Temperature", 23.1, now);
        UpsertTag("PackRobot.Cycle", 42, now);
        UpsertTag("OpcGateway.Link", 0, now);
        UpsertTag("Heartbeat", now.ToString("HH:mm:ss"), now);
    }

    private void UpsertTag(string tagKey, object? value, DateTimeOffset timestamp)
    {
        _tagStore.Upsert(new TagValue(
            new TagId(tagKey),
            value,
            TagQuality.Good,
            timestamp));
    }

    private async Task EnsureDatabaseDevicesAsync(KjDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Devices.AnyAsync(d => d.Id == DemoDeviceLineA, cancellationToken).ConfigureAwait(false))
            return;

        var now = DateTime.UtcNow;
        db.Devices.AddRange(
        [
            CreateEntity(DemoDeviceLineA, "1号线 PLC", DeviceType.Plc, ConnectionState.Connected, "10.0.0.12", 502, now.AddMinutes(-3)),
            CreateEntity(DemoDeviceMixer, "混合罐-02", DeviceType.Robot, ConnectionState.Connected, "10.0.0.21", 9000, now.AddMinutes(-8)),
            CreateEntity(DemoDeviceSensor, "车间温湿度站", DeviceType.Sensor, ConnectionState.Connected, "10.0.0.30", 1883, now.AddMinutes(-1)),
            CreateEntity(DemoDeviceRobot, "包装机器人", DeviceType.Robot, ConnectionState.Connecting, "10.0.0.45", 44818, now.AddMinutes(-25)),
            CreateEntity(DemoDeviceOpc, "OPC 网关", DeviceType.Other, ConnectionState.Faulted, "10.0.0.50", 4840, now.AddHours(-2)),
        ]);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Dashboard demo devices persisted to database.");
    }

    private static Device CreateEntity(
        Guid id,
        string name,
        DeviceType type,
        ConnectionState state,
        string host,
        int port,
        DateTime lastConnected) =>
        new()
        {
            Id = id,
            Name = name,
            Description = "Dashboard 演示设备",
            Type = type,
            State = state,
            LastConnected = lastConnected,
            Address = new DeviceAddress { Host = host, Port = port },
            PropertiesJson = "{}",
        };

    private async Task EnsureAuditLogsAsync(KjDbContext db, CancellationToken cancellationToken)
    {
        if (await db.AuditLogs.AnyAsync(l => l.Action == "流程启动", cancellationToken).ConfigureAwait(false))
            return;

        var now = DateTime.UtcNow;
        var entries = new[]
        {
            ("admin@local", "用户登录", "管理员登录成功"),
            ("admin@local", "设备连接", "1号线 PLC 已连接 10.0.0.12:502"),
            ("admin@local", "告警触发", "1号线反应釜温度超过设定上限"),
            ("system", "Tag 更新", "Mixer02.Pressure = 1.82 bar"),
            ("admin@local", "流程启动", "批次 #2026-0426-A 开始执行"),
            ("admin@local", "插件事件", "示例 WinForms 插件推送 test-info"),
            ("system", "设备离线", "OPC 网关通讯失败，进入 Faulted"),
            ("admin@local", "配置变更", "混合罐-02 轮询间隔调整为 500ms"),
        };

        for (var i = 0; i < entries.Length; i++)
        {
            var (userId, action, details) = entries[i];
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = now.AddMinutes(-(i + 1) * 17),
                UserId = userId,
                Action = action,
                Details = details,
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

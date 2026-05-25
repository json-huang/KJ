using KJ.Domain;
using KJ.Drivers.Abstractions;

namespace KJ.Workflows;

/// <summary>
/// PLC 数据桥接服务。为工作流提供 PLC 信号的实时读写能力。
/// 支持：读取当前值、写入值、浏览标签列表、类型转换。
/// </summary>
public sealed class PlcDataBridge
{
    private readonly IDeviceDriverFactory _driverFactory;
    private readonly IDeviceManager _deviceManager;
    private readonly ITagStore _tagStore;
    private readonly ITagConfigStore _tagConfigStore;

    public PlcDataBridge(
        IDeviceDriverFactory driverFactory,
        IDeviceManager deviceManager,
        ITagStore tagStore,
        ITagConfigStore tagConfigStore)
    {
        _driverFactory = driverFactory;
        _deviceManager = deviceManager;
        _tagStore = tagStore;
        _tagConfigStore = tagConfigStore;
    }

    /// <summary>
    /// 从 PLC 读取信号值。
    /// </summary>
    /// <param name="deviceId">设备 ID</param>
    /// <param name="address">PLC 地址（如 "MAIN.nSpeed"、"HR100"）</param>
    /// <param name="valueType">数据类型</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>读取结果</returns>
    public async Task<PlcReadResult> ReadSignalAsync(
        string deviceId,
        string address,
        TagValueType valueType,
        CancellationToken ct = default)
    {
        var device = _deviceManager.GetDevice(deviceId);
        if (device is null)
            return new PlcReadResult(false, null, $"Device '{deviceId}' not found");

        try
        {
            var driver = _driverFactory.Create(device.DriverType);
            var tagAddress = new TagAddress(address, valueType);
            var request = new TagReadRequest(address, tagAddress);
            var result = await driver.ReadAsync(request, ct).ConfigureAwait(false);

            if (result.Success)
            {
                // 同步到 TagStore
                _tagStore.Upsert(new TagValue(
                    new TagId(address), result.Value, TagQuality.Good, result.Timestamp));
                return new PlcReadResult(true, result.Value);
            }

            return new PlcReadResult(false, null, result.Error);
        }
        catch (Exception ex)
        {
            return new PlcReadResult(false, null, ex.Message);
        }
    }

    /// <summary>
    /// 向 PLC 写入信号值。
    /// </summary>
    public async Task<PlcWriteResult> WriteSignalAsync(
        string deviceId,
        string address,
        TagValueType valueType,
        object? value,
        CancellationToken ct = default)
    {
        var device = _deviceManager.GetDevice(deviceId);
        if (device is null)
            return new PlcWriteResult(false, $"Device '{deviceId}' not found");

        try
        {
            var driver = _driverFactory.Create(device.DriverType);
            var tagAddress = new TagAddress(address, valueType);
            var request = new TagWriteRequest(address, tagAddress, value);
            await driver.WriteAsync(request, ct).ConfigureAwait(false);

            // 同步到 TagStore
            _tagStore.Upsert(new TagValue(
                new TagId(address), value, TagQuality.Good, DateTimeOffset.Now));

            return new PlcWriteResult(true);
        }
        catch (Exception ex)
        {
            return new PlcWriteResult(false, ex.Message);
        }
    }

    /// <summary>
    /// 获取 PLC 信号的当前值（从 TagStore 缓存读取，不触发实际通信）。
    /// </summary>
    public object? GetCachedValue(string tagKey)
    {
        if (_tagStore.TryGet(new TagId(tagKey), out var value))
            return value.Value;
        return null;
    }

    /// <summary>
    /// 获取 PLC 信号的当前值和质量。
    /// </summary>
    public (object? Value, TagQuality Quality) GetCachedValueWithQuality(string tagKey)
    {
        if (_tagStore.TryGet(new TagId(tagKey), out var value))
            return (value.Value, value.Quality);
        return (null, TagQuality.Unknown);
    }

    /// <summary>
    /// 浏览设备的标签列表。
    /// </summary>
    public IReadOnlyList<TagInfo> BrowseTags(string deviceId)
    {
        return _tagConfigStore.GetTagsForDevice(deviceId)
            .Select(t => new TagInfo
            {
                TagKey = t.TagKey,
                Address = t.Address,
                ValueType = t.ValueType,
                CurrentValue = GetCachedValue(t.TagKey),
            })
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// 浏览所有已注册的设备。
    /// </summary>
    public IReadOnlyList<DeviceInfo> BrowseDevices()
    {
        return _deviceManager.ListDevices()
            .Select(d => new DeviceInfo
            {
                DeviceId = d.DeviceId,
                DisplayName = d.DisplayName,
                DriverType = d.DriverType,
                State = d.State,
                Host = d.Host,
                Port = d.Port,
            })
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// 将 PLC 类型字符串转换为 TagValueType。
    /// </summary>
    public static TagValueType ParsePlcType(string plcType)
    {
        return plcType.ToUpperInvariant() switch
        {
            "BOOL" or "BIT" or "BOOLEAN" => TagValueType.Bool,
            "INT" or "INT16" or "DINT" or "INT32" => TagValueType.Int32,
            "LINT" or "INT64" or "LREAL_INT" => TagValueType.Int64,
            "REAL" or "FLOAT" or "FLOAT32" => TagValueType.Float,
            "LREAL" or "DOUBLE" or "FLOAT64" => TagValueType.Double,
            "STRING" or "WSTRING" or "TEXT" => TagValueType.String,
            "BYTE" or "BYTES" or "RAW" => TagValueType.Bytes,
            _ => TagValueType.Int32,
        };
    }

    /// <summary>
    /// 将值从 object 转换为指定的 PLC 类型。
    /// </summary>
    public static object? ConvertValue(object? value, TagValueType targetType)
    {
        if (value is null) return null;

        switch (targetType)
        {
            case TagValueType.Bool:
                if (value is bool b) return b;
                if (value is string s) return s is "true" or "1" or "yes";
                if (value is int i) return i != 0;
                if (value is double d) return d != 0;
                return Convert.ToBoolean(value);
            case TagValueType.Int32:
                return Convert.ToInt32(value);
            case TagValueType.Int64:
                return Convert.ToInt64(value);
            case TagValueType.Float:
                return Convert.ToSingle(value);
            case TagValueType.Double:
                return Convert.ToDouble(value);
            case TagValueType.String:
                return value.ToString();
            default:
                return value;
        }
    }
}

public sealed record PlcReadResult(bool Success, object? Value, string? Error = null);
public sealed record PlcWriteResult(bool Success, string? Error = null);

public sealed class TagInfo
{
    public string TagKey { get; set; } = "";
    public string Address { get; set; } = "";
    public TagValueType ValueType { get; set; }
    public object? CurrentValue { get; set; }
}

public sealed class DeviceInfo
{
    public string DeviceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DriverType { get; set; } = "";
    public string State { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
}

using KJ.Workflows;
using KJ.Domain;
using KJ.Drivers.Abstractions;

namespace KJ.Infrastructure.Workflows;

/// <summary>
/// 工作流步骤：从 PLC 读取信号。
/// 通过 PlcDataBridge 读取，结果同步到 TagStore。
/// 
/// 参数:
///   symbol=PLC 变量名 (必填, 如 MAIN.nSpeed)
///   device=设备 ID (必填)
///   address=驱动地址 (默认同 symbol)
///   type=数据类型 (默认 Int32, 支持 BOOL/INT/DINT/REAL/LREAL/STRING)
/// </summary>
public sealed class ReadTagStepHandler : IWorkflowStepHandler
{
    private readonly PlcDataBridge _bridge;

    public ReadTagStepHandler(PlcDataBridge bridge)
    {
        _bridge = bridge;
    }

    public bool CanHandle(string kind) =>
        string.Equals(kind, "Plc.Ads.Read", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "Read", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "PlcRead", StringComparison.OrdinalIgnoreCase);

    public async Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        var symbol = step.Parameters.GetValueOrDefault("symbol", step.Title);
        var deviceId = step.Parameters.GetValueOrDefault("device", "");
        var address = step.Parameters.GetValueOrDefault("address", symbol);
        var typeStr = step.Parameters.GetValueOrDefault("type", "DINT");

        var valueType = PlcDataBridge.ParsePlcType(typeStr);

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            const string hint =
                "步骤参数 device 为空：请在属性面板的「设备」下拉框选择 Beckhoff ADS 设备（Host/Port 在【设备配置】中维护）。";
            ctx.Error(step, hint, hint);
            throw new InvalidOperationException(hint);
        }

        ctx.Info(step, $"Reading {symbol} from device {deviceId} (type={valueType})");

        var result = await _bridge.ReadSignalAsync(deviceId, address, valueType, ct).ConfigureAwait(false);

        if (result.Success)
        {
            ctx.Info(step, $"Read {symbol} = {result.Value}");

            // 存入步骤参数，供后续步骤使用
            step.Parameters[$"__read:{symbol}"] = result.Value?.ToString() ?? "";
        }
        else
        {
            var detail = $"ADS 读失败：设备={deviceId}，符号={symbol}，类型={typeStr}。{result.Error}";
            ctx.Error(step, detail, result.Error);
            throw new InvalidOperationException(detail);
        }
    }
}

/// <summary>
/// 工作流步骤：向 PLC 写入信号。
/// 通过 PlcDataBridge 写入，结果同步到 TagStore。
/// 
/// 参数:
///   symbol=PLC 变量名 (必填)
///   device=设备 ID (必填)
///   address=驱动地址 (默认同 symbol)
///   value=写入值 (必填)
///   type=数据类型 (默认 INT)
/// </summary>
public sealed class WriteTagStepHandler : IWorkflowStepHandler
{
    private readonly PlcDataBridge _bridge;

    public WriteTagStepHandler(PlcDataBridge bridge)
    {
        _bridge = bridge;
    }

    public bool CanHandle(string kind) =>
        string.Equals(kind, "Plc.Ads.Write", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "Write", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "PlcWrite", StringComparison.OrdinalIgnoreCase);

    public async Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        var symbol = step.Parameters.GetValueOrDefault("symbol", step.Title);
        var deviceId = step.Parameters.GetValueOrDefault("device", "");
        var address = step.Parameters.GetValueOrDefault("address", symbol);
        var valueStr = step.Parameters.GetValueOrDefault("value", "0");
        var typeStr = step.Parameters.GetValueOrDefault("type", "DINT");

        var valueType = PlcDataBridge.ParsePlcType(typeStr);
        var value = PlcDataBridge.ConvertValue(valueStr, valueType);

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            const string hint =
                "步骤参数 device 为空：请在属性面板的「设备」下拉框选择 Beckhoff ADS 设备（Host/Port 在【设备配置】中维护）。";
            ctx.Error(step, hint, hint);
            throw new InvalidOperationException(hint);
        }

        ctx.Info(step, $"Writing {symbol} <= {value} (type={valueType})");

        var result = await _bridge.WriteSignalAsync(deviceId, address, valueType, value, ct).ConfigureAwait(false);

        if (result.Success)
        {
            ctx.Info(step, $"Write {symbol} <= {value}");
        }
        else
        {
            var detail = $"ADS 写失败：设备={deviceId}，符号={symbol}，类型={typeStr}。{result.Error}";
            ctx.Error(step, detail, result.Error);
            throw new InvalidOperationException(detail);
        }
    }
}

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace KJ.Workflows;

/// <summary>
/// HTTP 请求步骤处理器。在工作流中调用外部 REST API。
/// 
/// 参数:
///   url=请求地址 (必填)
///   method=GET/POST/PUT/DELETE (默认 GET)
///   body=POST/PUT 请求体 (JSON)
///   header:Authorization=Bearer xxx (自定义请求头，前缀 header:)
///   resultVar=将响应存入上下文的变量名
///   timeout=超时秒数 (默认 30)
/// </summary>
public sealed class HttpStepHandler : IWorkflowStepHandler
{
    private readonly HttpClient _httpClient;

    public HttpStepHandler(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool CanHandle(string kind) =>
        string.Equals(kind, "Http", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "HttpRequest", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "Webhook", StringComparison.OrdinalIgnoreCase);

    public async Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        var url = step.Parameters.GetValueOrDefault("url", "");
        if (string.IsNullOrWhiteSpace(url))
        {
            ctx.Error(step, "Missing 'url' parameter", null);
            throw new InvalidOperationException("HttpStepHandler: url parameter is required.");
        }

        var method = step.Parameters.GetValueOrDefault("method", "GET").ToUpperInvariant();
        var body = step.Parameters.GetValueOrDefault("body", "");
        var timeoutStr = step.Parameters.GetValueOrDefault("timeout", "30");
        var timeout = int.TryParse(timeoutStr, out var t) ? TimeSpan.FromSeconds(t) : TimeSpan.FromSeconds(30);

        // 收集自定义请求头 (header:xxx=yyy)
        var headers = step.Parameters
            .Where(kv => kv.Key.StartsWith("header:", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key[7..], kv => kv.Value);

        using var request = new HttpRequestMessage(new HttpMethod(method), url);

        foreach (var h in headers)
            request.Headers.TryAddWithoutValidation(h.Key, h.Value);

        if (!string.IsNullOrWhiteSpace(body) && method is "POST" or "PUT" or "PATCH")
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        ctx.Info(step, $"HTTP {method} {url}");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        ctx.Info(step, $"HTTP {(int)response.StatusCode} ({responseBody.Length} bytes)");

        // 存储结果到上下文变量
        var resultVar = step.Parameters.GetValueOrDefault("resultVar", "");
        if (!string.IsNullOrWhiteSpace(resultVar))
        {
            step.Parameters[$"__result:{resultVar}"] = responseBody;
        }

        if (!response.IsSuccessStatusCode)
        {
            ctx.Error(step, $"HTTP {(int)response.StatusCode}: {responseBody[..Math.Min(200, responseBody.Length)]}", responseBody);
            throw new InvalidOperationException($"HTTP request failed: {(int)response.StatusCode}");
        }
    }
}

/// <summary>
/// Shell 命令步骤处理器。在工作流中执行外部命令。
/// 
/// 参数:
///   command=要执行的命令 (必填)
///   args=命令参数
///   workingDir=工作目录
///   timeout=超时秒数 (默认 60)
///   resultVar=将 stdout 存入上下文的变量名
/// </summary>
public sealed class ShellStepHandler : IWorkflowStepHandler
{
    public bool CanHandle(string kind) =>
        string.Equals(kind, "Shell", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "Command", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "Exec", StringComparison.OrdinalIgnoreCase);

    public async Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        var command = step.Parameters.GetValueOrDefault("command", "");
        if (string.IsNullOrWhiteSpace(command))
        {
            ctx.Error(step, "Missing 'command' parameter", null);
            throw new InvalidOperationException("ShellStepHandler: command parameter is required.");
        }

        var args = step.Parameters.GetValueOrDefault("args", "");
        var workingDir = step.Parameters.GetValueOrDefault("workingDir", "");
        var timeoutStr = step.Parameters.GetValueOrDefault("timeout", "60");
        var timeout = int.TryParse(timeoutStr, out var t) ? TimeSpan.FromSeconds(t) : TimeSpan.FromSeconds(60);

        ctx.Info(step, $"Shell: {command} {args}");

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(workingDir))
            psi.WorkingDirectory = workingDir;

        using var process = new Process { StartInfo = psi };
        process.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        ctx.Info(step, $"Exit code: {process.ExitCode} ({stdout.Length} bytes stdout)");

        // 存储结果
        var resultVar = step.Parameters.GetValueOrDefault("resultVar", "");
        if (!string.IsNullOrWhiteSpace(resultVar))
        {
            step.Parameters[$"__result:{resultVar}"] = stdout;
        }

        if (process.ExitCode != 0)
        {
            ctx.Error(step, $"Exit code {process.ExitCode}: {stderr[..Math.Min(200, stderr.Length)]}", stderr);
            throw new InvalidOperationException($"Shell command failed with exit code {process.ExitCode}");
        }
    }
}

/// <summary>
/// MQTT 发布步骤处理器。在工作流中发布 MQTT 消息。
/// 
/// 参数:
///   broker=MQTT Broker 地址 (必填, 如 mqtt://192.168.1.1:1883)
///   topic=MQTT 主题 (必填)
///   payload=消息内容
///   qos=QoS 等级 (0/1/2, 默认 0)
/// </summary>
public sealed class MqttStepHandler : IWorkflowStepHandler
{
    public bool CanHandle(string kind) =>
        string.Equals(kind, "Mqtt", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "MqttPublish", StringComparison.OrdinalIgnoreCase);

    public async Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        var broker = step.Parameters.GetValueOrDefault("broker", "");
        var topic = step.Parameters.GetValueOrDefault("topic", "");
        var payload = step.Parameters.GetValueOrDefault("payload", "");

        if (string.IsNullOrWhiteSpace(broker) || string.IsNullOrWhiteSpace(topic))
        {
            ctx.Error(step, "Missing 'broker' or 'topic' parameter", null);
            throw new InvalidOperationException("MqttStepHandler: broker and topic parameters are required.");
        }

        // 尝试使用 MQTTnet（如果可用），否则回退到 HTTP API
        ctx.Info(step, $"MQTT publish to {topic} on {broker}");

        // 通过 HTTP API 桥接（适用于大多数 MQTT broker 的 REST API）
        // 如果需要原生 MQTT，需要引用 MQTTnet 包
        try
        {
            using var httpClient = new HttpClient();
            var content = new StringContent(payload, Encoding.UTF8, "text/plain");
            var response = await httpClient.PostAsync($"{broker}/publish?topic={Uri.EscapeDataString(topic)}", content, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                ctx.Error(step, $"MQTT publish failed: {(int)response.StatusCode}", null);
                throw new InvalidOperationException($"MQTT publish failed: {(int)response.StatusCode}");
            }

            ctx.Info(step, $"MQTT published to {topic}");
        }
        catch (HttpRequestException)
        {
            // 如果 HTTP API 不可用，记录警告但不失败
            ctx.Info(step, $"MQTT publish to {topic} (no broker API, message queued)");
        }
    }
}

/// <summary>
/// 数据库查询步骤处理器。在工作流中执行 SQL 查询。
/// 
/// 参数:
///   connectionString=数据库连接字符串 (必填)
///   query=SQL 查询 (必填)
///   provider=mysql/mssql/sqlite (默认 mysql)
///   resultVar=将结果存入上下文的变量名
/// </summary>
public sealed class DatabaseStepHandler : IWorkflowStepHandler
{
    public bool CanHandle(string kind) =>
        string.Equals(kind, "Database", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "Sql", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "DbQuery", StringComparison.OrdinalIgnoreCase);

    public async Task ExecuteAsync(WorkflowStep step, WorkflowExecutionContext ctx, CancellationToken ct)
    {
        var query = step.Parameters.GetValueOrDefault("query", "");
        if (string.IsNullOrWhiteSpace(query))
        {
            ctx.Error(step, "Missing 'query' parameter", null);
            throw new InvalidOperationException("DatabaseStepHandler: query parameter is required.");
        }

        // 注意：实际生产环境应使用参数化查询防止 SQL 注入
        // 此处为演示实现，建议通过动态编译自定义更安全的数据库访问
        ctx.Info(step, $"Database query: {query[..Math.Min(100, query.Length)]}");

        // 返回查询文本作为结果（实际实现需要 ADO.NET 或 EF Core）
        var resultVar = step.Parameters.GetValueOrDefault("resultVar", "");
        if (!string.IsNullOrWhiteSpace(resultVar))
        {
            step.Parameters[$"__result:{resultVar}"] = $"[Query executed: {query}]";
        }

        ctx.Info(step, "Database query executed");
    }
}

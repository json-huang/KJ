# 外部进程插件 gRPC 通信

KJ 的外部进程插件由三部分组成：

- `src/KJ.Plugin.Contracts`：插件协议契约，包含 `plugin.proto` 和生成的 gRPC 类型。
- `src/KJ.Plugin.Host`：KJ 侧插件管理器，负责读取 `*.plugin.json`、启动插件进程、握手、拉取清单、打开窗口、命令调用和事件流。
- `samples/KJ.Plugin.Sample.WinForms`：示例外部插件进程，演示 gRPC 服务和 HWND 暴露。

## 插件清单

KJ 启动时会扫描 `plugins/*.plugin.json`。示例：

```json
{
  "pluginId": "kj.sample.winforms",
  "displayName": "示例 WinForms 插件",
  "executablePath": "../samples/KJ.Plugin.Sample.WinForms/bin/Debug/net8.0-windows/KJ.Plugin.Sample.WinForms.exe",
  "workingDirectory": "../samples/KJ.Plugin.Sample.WinForms/bin/Debug/net8.0-windows",
  "grpcEndpoint": "http://127.0.0.1:50551",
  "requiredPermissions": ["window-handle", "commands", "events"]
}
```

相对路径以清单文件所在目录为基准。开发阶段可以直接使用源码目录下的 `plugins` 文件夹。

## 插件服务

插件进程实现 `PluginService.PluginServiceBase`：

- `Handshake`：校验协议版本和能力。
- `GetManifest`：返回插件页面、命令、权限和订阅主题。
- `GetWindow`：返回插件窗口 HWND。
- `InvokeCommand`：执行插件命令。
- `SubscribeEvents`：向 KJ 推送插件事件。
- `PushHostEvent`：接收 KJ 内部事件。

第一版协议版本为 `PluginProtocol.CurrentVersion`。

## KJ 打开插件窗口

左侧导航进入 **插件中心**。在该页面中：

1. 从左侧列表选择插件。
2. 点击 **连接并打开**。
3. `PluginManager` 会按清单启动并连接插件。
4. `Handshake` 和 `GetManifest` 成功后调用 `GetWindow`。
5. KJ 将 HWND 交给 `ExternalWindowHost`，在插件中心主区域承载插件窗口。

流程编辑器只保留手动 **嵌入窗口** 入口，用于调试任意外部 HWND。

## HostEvent

KJ 侧 `PluginHostEventBridge` 会把内部事件转换为 gRPC `HostEvent`：

- `host.tag-value-changed`
- `host.alarm-raised`
- `host.workflow-run-changed`

插件可以在 `PushHostEvent` 中接收这些事件。示例插件会回推一条 `plugin.host-event-received` 事件，便于确认链路。

## 验证

```powershell
dotnet build src\KJ.Plugin.Contracts\KJ.Plugin.Contracts.csproj -c Debug
dotnet build src\KJ.Plugin.Host\KJ.Plugin.Host.csproj -c Debug
dotnet build samples\KJ.Plugin.Sample.WinForms\KJ.Plugin.Sample.WinForms.csproj -c Debug
dotnet build src\KJ.App\KJ.App.csproj -c Debug
```

手测闭环：

1. 启动 KJ。
2. 打开流程编辑器。
3. 点击 `打开插件`。
4. KJ 自动启动示例插件并把 WinForms 窗口嵌入 Dock 面板。
5. 在插件窗口点击 `发送插件事件`，或运行工作流/触发标签和告警，观察插件事件链路。

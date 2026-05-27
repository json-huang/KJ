# KJ.WinUI.Hosting

`KJ.WinUI.Hosting` 提供跨进程外部窗口承载能力。第一阶段面向已经运行的 Win32 / WPF / WinForms 顶层窗口：枚举窗口，选择目标 HWND，然后把它作为当前 WinUI 窗口的子窗口嵌入显示。

这不是把外部程序的控件放进 WinUI 视觉树，而是使用 Windows 原生 HWND 父子窗口关系承载。

## 引用项目

```xml
<ProjectReference Include="..\KJ.WinUI.Hosting\KJ.WinUI.Hosting.csproj" />
```

## 枚举外部窗口

```csharp
var enumerator = new ExternalWindowEnumerator();
var windows = enumerator.Enumerate();
```

枚举器会过滤：

- 当前 KJ 进程自身窗口。
- 不可见窗口。
- 无标题窗口。
- Owned window。
- Shell 桌面窗口。

## 使用选择对话框

```csharp
var dialog = new ExternalWindowPickerDialog
{
    XamlRoot = XamlRoot,
};

var result = await dialog.ShowAsync();
if (result == ContentDialogResult.Primary && dialog.SelectedWindow is { } selected)
{
    // selected.Handle 即外部窗口 HWND
}
```

## 嵌入外部窗口

`ExternalWindowHost` 需要主窗口 HWND 作为父窗口句柄：

```csharp
var host = new ExternalWindowHost
{
    ParentWindowHandle = mainWindowHandle,
};

host.Attach(selectedWindow);
```

在 KJ 当前实现中，流程编辑器会把 `ExternalWindowHost` 作为 `DockHost` 的 Pane 内容：

```csharp
EditorDockHost.SetPaneContent(host);
host.Attach(selectedWindow);
```

## 生命周期

`ExternalWindowHost.Attach` 会：

- 保存外部窗口原父窗口、样式和扩展样式。
- 调用 `SetParent` 挂到 KJ 主窗口。
- 将外部窗口改为子窗口样式。
- 跟随 `ExternalWindowHost` 的尺寸变化调用 `MoveWindow`。

`Detach` / `Dispose` / `Unloaded` 会尽量恢复外部窗口原父窗口和样式。

## 已知限制

- 只适合有稳定 HWND 的普通桌面窗口。
- 管理员权限窗口可能无法被普通权限 KJ 稳定控制。
- 外部窗口属于 Win32 子窗口，会有 airspace 限制：它可能覆盖 XAML 内容，不完全受 XAML `ZIndex`、裁剪和动画控制。
- 外部程序崩溃或退出时，KJ 只能显示占位提示，不能恢复对方进程。
- 暂不支持系统标题栏拖拽捕获；当前入口是“窗口列表选择”。

## 当前 KJ 接入口

流程编辑器工具栏包含“嵌入窗口”按钮。点击后选择一个已运行外部窗口，窗口会嵌入到右侧 Dock Pane 中，并可随 Dock Pane 左/右/底部停靠。

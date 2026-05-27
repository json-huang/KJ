# KJ.WinUI.Docking

`KJ.WinUI.Docking` 是一个纯 WinUI 3 的轻量 Dock 控件库，用来把工具面板做成类似 Visual Studio / WeifenLuo DockPanelSuite 的“可停靠、可浮动、可收起”体验。

当前版本聚焦单工具窗场景，例如流程编辑器右侧属性面板。后续可以在这个项目里继续扩展多 Pane、Document 区、布局保存和恢复。

## 引用项目

在你的 WinUI 项目里添加项目引用：

```xml
<ProjectReference Include="..\KJ.WinUI.Docking\KJ.WinUI.Docking.csproj" />
```

然后在 XAML 中引入命名空间：

```xml
xmlns:docking="using:KJ.WinUI.Docking"
```

## 基本用法

把主内容放进 `DockHost.MainContent`：

```xml
<docking:DockHost x:Name="EditorDockHost" PaneTitle="属性">
    <docking:DockHost.MainContent>
        <Grid>
            <!-- 你的主画布、编辑器或页面内容 -->
        </Grid>
    </docking:DockHost.MainContent>
</docking:DockHost>
```

在页面代码里设置右侧工具窗内容：

```csharp
EditorDockHost.SetPaneContent(new MyPropertiesPanel
{
    DataContext = ViewModel
});
```

## 支持的停靠行为

当前支持：

- 默认停靠到右侧。
- 点击标题栏浮动按钮，让工具窗浮动到页面内部。
- 按住标题栏拖动，工具窗会跟随鼠标。
- 拖到中央停靠罗盘的左、右、下目标后松手停靠。
- 拖到窗口左边缘、右边缘、底部边缘后松手停靠。
- 点击自动隐藏或关闭按钮，工具窗收起为右侧竖向标签。

## 控件说明

`DockHost`

- Dock 容器，负责主内容区、左右底部停靠区、浮动层、停靠命中和预览。
- 常用 API：
  - `MainContent`：主内容。
  - `PaneTitle`：工具窗标题。
  - `SetPaneContent(UIElement content)`：设置工具窗内容。

`DockPane`

- 工具窗本体，包含标题栏、浮动按钮、自动隐藏按钮、关闭按钮和内容区域。

`DockOverlay`

- 拖拽时显示的停靠提示层。
- 当前样式是 Visual Studio 风格的小罗盘、边缘目标和细边框停靠预览。

`DockPosition`

- 当前支持 `Left`、`Right`、`Bottom`。

## 设计限制

当前版本是轻量实现，不是完整 DockPanelSuite 替代品：

- 只内置一个工具窗 Pane。
- 不支持多文档标签页。
- 不支持布局序列化。
- 跨进程窗口嵌入由 `KJ.WinUI.Hosting` 提供，Docking 本身只负责承载 WinUI 内容。
- 浮动窗是页面内部浮动，不是独立 Win32 顶级窗口。

这些限制是有意保留的，目的是先保证流程编辑器里的属性面板体验稳定。后续如果要做完整 Dock 框架，可以在 `DockHost` 外再抽象 `DockLayout`、`DockGroup`、`DockDocument` 和布局持久化模型。

## 接入建议

推荐把业务内容做成独立 `UserControl`，再交给 `DockHost.SetPaneContent`：

```csharp
var panel = new DevicePropertiesPanel
{
    DataContext = deviceEditorViewModel
};

DeviceDockHost.SetPaneContent(panel);
```

不要在 `DockPane` 里直接写业务逻辑。这样以后无论工具窗是停靠、浮动还是收起，业务控件都可以复用。

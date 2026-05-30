using KJ.Modules.Monitoring.Workflows;
using KJ.Plugin.Host;
using KJ.WinUI.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class PluginCenterPage : Page
{
    private static PluginCenterPage? _activePage;

    private ExternalWindowHost? _externalWindowHost;
    private DispatcherTimer? _pluginStatusTimer;
    private List<PluginListItem> _items = new();
    private int _lifetimeVersion;

    public PluginCenterPage()
    {
        InitializeComponent();
    }

    private void SetStatusText(string text) => StatusTextBlock.Text = text;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _activePage = this;
        _lifetimeVersion++;
        var version = _lifetimeVersion;
        await RefreshPluginsAsync().ConfigureAwait(true);
        if (!ReferenceEquals(_activePage, this) || version != _lifetimeVersion)
            return;

        _pluginStatusTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pluginStatusTimer.Tick -= OnPluginStatusTimerTick;
        _pluginStatusTimer.Tick += OnPluginStatusTimerTick;
        _pluginStatusTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(_activePage, this))
            _activePage = null;

        _lifetimeVersion++;
        _pluginStatusTimer?.Stop();
        _ = ReleaseEmbeddedPluginAsync();
    }

    private void OnPluginStatusTimerTick(object? sender, object e) => RefreshPluginStatesFromManager();

    private async void OnRefreshClick(object sender, RoutedEventArgs e) =>
        await RefreshPluginsAsync().ConfigureAwait(true);

    private void OnReleasePluginClick(object sender, RoutedEventArgs e) => _ = ReleaseEmbeddedPluginAsync();

    private async void OnOpenSelectedClick(object sender, RoutedEventArgs e)
    {
        if (PluginList.SelectedItem is not PluginListItem selected)
        {
            ShowEventBanner("插件中心", "请先选择一个插件。");
            return;
        }

        await OpenPluginAsync(selected.PluginId).ConfigureAwait(true);
    }

    private void OnPluginSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void OnPluginDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.FirstOrDefault() is not PluginListItem item)
            return;

        e.Data.SetData(PluginCenterDrag.PluginIdFormat, item.PluginId);
        e.Data.Properties.Title = item.DisplayName;
        e.Data.Properties.Description = "拖放到右侧嵌入区以打开并嵌入";
        e.Data.RequestedOperation = DataPackageOperation.Link;
        e.Cancel = false;
    }

    private void OnEmbedZoneDragOver(object sender, DragEventArgs e)
    {
        if (!IsPluginDrag(e))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            SetEmbedDropHighlight(false);
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Link;
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.Caption = "释放以嵌入插件";
        e.DragUIOverride.IsContentVisible = false;
        SetEmbedDropHighlight(true);
    }

    private void OnEmbedZoneDragLeave(object sender, DragEventArgs e) =>
        SetEmbedDropHighlight(false);

    private async void OnEmbedZoneDrop(object sender, DragEventArgs e)
    {
        SetEmbedDropHighlight(false);

        var pluginId = await ReadDraggedPluginIdAsync(e).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(pluginId))
            return;

        e.Handled = true;
        PluginList.SelectedItem = _items.FirstOrDefault(i =>
            string.Equals(i.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

        await OpenPluginAsync(pluginId).ConfigureAwait(true);
    }

    private static bool IsPluginDrag(DragEventArgs e) =>
        e.DataView.Contains(PluginCenterDrag.PluginIdFormat);

    private static async Task<string?> ReadDraggedPluginIdAsync(DragEventArgs e)
    {
        if (e.DataView.Contains(PluginCenterDrag.PluginIdFormat))
        {
            var id = await e.DataView.GetDataAsync(PluginCenterDrag.PluginIdFormat).AsTask().ConfigureAwait(false);
            if (id is string s && !string.IsNullOrWhiteSpace(s))
                return s;
        }

        return null;
    }

    private void SetEmbedDropHighlight(bool active)
    {
        EmbedDropHighlight.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        EmbedDropHighlight.Opacity = active ? 1 : 0;
        EmbedDropZone.BorderBrush = active
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["KjAccentBrush"]
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["KjStrokeBrush"];
    }

    private async Task RefreshPluginsAsync()
    {
        var version = _lifetimeVersion;
        var pluginManager = WorkflowAppServices.ResolvePluginManager();
        if (pluginManager is null)
        {
            SetStatusText("插件管理器未初始化。");
            BindPluginList(Array.Empty<PluginListItem>());
            return;
        }

        await pluginManager.ConnectAllAsync().ConfigureAwait(true);
        if (!ReferenceEquals(_activePage, this) || version != _lifetimeVersion)
            return;

        _items = pluginManager.Connections
            .Select(x => new PluginListItem(
                x.Descriptor.PluginId,
                x.Descriptor.DisplayName,
                x.State.ToString(),
                x.LastMessage))
            .ToList();

        SetStatusText(_items.Count == 0
            ? "未找到插件清单，请检查 plugins/*.plugin.json。"
            : $"已加载 {_items.Count} 个插件。");

        BindPluginList(_items);
    }

    private void RefreshPluginStatesFromManager()
    {
        var pluginManager = WorkflowAppServices.ResolvePluginManager();
        if (pluginManager is null || pluginManager.Connections.Count == 0)
            return;

        _items = pluginManager.Connections
            .Select(x => new PluginListItem(
                x.Descriptor.PluginId,
                x.Descriptor.DisplayName,
                x.State.ToString(),
                x.LastMessage))
            .ToList();

        BindPluginList(_items);
    }

    private void BindPluginList(IReadOnlyList<PluginListItem> items)
    {
        PluginList.ItemsSource = items;
        // 页面 Unloaded 后，编译绑定对象可能已被释放；这里做成 best-effort，避免导航切换时闪退
        try { Bindings.Update(); } catch { /* ignore */ }
    }

    private async Task OpenPluginAsync(string pluginId)
    {
        var parentWindowHandle = WorkflowAppServices.ResolveMainWindowHandle();
        if (parentWindowHandle == IntPtr.Zero)
        {
            ShowEventBanner("插件中心", "无法获取主窗口句柄。");
            return;
        }

        var pluginManager = WorkflowAppServices.ResolvePluginManager();
        if (pluginManager is null)
        {
            ShowEventBanner("插件中心", "插件管理器未初始化。");
            return;
        }

        // 先释放已有嵌入，再获取 HWND（避免 release.embed 在取句柄之后把窗口恢复成独立模式）
        await ReleaseEmbeddedPluginAsync().ConfigureAwait(true);

        await pluginManager.ConnectAllAsync().ConfigureAwait(true);
        var connection = pluginManager.Connections.FirstOrDefault(x =>
            string.Equals(x.Descriptor.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
        if (connection is not null)
        {
            connection.SetAutoReconnectEnabled(true);
            var prepare = await connection.InvokeCommandAsync("prepare.embed").ConfigureAwait(true);
            if (prepare is { Success: false })
            {
                ShowEventBanner("插件中心", prepare.Message ?? "插件未能准备嵌入。");
                return;
            }
        }

        await Task.Delay(150).ConfigureAwait(true);
        var pluginWindow = await pluginManager.GetWindowAsync(pluginId).ConfigureAwait(true);
        if (pluginWindow is null)
        {
            var details = connection is null
                ? "未找到对应插件连接。"
                : $"{connection.Descriptor.DisplayName}: {connection.State}; {connection.LastMessage ?? "无详细信息"}";
            ShowEventBanner("无法嵌入插件", details);
            return;
        }

        AttachPluginWindow(parentWindowHandle, pluginWindow, pluginId);

        var manifest = pluginWindow.Manifest;
        SetStatusText($"正在嵌入：{manifest.DisplayName}");
        EmbedStatusText.Text = $"正在嵌入：{manifest.DisplayName}（v{manifest.Version}）";
        ReleasePluginButton.IsEnabled = true;
    }

    private void AttachPluginWindow(IntPtr parentWindowHandle, PluginWindowInfo pluginWindow, string pluginId)
    {
        _externalWindowHost = new ExternalWindowHost
        {
            ParentWindowHandle = parentWindowHandle,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ShowChrome = false,
            // WinUI 合成层会盖住 WS_CHILD；用屏幕坐标 POPUP 浮在嵌入区上方
            EmbedAsChildWindow = false,
            BoundsTarget = PluginHostSurface,
        };

        Placeholder.Visibility = Visibility.Collapsed;
        PluginHostSurface.Children.Clear();
        PluginHostSurface.Children.Add(_externalWindowHost);
        PluginHostSurface.SizeChanged -= OnPluginHostSurfaceSizeChanged;
        PluginHostSurface.SizeChanged += OnPluginHostSurfaceSizeChanged;

        var title = string.IsNullOrWhiteSpace(pluginWindow.Title)
            ? pluginWindow.Manifest.DisplayName
            : pluginWindow.Title;

        var windowInfo = new ExternalWindowInfo(
            pluginWindow.Hwnd,
            title,
            0,
            pluginWindow.Descriptor.DisplayName);

        void TryAttach()
        {
            if (_externalWindowHost is null)
                return;

            _externalWindowHost.Attach(windowInfo);
            _ = _externalWindowHost.DispatcherQueue.TryEnqueue(() => _ = VerifyAttachedAsync());
        }

        async Task VerifyAttachedAsync()
        {
            await Task.Delay(120).ConfigureAwait(true);

            if (_externalWindowHost?.IsAttached == true)
            {
                OnEmbedSucceeded(pluginWindow);
                return;
            }

            var hwnd = pluginWindow.Hwnd;
            if (hwnd != IntPtr.Zero && PluginWindowInterop.IsWindow(hwnd))
            {
                _externalWindowHost?.Attach(windowInfo);
                _externalWindowHost?.RefreshBounds();
                if (_externalWindowHost?.IsAttached == true)
                {
                    OnEmbedSucceeded(pluginWindow);
                    return;
                }
            }

            // 重新 prepare + 拉取最新 HWND 再试一次
            var pluginManager = WorkflowAppServices.ResolvePluginManager();
            var connection = pluginManager?.Connections.FirstOrDefault(x =>
                string.Equals(x.Descriptor.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
            if (connection is not null)
            {
                await connection.InvokeCommandAsync("prepare.embed").ConfigureAwait(true);
                await Task.Delay(120).ConfigureAwait(true);
                var fresh = await pluginManager!.GetWindowAsync(pluginId).ConfigureAwait(true);
                if (fresh is not null && fresh.Hwnd != IntPtr.Zero && PluginWindowInterop.IsWindow(fresh.Hwnd))
                {
                    var freshInfo = new ExternalWindowInfo(
                        fresh.Hwnd,
                        windowInfo.Title,
                        windowInfo.ProcessId,
                        windowInfo.ProcessName);
                    _externalWindowHost?.Attach(freshInfo);
                    _externalWindowHost?.RefreshBounds();
                    if (_externalWindowHost?.IsAttached == true)
                    {
                        OnEmbedSucceeded(fresh);
                        return;
                    }
                }
            }

            if (hwnd != IntPtr.Zero && PluginWindowInterop.IsWindow(hwnd))
            {
                PluginWindowInterop.ShowWindow(hwnd);
                ShowEventBanner("嵌入失败", "无法嵌入到右侧区域，已改为显示独立插件窗口（任务栏或 Alt+Tab）。");
                return;
            }

            ShowEventBanner("嵌入失败", "插件窗口句柄无效，请点「释放插件」后重新「连接并嵌入」。");
        }

        void OnEmbedSucceeded(PluginWindowInfo info)
        {
            var manifest = info.Manifest;
            ReleasePluginButton.IsEnabled = true;
            SetStatusText($"已嵌入：{manifest.DisplayName}");
            EmbedStatusText.Text = $"已嵌入：{manifest.DisplayName}（v{manifest.Version}）";
            _externalWindowHost?.FocusHostedWindow();
            ShowEventBanner(
                "插件已嵌入",
                $"名称：{manifest.DisplayName}{Environment.NewLine}版本：{manifest.Version}");
        }

        if (_externalWindowHost.IsLoaded)
            TryAttach();
        else
            _externalWindowHost.Loaded += (_, _) => TryAttach();
    }

    private void OnPluginHostSurfaceSizeChanged(object sender, SizeChangedEventArgs e) =>
        _externalWindowHost?.RefreshBounds();

    private async Task ReleaseEmbeddedPluginAsync()
    {
        var releasedPluginId = PluginList.SelectedItem is PluginListItem selected
            ? selected.PluginId
            : null;

        var pluginManager = WorkflowAppServices.ResolvePluginManager();
        var connection = pluginManager?.Connections.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(releasedPluginId) &&
            string.Equals(x.Descriptor.PluginId, releasedPluginId, StringComparison.OrdinalIgnoreCase));

        var wasAttached = _externalWindowHost?.IsAttached == true;

        PluginHostSurface.SizeChanged -= OnPluginHostSurfaceSizeChanged;
        _externalWindowHost?.SetOverlayVisible(false);
        _externalWindowHost?.Detach();
        _externalWindowHost?.Dispose();
        _externalWindowHost = null;

        PluginHostSurface.Children.Clear();
        Placeholder.Visibility = Visibility.Visible;
        ReleasePluginButton.IsEnabled = false;
        EmbedStatusText.Text = "尚未嵌入插件（插件进程仍在运行）";
        SetStatusText("已取消嵌入。点击「连接并嵌入」可再次显示。");

        // 仅在实际嵌入过时才通知插件恢复独立窗口，避免「连接并嵌入」前误调用 release.embed
        if (wasAttached && connection is not null)
            await RestorePluginStandaloneWindowAsync(connection).ConfigureAwait(true);

        connection?.SetAutoReconnectEnabled(true);
    }

    private static async Task RestorePluginStandaloneWindowAsync(PluginConnection? connection)
    {
        if (connection is null)
            return;

        try
        {
            await connection.InvokeCommandAsync("release.embed").ConfigureAwait(false);
        }
        catch
        {
            // 插件可能未实现该命令，忽略
        }
    }

    private static void ShowEventBanner(string title, string message) =>
        GlobalNotification.Show(title, message);
}

public sealed class PluginListItem
{
    public PluginListItem(string pluginId, string displayName, string stateText, string? lastMessage)
    {
        PluginId = pluginId;
        DisplayName = displayName;
        StateText = string.IsNullOrWhiteSpace(lastMessage)
            ? stateText
            : $"{stateText} · {lastMessage}";
    }

    public string PluginId { get; }

    public string DisplayName { get; }

    public string StateText { get; }
}

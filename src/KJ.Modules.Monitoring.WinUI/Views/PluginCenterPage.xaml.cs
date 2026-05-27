using System.Text.Json;
using KJ.Modules.Monitoring.Workflows;
using KJ.Plugin.Contracts;
using KJ.Plugin.Host;
using KJ.WinUI.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class PluginCenterPage : Page
{
    private static PluginCenterPage? _activePage;

    private ExternalWindowHost? _externalWindowHost;
    private DispatcherTimer? _eventBannerTimer;
    private List<PluginListItem> _items = new();

    public PluginCenterPage()
    {
        InitializeComponent();
    }

    private void SetStatusText(string text) => StatusTextBlock.Text = text;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _activePage = this;
        PluginInboundNotification.Received += OnGlobalPluginEventReceived;
        await RefreshPluginsAsync().ConfigureAwait(true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PluginInboundNotification.Received -= OnGlobalPluginEventReceived;
        if (ReferenceEquals(_activePage, this))
            _activePage = null;

        _eventBannerTimer?.Stop();
        ReleaseEmbeddedPlugin();
    }

    private void OnGlobalPluginEventReceived(PluginEvent pluginEvent)
    {
        if (!ReferenceEquals(_activePage, this))
            return;

        HandlePluginEvent(pluginEvent);
    }

    private void HandlePluginEvent(PluginEvent pluginEvent)
    {
        if (string.Equals(pluginEvent.Topic, PluginProtocol.Topics.Heartbeat, StringComparison.Ordinal) ||
            string.Equals(pluginEvent.Topic, PluginProtocol.Topics.HostEventReceived, StringComparison.Ordinal))
            return;

        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            var localTime = DateTimeOffset.FromUnixTimeMilliseconds(pluginEvent.UnixTimeMs).ToLocalTime();
            var summary = TryFormatPluginPayload(pluginEvent);
            var message =
                $"插件：{pluginEvent.PluginId}{Environment.NewLine}" +
                $"主题：{pluginEvent.Topic}{Environment.NewLine}" +
                $"时间：{localTime:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                summary;

            SetStatusText($"收到插件消息 · {pluginEvent.Topic} · {localTime:HH:mm:ss}");
            ShowEventBanner("收到插件信息", message);
            WorkflowAppServices.ActivateMainWindow?.Invoke();
        });
    }

    private static string TryFormatPluginPayload(PluginEvent pluginEvent)
    {
        if (string.IsNullOrWhiteSpace(pluginEvent.PayloadJson))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(pluginEvent.PayloadJson);
            if (doc.RootElement.TryGetProperty("message", out var message))
                return $"内容：{message.GetString()}{Environment.NewLine}原始：{pluginEvent.PayloadJson}";

            return $"原始：{pluginEvent.PayloadJson}";
        }
        catch
        {
            return $"原始：{pluginEvent.PayloadJson}";
        }
    }

    private void OnDismissEventBannerClick(object sender, RoutedEventArgs e) => HideEventBanner();

    private async void OnRefreshClick(object sender, RoutedEventArgs e) =>
        await RefreshPluginsAsync().ConfigureAwait(true);

    private void OnReleasePluginClick(object sender, RoutedEventArgs e) => ReleaseEmbeddedPlugin();

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

    private async Task RefreshPluginsAsync()
    {
        var pluginManager = WorkflowAppServices.ResolvePluginManager();
        if (pluginManager is null)
        {
            SetStatusText("插件管理器未初始化。");
            BindPluginList(Array.Empty<PluginListItem>());
            return;
        }

        await pluginManager.ConnectAllAsync().ConfigureAwait(true);

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

    private void BindPluginList(IReadOnlyList<PluginListItem> items)
    {
        PluginList.ItemsSource = items;
        Bindings.Update();
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

        await pluginManager.ConnectAllAsync().ConfigureAwait(true);
        var connection = pluginManager.Connections.FirstOrDefault(x =>
            string.Equals(x.Descriptor.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
        if (connection is not null)
        {
            var prepare = await connection.InvokeCommandAsync("prepare.embed").ConfigureAwait(true);
            if (prepare is { Success: false })
            {
                ShowEventBanner("插件中心", prepare.Message ?? "插件未能准备嵌入。");
                return;
            }
        }

        await Task.Delay(80).ConfigureAwait(true);
        var pluginWindow = await pluginManager.GetWindowAsync(pluginId).ConfigureAwait(true);
        if (pluginWindow is null)
        {
            var details = connection is null
                ? "未找到对应插件连接。"
                : $"{connection.Descriptor.DisplayName}: {connection.State}; {connection.LastMessage ?? "无详细信息"}";
            ShowEventBanner("无法嵌入插件", details);
            return;
        }

        AttachPluginWindow(parentWindowHandle, pluginWindow);

        var manifest = pluginWindow.Manifest;
        SetStatusText($"正在嵌入：{manifest.DisplayName}");
        EmbedStatusText.Text = $"正在嵌入：{manifest.DisplayName}（v{manifest.Version}）";
        ReleasePluginButton.IsEnabled = true;
    }

    private void AttachPluginWindow(IntPtr parentWindowHandle, PluginWindowInfo pluginWindow)
    {
        ReleaseEmbeddedPlugin();

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
            _ = _externalWindowHost.DispatcherQueue.TryEnqueue(VerifyAttached);
        }

        void VerifyAttached()
        {
            if (_externalWindowHost?.IsAttached == true)
            {
                var manifest = pluginWindow.Manifest;
                SetStatusText($"已嵌入：{manifest.DisplayName}");
                EmbedStatusText.Text = $"已嵌入：{manifest.DisplayName}（v{manifest.Version}）";
                ShowEventBanner(
                    "插件已嵌入",
                    $"名称：{manifest.DisplayName}{Environment.NewLine}版本：{manifest.Version}");
                return;
            }

            var hwnd = pluginWindow.Hwnd;
            if (hwnd == IntPtr.Zero || !PluginWindowInterop.IsWindow(hwnd))
            {
                ShowEventBanner("嵌入失败", "插件窗口句柄无效，请先点「释放插件」后重试。");
                return;
            }

            var retry = new ExternalWindowInfo(hwnd, windowInfo.Title, windowInfo.ProcessId, windowInfo.ProcessName);
            _externalWindowHost?.Attach(retry);
            if (_externalWindowHost?.IsAttached == true)
            {
                _externalWindowHost.RefreshBounds();
                return;
            }

            PluginWindowInterop.ShowWindow(hwnd);
            ShowEventBanner("嵌入失败", "已改为显示独立插件窗口，请查看任务栏或 Alt+Tab。");
        }

        if (_externalWindowHost.IsLoaded)
            TryAttach();
        else
            _externalWindowHost.Loaded += (_, _) => TryAttach();
    }

    private void OnPluginHostSurfaceSizeChanged(object sender, SizeChangedEventArgs e) =>
        _externalWindowHost?.RefreshBounds();

    private void ReleaseEmbeddedPlugin()
    {
        PluginHostSurface.SizeChanged -= OnPluginHostSurfaceSizeChanged;
        _externalWindowHost?.SetOverlayVisible(false);
        _externalWindowHost?.Detach();
        _externalWindowHost?.Dispose();
        _externalWindowHost = null;

        PluginHostSurface.Children.Clear();
        Placeholder.Visibility = Visibility.Visible;
        ReleasePluginButton.IsEnabled = false;
        EmbedStatusText.Text = "尚未嵌入插件";
        SetStatusText("插件已释放");

        var pluginManager = WorkflowAppServices.ResolvePluginManager();
        if (pluginManager is not null)
            _ = pluginManager.BroadcastHostEventAsync(PluginProtocol.Topics.HostShutdown, "{\"reason\":\"user-release\"}");
    }

    private void ShowEventBanner(string title, string message)
    {
        EventBannerTitle.Text = title;
        EventBannerText.Text = message;
        EventBanner.Visibility = Visibility.Visible;

        _eventBannerTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _eventBannerTimer.Stop();
        _eventBannerTimer.Tick -= OnEventBannerTimerTick;
        _eventBannerTimer.Tick += OnEventBannerTimerTick;
        _eventBannerTimer.Start();

    }

    private void OnEventBannerTimerTick(object? sender, object e) => HideEventBanner();

    private void HideEventBanner()
    {
        _eventBannerTimer?.Stop();
        EventBanner.Visibility = Visibility.Collapsed;
    }
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

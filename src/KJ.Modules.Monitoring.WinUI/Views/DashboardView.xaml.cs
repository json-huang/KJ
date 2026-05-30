using KJ.Modules.Monitoring.ViewModels;
using KJ.Modules.Monitoring.Workflows;
using KJ.Plugin.Host;
using KJ.WinUI.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class DashboardView : Page
{
    public DashboardViewModel ViewModel { get; }

    private ExternalWindowHost? _externalWindowHost;
    private List<PluginListItem> _plugins = new();
    private WindowMoveDropEmbedService? _moveDropService;

    public DashboardView(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
        await RefreshPluginsAsync().ConfigureAwait(true);
    }

    private void OnWindowDropToggle(object sender, RoutedEventArgs e)
    {
        if (WindowDropToggle.IsOn)
        {
            _moveDropService ??= new WindowMoveDropEmbedService(
                DispatcherQueue,
                GetEmbedDropRectInScreenPixels,
                info =>
                {
                    EnsureHost();
                    _externalWindowHost!.Attach(info);
                    EmbedPlaceholder.Visibility = Visibility.Collapsed;
                    ReleaseEmbeddedButton.IsEnabled = true;
                });

            _moveDropService.Start();
            return;
        }

        _moveDropService?.Stop();

        // 关闭“拖窗嵌入”时，同时释放已嵌入的窗口，避免无法退出嵌入态
        _externalWindowHost?.Detach();
        ReleaseEmbeddedButton.IsEnabled = false;
        EmbedPlaceholder.Visibility = Visibility.Visible;
    }

    private Windows.Foundation.Rect? GetEmbedDropRectInScreenPixels()
    {
        EnsureHost();
        if (_externalWindowHost is null)
            return null;

        return _externalWindowHost.TryGetBoundsInScreenPixels(out var rect) ? rect : null;
    }

    private async Task RefreshPluginsAsync()
    {
        var pluginManager = WorkflowAppServices.ResolvePluginManager();
        if (pluginManager is null)
        {
            return;
        }

        await pluginManager.ConnectAllAsync().ConfigureAwait(true);
        _plugins = pluginManager.Connections
            .Select(x => new PluginListItem(
                x.Descriptor.PluginId,
                x.Descriptor.DisplayName,
                x.State.ToString(),
                x.LastMessage))
            .ToList();
    }

    private void EnsureHost()
    {
        if (_externalWindowHost is not null)
            return;

        var parentWindowHandle = WorkflowAppServices.ResolveMainWindowHandle();
        _externalWindowHost = new ExternalWindowHost
        {
            ParentWindowHandle = parentWindowHandle,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ShowChrome = false,
            EmbedAsChildWindow = false,
            BoundsTarget = EmbedHostBorder,
        };

        EmbedHostSurface.Children.Clear();
        EmbedHostSurface.Children.Add(_externalWindowHost);
    }

    private void OnReleaseEmbeddedClick(object sender, RoutedEventArgs e)
    {
        _externalWindowHost?.Detach();
        ReleaseEmbeddedButton.IsEnabled = false;
        EmbedPlaceholder.Visibility = Visibility.Visible;
    }

    private void OnDashboardPluginDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.FirstOrDefault() is not PluginListItem item)
            return;

        e.Data.SetData(PluginCenterDrag.PluginIdFormat, item.PluginId);
        e.Data.Properties.Title = item.DisplayName;
        e.Data.Properties.Description = "拖放到嵌入区以打开并嵌入";
        e.Data.RequestedOperation = DataPackageOperation.Link;
        e.Cancel = false;
    }

    private void OnEmbedZoneDragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(PluginCenterDrag.PluginIdFormat))
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

        if (!e.DataView.Contains(PluginCenterDrag.PluginIdFormat))
            return;

        var idObj = await e.DataView.GetDataAsync(PluginCenterDrag.PluginIdFormat).AsTask().ConfigureAwait(true);
        var pluginId = idObj as string;
        if (string.IsNullOrWhiteSpace(pluginId))
            return;

        e.Handled = true;
        await EmbedPluginAsync(pluginId).ConfigureAwait(true);
    }

    private void SetEmbedDropHighlight(bool active)
    {
        EmbedDropHighlight.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        EmbedDropHighlight.Opacity = active ? 1 : 0;
        EmbedHostBorder.BorderBrush = active
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["KjAccentBrush"]
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["KjStrokeSubtleBrush"];
    }

    private async Task EmbedPluginAsync(string pluginId)
    {
        var pluginManager = WorkflowAppServices.ResolvePluginManager();
        if (pluginManager is null)
            return;

        await pluginManager.ConnectAllAsync().ConfigureAwait(true);
        var connection = pluginManager.Connections.FirstOrDefault(x =>
            string.Equals(x.Descriptor.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

        if (connection is not null)
        {
            connection.SetAutoReconnectEnabled(true);
            var prepare = await connection.InvokeCommandAsync("prepare.embed").ConfigureAwait(true);
            if (prepare is { Success: false })
                return;
        }

        await Task.Delay(150).ConfigureAwait(true);
        var window = await pluginManager.GetWindowAsync(pluginId).ConfigureAwait(true);
        if (window is null)
            return;

        EnsureHost();
        _externalWindowHost!.Attach(new ExternalWindowInfo(window.Hwnd, window.Title, 0, window.Descriptor.DisplayName));
        EmbedPlaceholder.Visibility = Visibility.Collapsed;
    }
}

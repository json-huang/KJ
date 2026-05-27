using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace KJ.WinUI.Hosting;

public sealed partial class ExternalWindowHost : UserControl, IDisposable
{
    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(ExternalWindowHost),
            new PropertyMetadata("未嵌入外部窗口"));

    public static readonly DependencyProperty DiagnosticsProperty =
        DependencyProperty.Register(nameof(Diagnostics), typeof(string), typeof(ExternalWindowHost),
            new PropertyMetadata("等待嵌入"));

    public static readonly DependencyProperty ShowChromeProperty =
        DependencyProperty.Register(nameof(ShowChrome), typeof(bool), typeof(ExternalWindowHost),
            new PropertyMetadata(true, OnShowChromeChanged));

    private IntPtr _hostedWindow;
    private IntPtr _containerWindow;
    private IntPtr _originalParent;
    private IntPtr _originalStyle;
    private IntPtr _originalExStyle;
    private FrameworkElement? _boundsTarget;
    private ExternalWindowInfo? _pendingWindow;
    private readonly DispatcherTimer _positionTimer;
    private bool _attached;
    private bool _disposed;

    public ExternalWindowHost()
    {
        InitializeComponent();
        ApplyChromeVisibility();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;

        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _positionTimer.Tick += (_, _) => ResizeHostedWindow();
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public string Diagnostics
    {
        get => (string)GetValue(DiagnosticsProperty);
        set => SetValue(DiagnosticsProperty, value);
    }

    public bool ShowChrome
    {
        get => (bool)GetValue(ShowChromeProperty);
        set => SetValue(ShowChromeProperty, value);
    }

    public IntPtr ParentWindowHandle { get; set; }

    /// <summary>嵌入区域定位目标；未设置时使用内部 HostSurface。</summary>
    public FrameworkElement? BoundsTarget
    {
        get => _boundsTarget;
        set
        {
            if (_boundsTarget is not null)
                _boundsTarget.SizeChanged -= OnBoundsTargetSizeChanged;

            _boundsTarget = value;

            if (_boundsTarget is not null)
                _boundsTarget.SizeChanged += OnBoundsTargetSizeChanged;
        }
    }

    /// <summary>
    /// 为 true 时容器为主窗口的子 HWND（客户区坐标）；为 false 时使用浮动 POPUP（屏幕坐标）。
    /// 插件中心页面应使用 true，避免盖住左侧 WinUI 控件。
    /// </summary>
    public bool EmbedAsChildWindow { get; set; }

    public ExternalWindowInfo? AttachedWindow { get; private set; }

    public bool IsAttached => _attached;

    public bool Attach(ExternalWindowInfo window)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ExternalWindowHost));

        _pendingWindow = window;
        if (!IsLoaded || ParentWindowHandle == IntPtr.Zero)
            return true;

        return AttachPendingWindow();
    }

    public void Detach() => DetachHostedWindow(restore: true);

    public void RefreshBounds() => ResizeHostedWindow();

    public void SetOverlayVisible(bool visible)
    {
        if (_containerWindow == IntPtr.Zero || !NativeMethods.IsWindow(_containerWindow))
            return;

        _ = NativeMethods.ShowWindow(_containerWindow, visible ? NativeMethods.SwShow : NativeMethods.SwHide);
        if (visible && _attached)
            ResizeHostedWindow();
    }

    private void OnReleaseClick(object sender, RoutedEventArgs e) => Detach();

    private static void OnShowChromeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ExternalWindowHost host)
            host.ApplyChromeVisibility();
    }

    private void ApplyChromeVisibility()
    {
        var showChrome = ShowChrome;
        var visibility = showChrome ? Visibility.Visible : Visibility.Collapsed;
        ToolbarPanel.Visibility = visibility;
        DiagnosticsText.Visibility = visibility;
        Root.RowDefinitions[0].Height = showChrome ? GridLength.Auto : new GridLength(0);
        Root.RowDefinitions[2].Height = showChrome ? GridLength.Auto : new GridLength(0);

        if (showChrome)
        {
            Root.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 11, 17, 24));
            HostSurface.Visibility = Visibility.Visible;
            Root.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            Opacity = 1;
            IsHitTestVisible = true;
            return;
        }

        // Popup 模式：WinUI 控件仅负责定位，不遮挡原生浮层
        Root.Background = null;
        HostSurface.Visibility = Visibility.Collapsed;
        Root.RowDefinitions[1].Height = new GridLength(0);
        Opacity = 0;
        IsHitTestVisible = false;
    }

    private void OnBoundsTargetSizeChanged(object sender, SizeChangedEventArgs e) => ResizeHostedWindow();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        BoundsTarget = null;
        DetachHostedWindow(restore: true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_pendingWindow is not null)
            AttachPendingWindow();
        else
            ResizeHostedWindow();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Docking can reparent the XAML control while dragging/floating. Do not detach here;
        // the owning page calls Dispose when the editor actually closes.
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ResizeHostedWindow();

    private bool AttachPendingWindow()
    {
        if (_pendingWindow is null || ParentWindowHandle == IntPtr.Zero)
        {
            UpdateDiagnostics("attach skipped");
            return false;
        }

        var window = _pendingWindow;
        _pendingWindow = null;

        if (!NativeMethods.IsWindow(window.Handle))
        {
            StatusText = "外部窗口已关闭";
            UpdateDiagnostics("target is not a valid window");
            return false;
        }

        DetachHostedWindow(restore: true);

        if (!EnsureContainerWindow())
        {
            StatusText = "无法创建外部窗口承载容器";
            UpdateDiagnostics("container create failed");
            return false;
        }

        _hostedWindow = window.Handle;
        _originalParent = NativeMethods.SetParent(_hostedWindow, _containerWindow);
        _originalStyle = NativeMethods.GetWindowLongPtr(_hostedWindow, NativeMethods.GwlStyle);
        _originalExStyle = NativeMethods.GetWindowLongPtr(_hostedWindow, NativeMethods.GwlExStyle);

        var style = _originalStyle.ToInt64();
        style &= ~(NativeMethods.WsPopup |
                   NativeMethods.WsCaption |
                   NativeMethods.WsThickFrame |
                   NativeMethods.WsSysMenu |
                   NativeMethods.WsMinimizeBox |
                   NativeMethods.WsMaximizeBox);
        style |= NativeMethods.WsChild | NativeMethods.WsVisible;

        _ = NativeMethods.SetWindowLongPtr(_hostedWindow, NativeMethods.GwlStyle, new IntPtr(style));

        var exStyle = _originalExStyle.ToInt64();
        exStyle &= ~NativeMethods.WsExAppWindow;
        _ = NativeMethods.SetWindowLongPtr(_hostedWindow, NativeMethods.GwlExStyle, new IntPtr(exStyle));

        _ = NativeMethods.ShowWindow(_hostedWindow, NativeMethods.SwRestore);
        _ = NativeMethods.ShowWindow(_hostedWindow, NativeMethods.SwShow);
        _ = NativeMethods.SetWindowPos(
            _hostedWindow,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged | NativeMethods.SwpShowWindow);

        _attached = true;
        AttachedWindow = window;
        Placeholder.Visibility = Visibility.Collapsed;
        StatusText = $"已嵌入：{window.Title}";
        _positionTimer.Start();
        ResizeHostedWindow();
        ScheduleBoundsRefresh();
        return true;
    }

    private void ScheduleBoundsRefresh()
    {
        if (DispatcherQueue is null)
            return;

        _ = DispatcherQueue.TryEnqueue(ResizeHostedWindow);
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => _ = RefreshBoundsDeferredAsync());
    }

    private async Task RefreshBoundsDeferredAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(80).ConfigureAwait(true);
            ResizeHostedWindow();
        }
    }

    private void DetachHostedWindow(bool restore)
    {
        if (!_attached || _hostedWindow == IntPtr.Zero)
            return;

        if (NativeMethods.IsWindow(_hostedWindow))
        {
            if (restore)
            {
                _ = NativeMethods.SetParent(_hostedWindow, _originalParent);
                _ = NativeMethods.SetWindowLongPtr(_hostedWindow, NativeMethods.GwlStyle, _originalStyle);
                _ = NativeMethods.SetWindowLongPtr(_hostedWindow, NativeMethods.GwlExStyle, _originalExStyle);
                _ = NativeMethods.SetWindowPos(
                    _hostedWindow,
                    IntPtr.Zero,
                    0,
                    0,
                    0,
                    0,
                    NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
                _ = NativeMethods.ShowWindow(_hostedWindow, NativeMethods.SwShow);
            }
        }

        _attached = false;
        _positionTimer.Stop();
        _hostedWindow = IntPtr.Zero;
        DestroyContainerWindow();
        _originalParent = IntPtr.Zero;
        _originalStyle = IntPtr.Zero;
        _originalExStyle = IntPtr.Zero;
        AttachedWindow = null;
        Placeholder.Visibility = Visibility.Visible;
        StatusText = "已释放外部窗口";
        UpdateDiagnostics("detached");
    }

    private void ResizeHostedWindow()
    {
        if (!_attached || _hostedWindow == IntPtr.Zero || !NativeMethods.IsWindow(_hostedWindow))
        {
            if (_attached)
            {
                _attached = false;
                _hostedWindow = IntPtr.Zero;
                Placeholder.Visibility = Visibility.Visible;
                StatusText = "外部窗口已关闭";
            }
            return;
        }

        if (!TryComputeHostBounds(out var posX, out var posY, out var width, out var height))
        {
            UpdateDiagnostics("resize skipped");
            return;
        }

        EnsureContainerWindow();

        var insertAfter = EmbedAsChildWindow ? IntPtr.Zero : NativeMethods.HwndTop;
        _ = NativeMethods.SetWindowPos(
            _containerWindow,
            insertAfter,
            posX,
            posY,
            width,
            height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        _ = NativeMethods.MoveWindow(_hostedWindow, 0, 0, width, height, true);
        NativeMethods.NotifyChildResized(_hostedWindow, width, height);
        UpdateDiagnostics(EmbedAsChildWindow ? "resized-child" : "resized-popup", posX, posY, width, height);
    }

    /// <summary>
    /// Computes host rectangle in physical pixels. Popup mode returns screen coordinates so the
    /// native layer draws above WinUI XAML; child mode returns main-window client coordinates.
    /// </summary>
    private bool TryComputeHostBounds(out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;

        var boundsElement = BoundsTarget ?? HostSurface;
        if (boundsElement.ActualWidth <= 0 || boundsElement.ActualHeight <= 0 || XamlRoot?.Content is not UIElement root)
            return false;

        var logical = boundsElement.TransformToVisual(root).TransformBounds(
            new Rect(0, 0, boundsElement.ActualWidth, boundsElement.ActualHeight));
        var scale = XamlRoot.RasterizationScale;

        var topLeft = new NativeMethods.Point
        {
            X = (int)Math.Round(logical.X * scale),
            Y = (int)Math.Round(logical.Y * scale),
        };
        var bottomRight = new NativeMethods.Point
        {
            X = (int)Math.Round((logical.X + logical.Width) * scale),
            Y = (int)Math.Round((logical.Y + logical.Height) * scale),
        };

        if (EmbedAsChildWindow)
        {
            x = topLeft.X;
            y = topLeft.Y;
            width = Math.Max(1, bottomRight.X - topLeft.X);
            height = Math.Max(1, bottomRight.Y - topLeft.Y);
            return true;
        }

        if (!NativeMethods.ClientToScreen(ParentWindowHandle, ref topLeft) ||
            !NativeMethods.ClientToScreen(ParentWindowHandle, ref bottomRight))
            return false;

        x = topLeft.X;
        y = topLeft.Y;
        width = Math.Max(1, bottomRight.X - topLeft.X);
        height = Math.Max(1, bottomRight.Y - topLeft.Y);
        return true;
    }

    private bool EnsureContainerWindow()
    {
        if (_containerWindow != IntPtr.Zero && NativeMethods.IsWindow(_containerWindow))
            return true;

        if (ParentWindowHandle == IntPtr.Zero)
            return false;

        var exStyle = EmbedAsChildWindow ? 0 : NativeMethods.WsExToolWindow;
        var style = EmbedAsChildWindow
            ? NativeMethods.WsChild | NativeMethods.WsVisible | NativeMethods.WsClipChildren | NativeMethods.WsClipSiblings
            : NativeMethods.WsPopup | NativeMethods.WsVisible | NativeMethods.WsClipChildren | NativeMethods.WsClipSiblings;

        _containerWindow = NativeMethods.CreateWindowExW(
            exStyle,
            "STATIC",
            null,
            style,
            0,
            0,
            1,
            1,
            ParentWindowHandle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        UpdateDiagnostics(_containerWindow == IntPtr.Zero ? "container create failed" : "container created");
        return _containerWindow != IntPtr.Zero;
    }

    private void DestroyContainerWindow()
    {
        if (_containerWindow == IntPtr.Zero)
            return;

        if (NativeMethods.IsWindow(_containerWindow))
            _ = NativeMethods.DestroyWindow(_containerWindow);

        _containerWindow = IntPtr.Zero;
    }

    private void UpdateDiagnostics(string phase, int? x = null, int? y = null, int? width = null, int? height = null)
    {
        Diagnostics =
            $"phase={phase}; " +
            $"parent=0x{ParentWindowHandle.ToInt64():X}; " +
            $"container=0x{_containerWindow.ToInt64():X}; " +
            $"target=0x{_hostedWindow.ToInt64():X}; " +
            $"originalParent=0x{_originalParent.ToInt64():X}; " +
            $"attached={_attached}; " +
            $"validTarget={(_hostedWindow != IntPtr.Zero && NativeMethods.IsWindow(_hostedWindow))}; " +
            $"bounds={x?.ToString() ?? "-"}:{y?.ToString() ?? "-"}:{width?.ToString() ?? "-"}:{height?.ToString() ?? "-"}";
    }
}

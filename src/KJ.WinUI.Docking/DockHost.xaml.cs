using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace KJ.WinUI.Docking;

public sealed partial class DockHost : UserControl
{
    public static readonly DependencyProperty MainContentProperty =
        DependencyProperty.Register(nameof(MainContent), typeof(UIElement), typeof(DockHost),
            new PropertyMetadata(null, OnMainContentChanged));

    public static readonly DependencyProperty PaneTitleProperty =
        DependencyProperty.Register(nameof(PaneTitle), typeof(string), typeof(DockHost),
            new PropertyMetadata("属性", OnPaneTitleChanged));

    private readonly DockPane _pane = new();
    private UIElement? _paneContent;
    private DockPosition _dockPosition = DockPosition.Right;
    private bool _isHeaderPointerDown;
    private bool _isFloating;
    private bool _isDraggingFloating;
    private Point _dragStart;
    private Point _dragOffset;
    private DockPosition? _previewPosition;

    public DockHost()
    {
        InitializeComponent();
        _pane.Title = PaneTitle;
        _pane.FloatRequested += (_, _) => FloatPane();
        _pane.CloseRequested += (_, _) => CollapsePane();
        _pane.AutoHideRequested += (_, _) => CollapsePane();
        _pane.HeaderPointerPressed += OnPaneHeaderPointerPressed;
        _pane.HeaderPointerMoved += OnPaneHeaderPointerMoved;
        _pane.HeaderPointerReleased += OnPaneHeaderPointerReleased;
        PointerMoved += OnHostPointerMoved;
        PointerReleased += OnHostPointerReleased;
        PointerCanceled += OnHostPointerReleased;
    }

    public UIElement? MainContent
    {
        get => (UIElement?)GetValue(MainContentProperty);
        set => SetValue(MainContentProperty, value);
    }

    public string PaneTitle
    {
        get => (string)GetValue(PaneTitleProperty);
        set => SetValue(PaneTitleProperty, value);
    }

    public void SetMainContent(UIElement content) => MainHost.Content = content;

    public bool IsPaneOpen => CollapsedTab.Visibility != Visibility.Visible;

    public bool IsPaneFloating => _isFloating && FloatingLayer.Visibility == Visibility.Visible;

    public void SetPaneContent(UIElement content)
    {
        _paneContent = content;
        _pane.SetPaneContent(content);
        DockPane(DockPosition.Right);
    }

    /// <summary>以浮动窗口显示属性/工具面板（不占用停靠位）。</summary>
    public void ShowPaneFloating()
    {
        if (_paneContent is null)
            return;

        if (_isFloating && FloatingLayer.Visibility == Visibility.Visible)
            return;

        if (IsPaneOpen && !_isFloating)
            FloatPane();
        else
            RestoreAndFloat();
    }

    /// <summary>停靠显示面板（默认右侧）。</summary>
    public void ShowPaneDocked(DockPosition position = DockPosition.Right)
    {
        if (_paneContent is null)
            return;

        EnsurePaneContent();
        DockPane(position);
    }

    /// <summary>关闭/收起面板。</summary>
    public void HidePane() => CollapsePane();

    public void TogglePaneFloating()
    {
        if (IsPaneOpen)
            HidePane();
        else
            ShowPaneFloating();
    }

    private void EnsurePaneContent()
    {
        if (_paneContent is not null)
            _pane.SetPaneContent(_paneContent);
    }

    private void RestoreAndFloat()
    {
        ClearDockHosts();
        CollapsedTab.Visibility = Visibility.Collapsed;
        _isFloating = false;
        Overlay.Hide();

        _pane.SetPaneContent(_paneContent);
        ApplyFloatingPaneSize();
        FloatingLayer.Children.Clear();
        FloatingLayer.Children.Add(_pane);
        FloatingLayer.Visibility = Visibility.Visible;
        Canvas.SetLeft(_pane, Math.Max(24, (ActualWidth - _pane.Width) / 2));
        Canvas.SetTop(_pane, Math.Max(24, (ActualHeight - _pane.Height) / 2));
        _isFloating = true;
    }

    private static void OnMainContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockHost host)
            host.MainHost.Content = e.NewValue as UIElement;
    }

    private static void OnPaneTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockHost host && e.NewValue is string title)
            host._pane.Title = title;
    }

    private void FloatPane()
    {
        if (_isFloating)
            return;

        var content = _pane.TakePaneContent() ?? _paneContent;
        if (content is null)
            return;

        _paneContent = content;
        _isFloating = true;
        ClearDockHosts();
        _pane.SetPaneContent(content);
        ApplyFloatingPaneSize();
        FloatingLayer.Children.Clear();
        FloatingLayer.Children.Add(_pane);
        FloatingLayer.Visibility = Visibility.Visible;
        Canvas.SetLeft(_pane, Math.Max(24, (ActualWidth - _pane.Width) / 2));
        Canvas.SetTop(_pane, Math.Max(24, (ActualHeight - _pane.Height) / 2));
        Overlay.Show("拖到中间指南针或边缘，松手停靠");
    }

    private void FloatPaneAt(Point pointer)
    {
        if (_isFloating)
            return;

        var content = _pane.TakePaneContent() ?? _paneContent;
        if (content is null)
            return;

        _paneContent = content;
        _isFloating = true;
        ClearDockHosts();
        _pane.SetPaneContent(content);
        ApplyFloatingPaneSize();
        FloatingLayer.Children.Clear();
        FloatingLayer.Children.Add(_pane);
        FloatingLayer.Visibility = Visibility.Visible;
        Canvas.SetLeft(_pane, Math.Clamp(pointer.X - 72, 0, Math.Max(0, ActualWidth - _pane.Width)));
        Canvas.SetTop(_pane, Math.Clamp(pointer.Y - 17, 0, Math.Max(0, ActualHeight - _pane.Height)));
        Overlay.Show("拖到中间指南针或边缘，松手停靠");
    }

    private void DockFloatingPane(DockPosition position)
    {
        _paneContent = _pane.TakePaneContent() ?? _paneContent;
        FloatingLayer.Children.Clear();
        FloatingLayer.Visibility = Visibility.Collapsed;
        _isFloating = false;
        _isDraggingFloating = false;
        _previewPosition = null;
        Overlay.Hide();
        _pane.Width = double.NaN;
        _pane.Height = double.NaN;
        DockPane(position);
    }

    private void DockPane(DockPosition position)
    {
        _dockPosition = position;
        _isFloating = false;
        CollapsedTab.Visibility = Visibility.Collapsed;
        FloatingLayer.Children.Clear();
        FloatingLayer.Visibility = Visibility.Collapsed;
        ClearDockHosts();
        _pane.SetPaneContent(_paneContent);

        switch (position)
        {
            case DockPosition.Left:
                LeftColumn.Width = new GridLength(320);
                RightColumn.Width = new GridLength(0);
                BottomRow.Height = new GridLength(0);
                LeftHost.Content = _pane;
                break;
            case DockPosition.Bottom:
                LeftColumn.Width = new GridLength(0);
                RightColumn.Width = new GridLength(0);
                BottomRow.Height = new GridLength(260);
                BottomHost.Content = _pane;
                break;
            default:
                LeftColumn.Width = new GridLength(0);
                RightColumn.Width = new GridLength(320);
                BottomRow.Height = new GridLength(0);
                RightHost.Content = _pane;
                break;
        }
    }

    private void CollapsePane()
    {
        if (_isFloating)
        {
            _paneContent = _pane.TakePaneContent() ?? _paneContent;
            FloatingLayer.Children.Clear();
            FloatingLayer.Visibility = Visibility.Collapsed;
            _isFloating = false;
            Overlay.Hide();
        }
        else
        {
            _paneContent = _pane.TakePaneContent() ?? _paneContent;
        }

        ClearDockHosts();
        LeftColumn.Width = new GridLength(0);
        RightColumn.Width = new GridLength(36);
        BottomRow.Height = new GridLength(0);
        CollapsedTab.Visibility = Visibility.Visible;
    }

    private void OnCollapsedTabClick(object sender, RoutedEventArgs e) => DockPane(_dockPosition);

    private void ClearDockHosts()
    {
        LeftHost.Content = null;
        RightHost.Content = null;
        BottomHost.Content = null;
    }

    private void ApplyFloatingPaneSize()
    {
        _pane.Width = 360;
        _pane.Height = ActualHeight > 120 ? Math.Max(320, ActualHeight - 16) : 520;
        _pane.VerticalAlignment = VerticalAlignment.Stretch;
        _pane.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private void OnPaneHeaderPointerPressed(DockPane pane, PointerRoutedEventArgs e)
    {
        _isHeaderPointerDown = true;
        _dragStart = e.GetCurrentPoint(this).Position;
        CapturePointer(e.Pointer);
        if (_isFloating)
            BeginFloatingDrag(e.GetCurrentPoint(FloatingLayer).Position);

        e.Handled = true;
    }

    private void OnPaneHeaderPointerMoved(DockPane pane, PointerRoutedEventArgs e) => HandlePointerMoved(e);

    private void OnHostPointerMoved(object sender, PointerRoutedEventArgs e) => HandlePointerMoved(e);

    private void HandlePointerMoved(PointerRoutedEventArgs e)
    {
        if (!_isHeaderPointerDown)
            return;

        var pointer = e.GetCurrentPoint(FloatingLayer).Position;
        if (!_isFloating)
        {
            var current = e.GetCurrentPoint(this).Position;
            if (Math.Abs(current.X - _dragStart.X) + Math.Abs(current.Y - _dragStart.Y) < 14)
                return;

            FloatPaneAt(pointer);
            BeginFloatingDrag(pointer, new Point(Math.Min(72, _pane.Width / 2), 17));
        }

        if (!_isDraggingFloating)
            return;

        MoveFloatingPane(pointer);
        e.Handled = true;
    }

    private void OnPaneHeaderPointerReleased(DockPane pane, PointerRoutedEventArgs e) => FinishPointerDrag(e);

    private void OnHostPointerReleased(object sender, PointerRoutedEventArgs e) => FinishPointerDrag(e);

    private void FinishPointerDrag(PointerRoutedEventArgs e)
    {
        _isHeaderPointerDown = false;
        ReleasePointerCapture(e.Pointer);
        if (!_isFloating)
            return;

        _isDraggingFloating = false;
        if (_previewPosition is DockPosition position)
        {
            DockFloatingPane(position);
            e.Handled = true;
            return;
        }

        Overlay.Show("浮动：拖到指南针或边缘可停靠");
        e.Handled = true;
    }

    private void BeginFloatingDrag(Point pointer, Point? dragOffset = null)
    {
        _isDraggingFloating = true;
        _dragOffset = dragOffset ?? new Point(pointer.X - Canvas.GetLeft(_pane), pointer.Y - Canvas.GetTop(_pane));
    }

    private void MoveFloatingPane(Point pointer)
    {
        var left = Math.Clamp(pointer.X - _dragOffset.X, 0, Math.Max(0, ActualWidth - _pane.ActualWidth));
        var top = Math.Clamp(pointer.Y - _dragOffset.Y, 0, Math.Max(0, ActualHeight - _pane.ActualHeight));
        Canvas.SetLeft(_pane, left);
        Canvas.SetTop(_pane, top);

        _previewPosition = GetPreviewPosition(pointer, left, top);
        if (_previewPosition is DockPosition position)
            Overlay.Show(position switch
            {
                DockPosition.Left => "停靠到左侧",
                DockPosition.Bottom => "停靠到底部",
                _ => "停靠到右侧",
            }, position);
        else
            Overlay.Show("浮动：拖到指南针或边缘可停靠");
    }

    private DockPosition? GetPreviewPosition(Point pointer, double paneLeft, double paneTop)
    {
        const double edge = 96;
        var compassPosition = GetCompassPreviewPosition(pointer);
        if (compassPosition is not null)
            return compassPosition;

        var paneRight = paneLeft + _pane.ActualWidth;
        var paneBottom = paneTop + _pane.ActualHeight;

        if (pointer.X <= edge || paneLeft <= edge)
            return DockPosition.Left;
        if (pointer.X >= ActualWidth - edge || paneRight >= ActualWidth - edge)
            return DockPosition.Right;
        if (pointer.Y >= ActualHeight - edge || paneBottom >= ActualHeight - edge)
            return DockPosition.Bottom;
        return null;
    }

    private DockPosition? GetCompassPreviewPosition(Point pointer)
    {
        const double compassSize = 160;
        const double cell = compassSize / 3;
        var left = (ActualWidth - compassSize) / 2;
        var top = (ActualHeight - compassSize) / 2;

        if (pointer.X < left || pointer.X > left + compassSize || pointer.Y < top || pointer.Y > top + compassSize)
            return null;

        var column = (int)((pointer.X - left) / cell);
        var row = (int)((pointer.Y - top) / cell);
        return (row, column) switch
        {
            (1, 0) => DockPosition.Left,
            (1, 2) => DockPosition.Right,
            (2, 1) => DockPosition.Bottom,
            _ => null,
        };
    }
}

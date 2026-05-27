using KJ.Modules.Core.Diagnostics;
using KJ.Modules.Monitoring.ViewModels;
using KJ.Modules.Monitoring.Workflows;
using KJ.WinUI.Hosting;
using KJ.Workflows;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.UI.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class WorkflowEditorPage : Page
{
    private const double NodeWidth = 220;
    private const double NodeHeight = 68;
    private const double PortHostSize = 26;
    private const double PortDotSize = 10;
    private const double PortHitRadius = 22;
    private const float CanvasMinZoom = 0.25f;
    private const float CanvasMaxZoom = 5f;
    private float _canvasZoom = 1f;
    private const double CanvasWorldInset = 48;
    private double _canvasPadLeft = CanvasWorldInset;
    private double _canvasPadTop = CanvasWorldInset;
    private double _contentMinX;
    private double _contentMinY;
    private const double CanvasFitMarginPx = 56;
    private const double CanvasRouteBoundsPad =
        WorkflowGridLinkRouter.PortStubLength + WorkflowGridLinkRouter.NodeClearance + 64;
    private const double CanvasNodeMinX = -4000;
    private const double CanvasNodeMinY = -4000;
    private const double CanvasNodeMaxX = 20000;
    private const double CanvasNodeMaxY = 20000;

    private static double ClampNodeX(double x) => Math.Clamp(x, CanvasNodeMinX, CanvasNodeMaxX);

    private static double ClampNodeY(double y) => Math.Clamp(y, CanvasNodeMinY, CanvasNodeMaxY);

    /// <summary>未选中节点：淡蓝描边。</summary>
    private static readonly SolidColorBrush NodeDefaultBorderBrush =
        new(Windows.UI.Color.FromArgb(255, 75, 123, 181));

    /// <summary>选中节点：高亮蓝（#3B82F6）。</summary>
    private static readonly SolidColorBrush NodeSelectedBorderBrush =
        new(Windows.UI.Color.FromArgb(255, 59, 130, 246));

    private static readonly SolidColorBrush NodeRunningBrush =
        new(Windows.UI.Color.FromArgb(255, 74, 222, 128));

    private static readonly SolidColorBrush PortDefaultBrush =
        new(Windows.UI.Color.FromArgb(255, 75, 123, 181));

    private static readonly SolidColorBrush PortActiveBrush =
        new(Windows.UI.Color.FromArgb(255, 59, 130, 246));

    /// <summary>连线：与节点边框区分的中性色。</summary>
    private static readonly SolidColorBrush LinkStrokeBrush =
        new(Windows.UI.Color.FromArgb(255, 148, 163, 184));

    private static readonly SolidColorBrush LinkPreviewStrokeBrush =
        new(Windows.UI.Color.FromArgb(255, 186, 198, 212));

    private WorkflowStep? _dragging;
    private Border? _draggingBorder;
    private readonly Dictionary<Guid, (double X, double Y)> _dragOriginById = new();
    private readonly List<WorkflowStep> _draggingSteps = new();
    private readonly Dictionary<Guid, Border> _nodeById = new();
    private readonly List<PortVisual> _ports = new();
    private ViewModels.WorkflowEditorViewModel? _hookedVm;
    private Windows.Foundation.Point _dragStart;
    private bool _redrawQueued;
    private bool _viewportGridQueued;
    private ExternalWindowHost? _externalWindowHost;
    private bool _logPanelVisible = true;

    private bool _isLinkDragging;
    private PortVisual? _linkFromPort;
    private PortVisual? _linkSnapTarget;
    private Polyline? _linkPreviewPolyline;
    private UIElement? _linkCaptureTarget;

    private bool _isCanvasPanning;
    private uint _panPointerId;
    private Windows.Foundation.Point _panPointerStart;
    private double _panScrollStartX;
    private double _panScrollStartY;
    private InputCursor? _canvasCursorBeforePan;
    private UIElement? _panCaptureTarget;

    private WorkflowToolboxItem? _toolboxDragItem;
    private Border? _toolboxDragGhost;
    private bool _toolboxPointerHandlersActive;

    private bool _isMarqueeSelecting;
    private uint _marqueePointerId;
    private Windows.Foundation.Point _marqueeStartWorld;
    private Rectangle? _marqueeRect;

    private sealed class PortVisual
    {
        public required WorkflowStep Step { get; init; }
        public required WorkflowPort Port { get; init; }
        public required Grid Host { get; init; }
        public required Ellipse Dot { get; init; }
    }

    public WorkflowEditorPage(ViewModels.WorkflowEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        DataContextChanged += (_, _) => HookVm();
        EditorDockHost.PaneTitle = "属性";
        EditorDockHost.SetPaneContent(new WorkflowEditorPropertiesPanel { DataContext = viewModel });
        EditorLogPanel.CloseRequested += (_, _) => SetLogPanelVisible(false);
        WireCanvasDropTargets();
        WireCanvasZoomWheel();
        WireLinkDragPointerHandlers();
        WireCanvasPanHandlers();
        CanvasHost.SizeChanged += (_, _) =>
        {
            SyncCanvasScrollExtent();
            QueueRedrawViewportGrid();
        };
        CanvasScroller.SizeChanged += (_, _) =>
        {
            SyncCanvasScrollExtent();
            QueueRedrawViewportGrid();
        };
        ViewportGridHost.SizeChanged += (_, _) => QueueRedrawViewportGrid();
        ApplyCanvasZoom(_canvasZoom, resetScroll: false);
    }

    private void OnCanvasScrollerLoaded(object sender, RoutedEventArgs e) =>
        MakeScrollViewerTransparent(CanvasScroller);

    private void OnCanvasViewChanged(object sender, ScrollViewerViewChangedEventArgs e) => QueueRedrawViewportGrid();

    private void WireCanvasZoomWheel()
    {
        var handler = new PointerEventHandler(OnCanvasPointerWheelChanged);
        CanvasHost.AddHandler(UIElement.PointerWheelChangedEvent, handler, handledEventsToo: true);
        CanvasScroller.AddHandler(UIElement.PointerWheelChangedEvent, handler, handledEventsToo: true);
        CanvasDropRoot.AddHandler(UIElement.PointerWheelChangedEvent, handler, handledEventsToo: true);
        CanvasWorld.AddHandler(UIElement.PointerWheelChangedEvent, handler, handledEventsToo: true);
    }

    private void OnCanvasPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
            return;

        var delta = e.GetCurrentPoint(CanvasScroller).Properties.MouseWheelDelta;
        if (delta == 0)
            return;

        var factor = delta > 0 ? 1.12f : 1f / 1.12f;
        ApplyCanvasZoom(_canvasZoom * factor, resetScroll: false);
        e.Handled = true;
    }

    private void UpdateZoomLabel()
    {
        if (ZoomLabel is null)
            return;

        ZoomLabel.Text = $"{_canvasZoom * 100:0}%";
    }

    private void ApplyCanvasZoom(float zoom, bool resetScroll)
    {
        _canvasZoom = Math.Clamp(zoom, CanvasMinZoom, CanvasMaxZoom);
        CanvasScaleTransform.ScaleX = _canvasZoom;
        CanvasScaleTransform.ScaleY = _canvasZoom;
        ApplyCanvasWorldPadding();
        SyncCanvasScrollExtent();

        if (resetScroll)
            _ = CanvasScroller.ChangeView(0, 0, null, disableAnimation: true);

        UpdateZoomLabel();
        QueueRedrawViewportGrid();
    }

    private double LayerOffsetX => _contentMinX < 0 ? -_contentMinX : 0;

    private double LayerOffsetY => _contentMinY < 0 ? -_contentMinY : 0;

    private void ApplyCanvasWorldPadding()
    {
        CanvasWorldTranslate.X = _canvasPadLeft;
        CanvasWorldTranslate.Y = _canvasPadTop;

        var offsetX = LayerOffsetX;
        var offsetY = LayerOffsetY;
        Canvas.SetLeft(NodesLayer, offsetX);
        Canvas.SetTop(NodesLayer, offsetY);
        Canvas.SetLeft(LinkLayer, offsetX);
        Canvas.SetTop(LinkLayer, offsetY);
        Canvas.SetLeft(PortsLayer, offsetX);
        Canvas.SetTop(PortsLayer, offsetY);
        Canvas.SetLeft(LinkPreviewLayer, offsetX);
        Canvas.SetTop(LinkPreviewLayer, offsetY);
    }

    private void SyncCanvasScrollExtent()
    {
        var contentW = NodesLayer.Width > 0 ? NodesLayer.Width : 1400;
        var contentH = NodesLayer.Height > 0 ? NodesLayer.Height : 720;
        var logicalW = _canvasPadLeft + LayerOffsetX + contentW;
        var logicalH = _canvasPadTop + LayerOffsetY + contentH;
        var pad = CanvasScroller.Padding;
        var viewW = Math.Max(1, CanvasScroller.ActualWidth - pad.Left - pad.Right);
        var viewH = Math.Max(1, CanvasScroller.ActualHeight - pad.Top - pad.Bottom);
        var scaledW = logicalW * _canvasZoom;
        var scaledH = logicalH * _canvasZoom;
        CanvasWorld.Width = logicalW;
        CanvasWorld.Height = logicalH;
        CanvasDropRoot.Width = Math.Max(scaledW, viewW);
        CanvasDropRoot.Height = Math.Max(scaledH, viewH);
    }

    private bool TryGetCanvasViewportMetrics(
        out double vw,
        out double vh,
        out double scrollX,
        out double scrollY,
        out double originX,
        out double originY,
        out double clientW,
        out double clientH)
    {
        vw = ViewportGridHost.ActualWidth;
        vh = ViewportGridHost.ActualHeight;
        if (vw < 1 || vh < 1)
        {
            vw = CanvasScroller.ActualWidth;
            vh = CanvasScroller.ActualHeight;
        }

        if (vw < 1 || vh < 1)
        {
            originX = originY = clientW = clientH = scrollX = scrollY = 0;
            return false;
        }

        var pad = CanvasScroller.Padding;
        scrollX = CanvasScroller.HorizontalOffset;
        scrollY = CanvasScroller.VerticalOffset;
        clientW = vw - pad.Left - pad.Right;
        clientH = vh - pad.Top - pad.Bottom;

        var extentW = CanvasWorld.Width * _canvasZoom;
        var extentH = CanvasWorld.Height * _canvasZoom;
        originX = pad.Left + Math.Max(0, (clientW - extentW) * 0.5);
        originY = pad.Top + Math.Max(0, (clientH - extentH) * 0.5);
        return true;
    }

    private double WorldToScrollX(double worldX) =>
        _canvasPadLeft + LayerOffsetX + worldX;

    private double WorldToScrollY(double worldY) =>
        _canvasPadTop + LayerOffsetY + worldY;

    private void ScrollToRevealWorldPoint(double worldX, double worldY)
    {
        if (!TryGetCanvasViewportMetrics(
                out _,
                out _,
                out var scrollX,
                out var scrollY,
                out var originX,
                out var originY,
                out var clientW,
                out var clientH))
            return;

        const double margin = 32;
        var zoom = _canvasZoom;
        var nodeScreenX = originX + WorldToScrollX(worldX) * zoom - scrollX;
        var nodeScreenY = originY + WorldToScrollY(worldY) * zoom - scrollY;
        var nodeScreenRight = nodeScreenX + NodeWidth * zoom;
        var nodeScreenBottom = nodeScreenY + NodeHeight * zoom;

        var targetScrollX = scrollX;
        var targetScrollY = scrollY;

        if (nodeScreenX < margin)
            targetScrollX = Math.Max(0, originX + WorldToScrollX(worldX) * zoom - margin);
        else if (nodeScreenRight > clientW - margin)
            targetScrollX = Math.Max(0, originX + WorldToScrollX(worldX) * zoom + NodeWidth * zoom - clientW + margin);

        if (nodeScreenY < margin)
            targetScrollY = Math.Max(0, originY + WorldToScrollY(worldY) * zoom - margin);
        else if (nodeScreenBottom > clientH - margin)
            targetScrollY = Math.Max(0, originY + WorldToScrollY(worldY) * zoom + NodeHeight * zoom - clientH + margin);

        if (Math.Abs(targetScrollX - scrollX) > 0.5 || Math.Abs(targetScrollY - scrollY) > 0.5)
            _ = CanvasScroller.ChangeView(targetScrollX, targetScrollY, null, disableAnimation: true);
    }

    private static void MakeScrollViewerTransparent(ScrollViewer scrollViewer)
    {
        scrollViewer.Background = null;

        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(scrollViewer);
        for (var i = 0; i < count; i++)
        {
            ClearBackgrounds(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(scrollViewer, i));
        }
    }

    private static void ClearBackgrounds(DependencyObject node)
    {
        switch (node)
        {
            case ScrollContentPresenter presenter:
                presenter.Background = null;
                break;
            case Panel panel:
                panel.Background = null;
                break;
            case Control control:
                control.Background = null;
                break;
        }

        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
            ClearBackgrounds(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e) =>
        ApplyCanvasZoom(_canvasZoom * 1.2f, resetScroll: false);

    private void OnZoomOutClick(object sender, RoutedEventArgs e) =>
        ApplyCanvasZoom(_canvasZoom / 1.2f, resetScroll: false);

    private void OnZoomResetClick(object sender, RoutedEventArgs e) =>
        ApplyCanvasZoom(1f, resetScroll: true);

    private void OnZoomFitClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkflowEditorViewModel vm || vm.Steps.Count == 0)
        {
            ApplyCanvasZoom(1f, resetScroll: true);
            return;
        }

        if (!TryGetCanvasViewportMetrics(out _, out _, out _, out _, out _, out _, out var clientW, out var clientH))
            return;

        UpdateCanvasExtent(vm);

        if (!TryGetWorkflowContentBounds(vm, out var minX, out var minY, out var maxX, out var maxY))
            return;

        var contentW = Math.Max(480, maxX - minX + CanvasRouteBoundsPad * 2);
        var contentH = Math.Max(360, maxY - minY + CanvasRouteBoundsPad * 2);

        var zoom = (float)Math.Clamp(Math.Min(clientW / contentW, clientH / contentH), CanvasMinZoom, CanvasMaxZoom);
        ApplyCanvasZoom(zoom, resetScroll: false);

        if (!TryGetCanvasViewportMetrics(out _, out _, out _, out _, out var originX, out var originY, out _, out _))
            return;

        var scrollX = Math.Max(0, originX + WorldToScrollX(minX) * _canvasZoom - CanvasFitMarginPx);
        var scrollY = Math.Max(0, originY + WorldToScrollY(minY) * _canvasZoom - CanvasFitMarginPx);
        _ = CanvasScroller.ChangeView(scrollX, scrollY, null, disableAnimation: true);
        QueueRedrawViewportGrid();
    }

    private bool TryGetWorkflowContentBounds(
        WorkflowEditorViewModel vm,
        out double minX,
        out double minY,
        out double maxX,
        out double maxY)
    {
        minX = minY = maxX = maxY = 0;
        var boundsMinX = double.PositiveInfinity;
        var boundsMinY = double.PositiveInfinity;
        var boundsMaxX = double.NegativeInfinity;
        var boundsMaxY = double.NegativeInfinity;

        foreach (var step in vm.Steps)
        {
            boundsMinX = Math.Min(boundsMinX, step.X);
            boundsMinY = Math.Min(boundsMinY, step.Y);
            boundsMaxX = Math.Max(boundsMaxX, step.X + NodeWidth);
            boundsMaxY = Math.Max(boundsMaxY, step.Y + NodeHeight);
        }

        if (vm.Steps.Count == 0)
            return false;

        var obstacles = BuildObstacles(vm);
        var reserved = new HashSet<WorkflowGridLinkRouter.GridCell>();
        var lane = 0;
        foreach (var link in vm.Links
                     .OrderBy(l => l.FromStepId)
                     .ThenBy(l => l.ToStepId)
                     .ThenBy(l => l.FromPort)
                     .ThenBy(l => l.ToPort))
        {
            var from = vm.Steps.FirstOrDefault(x => x.Id == link.FromStepId);
            var to = vm.Steps.FirstOrDefault(x => x.Id == link.ToStepId);
            if (from is null || to is null)
                continue;

            var start = GetPortPoint(from, link.FromPort, NodeWidth, NodeHeight);
            var end = GetPortPoint(to, link.ToPort, NodeWidth, NodeHeight);
            var path = WorkflowGridLinkRouter.Route(
                start,
                link.FromPort,
                end,
                link.ToPort,
                obstacles,
                from.Id,
                to.Id,
                reserved,
                lane++);

            WorkflowGridLinkRouter.ReservePathCells(path, reserved);
            foreach (var point in path)
            {
                boundsMinX = Math.Min(boundsMinX, point.X);
                boundsMinY = Math.Min(boundsMinY, point.Y);
                boundsMaxX = Math.Max(boundsMaxX, point.X);
                boundsMaxY = Math.Max(boundsMaxY, point.Y);
            }
        }

        if (double.IsInfinity(boundsMinX))
            return false;

        boundsMinX -= CanvasRouteBoundsPad;
        boundsMinY -= CanvasRouteBoundsPad;
        boundsMaxX += CanvasRouteBoundsPad;
        boundsMaxY += CanvasRouteBoundsPad;

        minX = boundsMinX;
        minY = boundsMinY;
        maxX = boundsMaxX;
        maxY = boundsMaxY;
        return true;
    }

    private void OnToggleLogPanelClick(object sender, RoutedEventArgs e) => SetLogPanelVisible(!_logPanelVisible);

    private void SetLogPanelVisible(bool visible)
    {
        _logPanelVisible = visible;
        LogPanelRow.Height = visible ? new GridLength(200) : new GridLength(0);
        EditorLogPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void WireCanvasDropTargets()
    {
        void Wire(UIElement element)
        {
            element.AllowDrop = true;
            element.DragOver += OnCanvasDragOver;
            element.Drop += OnCanvasDrop;
        }

        Wire(CanvasHost);
        Wire(CanvasScroller);
        Wire(CanvasDropRoot);
        Wire(CanvasWorld);
        Wire(LinkLayer);
        Wire(NodesLayer);
        Wire(PortsLayer);
    }

    private void OnCanvasDragOver(object sender, DragEventArgs e) =>
        e.AcceptedOperation = DataPackageOperation.None;

    private void OnCanvasDrop(object sender, DragEventArgs e)
    {
    }

    private void WireLinkDragPointerHandlers()
    {
        CanvasScroller.PointerMoved += OnLinkDragPointerMoved;
        CanvasScroller.PointerReleased += OnLinkDragPointerReleased;
        CanvasScroller.PointerCanceled += OnLinkDragPointerCanceled;
    }

    private void WireCanvasPanHandlers()
    {
        var pressed = new PointerEventHandler(OnCanvasPanPointerPressed);
        var moved = new PointerEventHandler(OnCanvasPanPointerMoved);
        var released = new PointerEventHandler(OnCanvasPanPointerReleased);
        var canceled = new PointerEventHandler(OnCanvasPanPointerCanceled);

        void Wire(UIElement element)
        {
            element.AddHandler(UIElement.PointerPressedEvent, pressed, handledEventsToo: true);
            element.AddHandler(UIElement.PointerMovedEvent, moved, handledEventsToo: true);
            element.AddHandler(UIElement.PointerReleasedEvent, released, handledEventsToo: true);
            element.AddHandler(UIElement.PointerCanceledEvent, canceled, handledEventsToo: true);
        }

        Wire(CanvasHost);
        Wire(CanvasScroller);
    }

    private void OnCanvasPanPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isMarqueeSelecting || _isLinkDragging || _dragging is not null || _isCanvasPanning)
            return;

        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse &&
            e.GetCurrentPoint(null).Properties.IsRightButtonPressed)
            return;

        if (!e.GetCurrentPoint(CanvasDropRoot).Properties.IsLeftButtonPressed)
            return;

        if (!IsCanvasBackgroundHit(e.OriginalSource as DependencyObject))
            return;

        // Ctrl/Shift + drag on blank canvas => marquee select (do not pan)
        if (IsCtrlDown() || IsShiftDown())
        {
            BeginMarqueeSelect(e);
            e.Handled = true;
            return;
        }

        _isCanvasPanning = true;
        _panPointerId = e.Pointer.PointerId;
        _panPointerStart = e.GetCurrentPoint(CanvasScroller).Position;
        _panScrollStartX = CanvasScroller.HorizontalOffset;
        _panScrollStartY = CanvasScroller.VerticalOffset;
        _canvasCursorBeforePan = ProtectedCursor;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
        _panCaptureTarget = CanvasHost;
        CanvasHost.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnCanvasPanPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isMarqueeSelecting && e.Pointer.PointerId == _marqueePointerId)
        {
            UpdateMarqueeSelect(e);
            e.Handled = true;
            return;
        }

        if (_isCanvasPanning && e.Pointer.PointerId == _panPointerId)
        {
            var pos = e.GetCurrentPoint(CanvasScroller).Position;
            var dx = pos.X - _panPointerStart.X;
            var dy = pos.Y - _panPointerStart.Y;
            var maxScrollX = Math.Max(0, CanvasScroller.ScrollableWidth);
            var maxScrollY = Math.Max(0, CanvasScroller.ScrollableHeight);
            var scrollX = Math.Clamp(_panScrollStartX - dx, 0, maxScrollX);
            var scrollY = Math.Clamp(_panScrollStartY - dy, 0, maxScrollY);
            _ = CanvasScroller.ChangeView(scrollX, scrollY, null, disableAnimation: true);
            e.Handled = true;
            return;
        }

        if (ReferenceEquals(sender, CanvasScroller) && _isLinkDragging)
            PortHost_PointerMoved(sender, e);
    }

    private void OnCanvasPanPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isMarqueeSelecting && e.Pointer.PointerId == _marqueePointerId)
        {
            EndMarqueeSelect(commit: true, e);
            e.Handled = true;
            return;
        }

        if (_isCanvasPanning && e.Pointer.PointerId == _panPointerId)
        {
            EndCanvasPan(e.Pointer);
            e.Handled = true;
            return;
        }

        if (ReferenceEquals(sender, CanvasScroller) && _isLinkDragging)
            PortHost_PointerReleased(sender, e);
    }

    private void OnCanvasPanPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_isMarqueeSelecting && e.Pointer.PointerId == _marqueePointerId)
        {
            EndMarqueeSelect(commit: false, e);
            e.Handled = true;
            return;
        }

        if (_isCanvasPanning && e.Pointer.PointerId == _panPointerId)
        {
            EndCanvasPan(e.Pointer);
            e.Handled = true;
            return;
        }

        if (ReferenceEquals(sender, CanvasScroller) && _isLinkDragging)
            PortHost_PointerCanceled(sender, e);
    }

    private void EndCanvasPan(Pointer pointer)
    {
        _isCanvasPanning = false;
        _panCaptureTarget?.ReleasePointerCapture(pointer);
        _panCaptureTarget = null;
        ProtectedCursor = _canvasCursorBeforePan;
        _canvasCursorBeforePan = null;
    }

    private void BeginMarqueeSelect(PointerRoutedEventArgs e)
    {
        if (DataContext is not ViewModels.WorkflowEditorViewModel)
            return;

        _isMarqueeSelecting = true;
        _marqueePointerId = e.Pointer.PointerId;
        _marqueeStartWorld = e.GetCurrentPoint(NodesLayer).Position;

        _marqueeRect = new Rectangle
        {
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 59, 130, 246)),
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(44, 59, 130, 246)),
            RadiusX = 6,
            RadiusY = 6,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(_marqueeRect, _marqueeStartWorld.X);
        Canvas.SetTop(_marqueeRect, _marqueeStartWorld.Y);
        LinkPreviewLayer.Children.Add(_marqueeRect);

        _panCaptureTarget = CanvasHost;
        CanvasHost.CapturePointer(e.Pointer);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
    }

    private void UpdateMarqueeSelect(PointerRoutedEventArgs e)
    {
        if (_marqueeRect is null)
            return;

        var pos = e.GetCurrentPoint(NodesLayer).Position;
        var x1 = Math.Min(_marqueeStartWorld.X, pos.X);
        var y1 = Math.Min(_marqueeStartWorld.Y, pos.Y);
        var x2 = Math.Max(_marqueeStartWorld.X, pos.X);
        var y2 = Math.Max(_marqueeStartWorld.Y, pos.Y);

        Canvas.SetLeft(_marqueeRect, x1);
        Canvas.SetTop(_marqueeRect, y1);
        _marqueeRect.Width = Math.Max(1, x2 - x1);
        _marqueeRect.Height = Math.Max(1, y2 - y1);
    }

    private void EndMarqueeSelect(bool commit, PointerRoutedEventArgs e)
    {
        _isMarqueeSelecting = false;

        try { _panCaptureTarget?.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }
        _panCaptureTarget = null;
        ProtectedCursor = null;

        if (_marqueeRect is null)
            return;

        var left = Canvas.GetLeft(_marqueeRect);
        var top = Canvas.GetTop(_marqueeRect);
        var rect = new Windows.Foundation.Rect(left, top, _marqueeRect.Width, _marqueeRect.Height);
        LinkPreviewLayer.Children.Remove(_marqueeRect);
        _marqueeRect = null;

        if (!commit || DataContext is not ViewModels.WorkflowEditorViewModel vm)
            return;

        var additive = IsShiftDown();
        static bool Intersects(Windows.Foundation.Rect a, Windows.Foundation.Rect b) =>
            a.X < b.X + b.Width &&
            a.X + a.Width > b.X &&
            a.Y < b.Y + b.Height &&
            a.Y + a.Height > b.Y;

        var hits = vm.Steps.Where(s =>
        {
            var nodeRect = new Windows.Foundation.Rect(s.X, s.Y, NodeWidth, NodeHeight);
            return Intersects(rect, nodeRect);
        }).ToList();

        if (hits.Count == 0)
        {
            if (!additive)
                vm.ClearSelection();
        }
        else
        {
            if (additive)
            {
                foreach (var step in hits)
                    vm.AddToSelection(step);
            }
            else
            {
                vm.SetSelection(hits);
            }
        }

        UpdateNodeStyles();
    }

    private static bool IsCtrlDown()
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        return state.HasFlag(CoreVirtualKeyStates.Down);
    }

    private static bool IsShiftDown()
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        return state.HasFlag(CoreVirtualKeyStates.Down);
    }

    private bool IsCanvasBackgroundHit(DependencyObject? source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ScrollBar)
                return false;

            if (node is Border border && _nodeById.Values.Contains(border))
                return false;

            if (node is Grid grid && _ports.Exists(p => ReferenceEquals(p.Host, grid)))
                return false;

            if (node is Ellipse ellipse && _ports.Exists(p => ReferenceEquals(p.Dot, ellipse)))
                return false;

            if (node is FrameworkElement fe)
            {
                if (fe.Name is "CanvasZoomBar")
                    return false;

                if (ReferenceEquals(fe, CanvasHost) ||
                    ReferenceEquals(fe, CanvasScroller) ||
                    ReferenceEquals(fe, CanvasDropRoot) ||
                    ReferenceEquals(fe, CanvasWorld) ||
                    ReferenceEquals(fe, NodesLayer) ||
                    ReferenceEquals(fe, LinkLayer) ||
                    ReferenceEquals(fe, PortsLayer) ||
                    ReferenceEquals(fe, LinkPreviewLayer))
                    return true;
            }

            if (node.GetType().Name == "ScrollContentPresenter")
                return true;
        }

        return false;
    }

    private void OnLinkDragPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isCanvasPanning)
            return;

        if (!_isLinkDragging)
            return;

        PortHost_PointerMoved(sender, e);
    }

    private void OnLinkDragPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isCanvasPanning)
            return;

        if (!_isLinkDragging)
            return;

        PortHost_PointerReleased(sender, e);
    }

    private void OnLinkDragPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_isCanvasPanning)
            return;

        if (!_isLinkDragging)
            return;

        PortHost_PointerCanceled(sender, e);
    }

    private void OnToolboxPointerDragBegan(WorkflowToolboxItem item, PointerRoutedEventArgs e)
    {
        if (_toolboxDragItem is not null)
            EndToolboxPointerDrag(commit: false);

        _toolboxDragItem = item;
        if (ToolboxDragOverlay.Parent is FrameworkElement overlayHost)
        {
            ToolboxDragOverlay.Width = overlayHost.ActualWidth;
            ToolboxDragOverlay.Height = overlayHost.ActualHeight;
        }

        _toolboxDragGhost = WorkflowEditorDragPreviewFactory.CreateNodePreview(item.Category, item.Title, item.Kind);
        ToolboxDragOverlay.Children.Clear();
        ToolboxDragOverlay.Children.Add(_toolboxDragGhost);
        ToolboxDragOverlay.Visibility = Visibility.Visible;

        UpdateToolboxDragGhostPosition(e);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
        CapturePointer(e.Pointer);
        StartToolboxPointerHandlers();
        e.Handled = true;
    }

    private void StartToolboxPointerHandlers()
    {
        if (_toolboxPointerHandlersActive)
            return;

        PointerMoved += OnToolboxPointerDragMoved;
        PointerReleased += OnToolboxPointerDragReleased;
        PointerCanceled += OnToolboxPointerDragCanceled;
        _toolboxPointerHandlersActive = true;
    }

    private void StopToolboxPointerHandlers()
    {
        if (!_toolboxPointerHandlersActive)
            return;

        PointerMoved -= OnToolboxPointerDragMoved;
        PointerReleased -= OnToolboxPointerDragReleased;
        PointerCanceled -= OnToolboxPointerDragCanceled;
        _toolboxPointerHandlersActive = false;
    }

    private void OnToolboxPointerDragMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_toolboxDragItem is null || _toolboxDragGhost is null)
            return;

        UpdateToolboxDragGhostPosition(e);
        e.Handled = true;
    }

    private void OnToolboxPointerDragReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_toolboxDragItem is null)
            return;

        EndToolboxPointerDrag(commit: true, e);
        e.Handled = true;
    }

    private void OnToolboxPointerDragCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_toolboxDragItem is null)
            return;

        EndToolboxPointerDrag(commit: false, e);
        e.Handled = true;
    }

    private void UpdateToolboxDragGhostPosition(PointerRoutedEventArgs e)
    {
        if (_toolboxDragGhost is null)
            return;

        var pt = e.GetCurrentPoint(ToolboxDragOverlay).Position;
        Canvas.SetLeft(_toolboxDragGhost, pt.X - WorkflowEditorDragPreviewFactory.NodeWidth / 2);
        Canvas.SetTop(_toolboxDragGhost, pt.Y - WorkflowEditorDragPreviewFactory.NodeHeight / 2);
    }

    private void EndToolboxPointerDrag(bool commit, PointerRoutedEventArgs? e = null)
    {
        if (commit && _toolboxDragItem is not null && e is not null && DataContext is ViewModels.WorkflowEditorViewModel vm)
        {
            var hostPoint = e.GetCurrentPoint(CanvasHost).Position;
            if (IsPointerOverCanvas(hostPoint))
            {
                var position = e.GetCurrentPoint(NodesLayer).Position;
                var x = ClampNodeX(position.X - WorkflowEditorDragPreviewFactory.NodeWidth / 2);
                var y = ClampNodeY(position.Y - WorkflowEditorDragPreviewFactory.NodeHeight / 2);
                vm.AddStepFromToolboxAt(_toolboxDragItem, x, y);
                RenderNodes();
                UpdateCanvasExtent(vm);
                QueueRedrawLinks();
            }
        }

        ToolboxDragOverlay.Children.Clear();
        ToolboxDragOverlay.Visibility = Visibility.Collapsed;
        _toolboxDragItem = null;
        _toolboxDragGhost = null;
        ProtectedCursor = null;
        StopToolboxPointerHandlers();

        if (e is not null)
            try { ReleasePointerCapture(e.Pointer); } catch { /* already released */ }
    }

    private bool IsPointerOverCanvas(Windows.Foundation.Point positionInCanvasHost)
    {
        if (CanvasHost.ActualWidth < 1 || CanvasHost.ActualHeight < 1)
            return false;

        return positionInCanvasHost.X >= 0
               && positionInCanvasHost.Y >= 0
               && positionInCanvasHost.X <= CanvasHost.ActualWidth
               && positionInCanvasHost.Y <= CanvasHost.ActualHeight;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        WorkflowToolboxDrag.PointerDragBegan += OnToolboxPointerDragBegan;
        NavTrace.Write("WorkflowEditorPage.OnLoaded");
        _ = EnsureLoadedAsync(WorkflowNavigationBridge.TakePending());
    }

    private void OnCopyStepAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
            return;

        if (DataContext is not ViewModels.WorkflowEditorViewModel vm || vm.SelectedStep is null)
            return;

        vm.CopySelectedStep();
        args.Handled = true;
    }

    private void OnUndoAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
            return;

        if (DataContext is not ViewModels.WorkflowEditorViewModel vm || !vm.CanUndo)
            return;

        vm.Undo();
        RefreshCanvasAfterHistoryChange(vm);
        args.Handled = true;
    }

    private void OnRedoAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
            return;

        if (DataContext is not ViewModels.WorkflowEditorViewModel vm || !vm.CanRedo)
            return;

        vm.Redo();
        RefreshCanvasAfterHistoryChange(vm);
        args.Handled = true;
    }

    private void RefreshCanvasAfterHistoryChange(ViewModels.WorkflowEditorViewModel vm)
    {
        RenderNodes();
        UpdateCanvasExtent(vm);
        QueueRedrawLinks();
        UpdateNodeStyles();
    }

    private void OnDeleteStepAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
            return;

        if (DataContext is not ViewModels.WorkflowEditorViewModel vm || !vm.DeleteSelectedSteps())
            return;

        RefreshCanvasAfterHistoryChange(vm);
        args.Handled = true;
    }

    private void OnPasteStepAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
            return;

        if (DataContext is not ViewModels.WorkflowEditorViewModel vm || !vm.PasteStep())
            return;

        var pasted = vm.SelectedStep;
        if (pasted is not null)
        {
            pasted.X = ClampNodeX(pasted.X);
            pasted.Y = ClampNodeY(pasted.Y);
        }

        RenderNodes();
        UpdateCanvasExtent(vm);
        QueueRedrawLinks();
        if (pasted is not null)
            ScrollToRevealWorldPoint(pasted.X, pasted.Y);
        args.Handled = true;
    }

    private bool IsTextInputFocused()
    {
        if (FocusManager.GetFocusedElement(XamlRoot) is not DependencyObject focused)
            return false;

        for (var node = focused; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is TextBox or RichEditBox or AutoSuggestBox)
                return true;
        }

        return false;
    }

    public async Task EnsureLoadedAsync(Prism.Navigation.INavigationParameters? parameters)
    {
        if (DataContext is not ViewModels.WorkflowEditorViewModel vm)
            return;

        vm.DialogXamlRoot = XamlRoot;
        NavTrace.Write($"WorkflowEditorPage.EnsureLoaded: pending={(parameters is null ? "null" : "ok")}");
        await vm.LoadFromNavigationAsync(parameters).ConfigureAwait(true);

        HookVm();
        RenderNodes();
        RedrawLinks();

        _ = vm.TryRecoverAutosaveDeferredAsync();
        _ = vm.BeginEditorSessionAsync();
        NavTrace.Write("WorkflowEditorPage.EnsureLoaded: done");
    }

    private async void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        WorkflowToolboxDrag.PointerDragBegan -= OnToolboxPointerDragBegan;
        EndToolboxPointerDrag(commit: false);

        _externalWindowHost?.Dispose();
        _externalWindowHost = null;

        if (DataContext is ViewModels.WorkflowEditorViewModel vm)
            await vm.EndEditorSessionAsync();
    }

    private void OnTogglePropertiesPaneClick(object sender, RoutedEventArgs e)
    {
        if (_externalWindowHost is not null)
        {
            _externalWindowHost.Dispose();
            _externalWindowHost = null;
            EditorDockHost.PaneTitle = "属性";
            if (DataContext is ViewModels.WorkflowEditorViewModel vm)
                EditorDockHost.SetPaneContent(new WorkflowEditorPropertiesPanel { DataContext = vm });
        }

        EditorDockHost.TogglePaneFloating();
    }

    private async void OnEmbedExternalWindowClick(object sender, RoutedEventArgs e)
    {
        var parentWindowHandle = WorkflowAppServices.ResolveMainWindowHandle();
        if (parentWindowHandle == IntPtr.Zero)
        {
            await ShowMessageAsync("无法获取主窗口句柄，暂时不能嵌入外部窗口。").ConfigureAwait(true);
            return;
        }

        var dialog = new ExternalWindowPickerDialog
        {
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary || dialog.SelectedWindow is null)
            return;

        _externalWindowHost?.Dispose();
        _externalWindowHost = new ExternalWindowHost
        {
            ParentWindowHandle = parentWindowHandle,
        };

        EditorDockHost.PaneTitle = $"外部窗口";
        EditorDockHost.SetPaneContent(_externalWindowHost);
        _externalWindowHost.Attach(dialog.SelectedWindow);
    }

    private async Task ShowMessageAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "嵌入外部窗口",
            Content = message,
            CloseButtonText = "确定",
        };
        await dialog.ShowAsync();
    }

    private void HookVm()
    {
        if (DataContext is not ViewModels.WorkflowEditorViewModel vm)
            return;

        if (ReferenceEquals(_hookedVm, vm))
            return;

        if (_hookedVm is not null)
        {
            _hookedVm.Steps.CollectionChanged -= Steps_CollectionChanged;
            _hookedVm.Links.CollectionChanged -= Links_CollectionChanged;
            _hookedVm.PropertyChanged -= Vm_PropertyChanged;
        }

        _hookedVm = vm;
        _hookedVm.Steps.CollectionChanged += Steps_CollectionChanged;
        _hookedVm.Links.CollectionChanged += Links_CollectionChanged;
        _hookedVm.PropertyChanged += Vm_PropertyChanged;
    }

    private void Links_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        QueueRedrawLinks();

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewModels.WorkflowEditorViewModel.RuntimeCurrentStepId)
            or nameof(ViewModels.WorkflowEditorViewModel.SelectedStep)
            or nameof(ViewModels.WorkflowEditorViewModel.SelectedSteps))
            UpdateNodeStyles();
        else if (e.PropertyName == nameof(ViewModels.WorkflowEditorViewModel.Steps))
        {
            RenderNodes();
            QueueRedrawLinks();
        }
    }

    private void Steps_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        QueueRedrawLinks();
    }

    private void Node_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isLinkDragging)
            return;

        var border = sender as Border
            ?? (sender as FrameworkElement)?.Parent as Border;
        if (border?.DataContext is not WorkflowStep step)
            return;

        if (DataContext is ViewModels.WorkflowEditorViewModel vm)
        {
            if (IsCtrlDown())
                vm.ToggleSelection(step);
            else if (IsShiftDown())
                vm.AddToSelection(step);
            else
                vm.SelectedStep = step;

            vm.BeginCanvasInteraction();
            UpdateNodeStyles();
        }

        _dragging = step;
        _draggingBorder = border;
        _dragStart = e.GetCurrentPoint(NodesLayer).Position;

        _draggingSteps.Clear();
        _dragOriginById.Clear();
        if (DataContext is ViewModels.WorkflowEditorViewModel dragVm &&
            dragVm.SelectedSteps.Contains(step) &&
            dragVm.SelectedSteps.Count > 1)
        {
            foreach (var selected in dragVm.SelectedSteps)
            {
                _draggingSteps.Add(selected);
                _dragOriginById[selected.Id] = (selected.X, selected.Y);
            }
        }
        else
        {
            _draggingSteps.Add(step);
            _dragOriginById[step.Id] = (step.X, step.Y);
        }

        border.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Node_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging is null || _draggingBorder is null)
            return;

        var p = e.GetCurrentPoint(NodesLayer).Position;
        var dx = p.X - _dragStart.X;
        var dy = p.Y - _dragStart.Y;

        foreach (var step in _draggingSteps)
        {
            if (!_dragOriginById.TryGetValue(step.Id, out var origin))
                continue;

            var newX = ClampNodeX(origin.X + dx);
            var newY = ClampNodeY(origin.Y + dy);
            step.X = newX;
            step.Y = newY;

            if (_nodeById.TryGetValue(step.Id, out var border))
            {
                Canvas.SetLeft(border, newX);
                Canvas.SetTop(border, newY);
            }

            UpdatePortsForStep(step);
        }

        QueueRedrawLinks();
    }

    private void Node_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b)
            b.ReleasePointerCaptures();

        if (DataContext is ViewModels.WorkflowEditorViewModel vm)
        {
            vm.EndCanvasInteraction();
            vm.MarkDirtyAfterCanvasInteraction();
        }

        var releasedStep = _dragging;
        _dragging = null;
        _draggingBorder = null;
        _draggingSteps.Clear();
        _dragOriginById.Clear();

        if (DataContext is ViewModels.WorkflowEditorViewModel vmAfterDrag)
        {
            UpdateCanvasExtent(vmAfterDrag);
            if (releasedStep is not null)
                ScrollToRevealWorldPoint(releasedStep.X, releasedStep.Y);
        }

        QueueRedrawLinks();
    }

    private void RenderNodes()
    {
        if (DataContext is not ViewModels.WorkflowEditorViewModel vm)
            return;

        NodesLayer.Children.Clear();
        PortsLayer.Children.Clear();
        _nodeById.Clear();
        _ports.Clear();
        EndLinkDrag(commit: false);

        foreach (var step in vm.Steps)
        {
            var b = CreateNode(step);
            NodesLayer.Children.Add(b);
            _nodeById[step.Id] = b;
        }

        RenderPorts();
        UpdateCanvasExtent(vm);
        UpdateNodeStyles();
    }

    private void RenderPorts()
    {
        if (DataContext is not WorkflowEditorViewModel vm)
            return;

        PortsLayer.Children.Clear();
        _ports.Clear();

        foreach (var step in vm.Steps)
        {
            foreach (WorkflowPort port in Enum.GetValues<WorkflowPort>())
            {
                var visual = CreatePortVisual(step, port);
                _ports.Add(visual);
                PositionPort(visual);
                PortsLayer.Children.Add(visual.Host);
            }
        }
    }

    private void PositionPort(PortVisual visual)
    {
        var center = GetPortPoint(visual.Step, visual.Port, NodeWidth, NodeHeight);
        Canvas.SetLeft(visual.Host, center.X - PortHostSize / 2);
        Canvas.SetTop(visual.Host, center.Y - PortHostSize / 2);
    }

    private void UpdatePortsForStep(WorkflowStep step)
    {
        foreach (var port in _ports.Where(p => p.Step.Id == step.Id))
            PositionPort(port);
    }

    private Border CreateNode(WorkflowStep step)
    {
        var border = new Border
        {
            Width = 220,
            Height = 68,
            Background = (Brush)Application.Current.Resources["KjPanel2Brush"],
            BorderBrush = NodeDefaultBorderBrush,
            BorderThickness = new Microsoft.UI.Xaml.Thickness(1.5),
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(10),
            Padding = new Microsoft.UI.Xaml.Thickness(12),
            DataContext = step,
            IsTabStop = false,
        };

        var stack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(CreateNodeCaption(step.Title, isSecondary: false));
        stack.Children.Add(CreateNodeCaption(step.Kind, isSecondary: true));

        border.Child = stack;

        Canvas.SetLeft(border, step.X);
        Canvas.SetTop(border, step.Y);

        // Keep text in sync with edits from right panel
        step.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(WorkflowStep.X))
            {
                Canvas.SetLeft(border, step.X);
                UpdatePortsForStep(step);
            }
            else if (args.PropertyName == nameof(WorkflowStep.Y))
            {
                Canvas.SetTop(border, step.Y);
                UpdatePortsForStep(step);
            }
            else if (args.PropertyName == nameof(WorkflowStep.Title)
                     && border.Child is StackPanel sp
                     && sp.Children.ElementAtOrDefault(0) is TextBlock t1)
                t1.Text = step.Title;
            else if (args.PropertyName == nameof(WorkflowStep.Kind)
                     && border.Child is StackPanel sp2
                     && sp2.Children.ElementAtOrDefault(1) is TextBlock t2)
                t2.Text = step.Kind;
            else if (args.PropertyName == nameof(WorkflowStep.NextStepId))
                QueueRedrawLinks();
            else if (args.PropertyName is nameof(WorkflowStep.X) or nameof(WorkflowStep.Y))
            {
                UpdatePortsForStep(step);
                UpdateCanvasExtent(_hookedVm);
            }
        };

        border.PointerPressed += Node_PointerPressed;
        border.PointerMoved += Node_PointerMoved;
        border.PointerReleased += Node_PointerReleased;

        return border;
    }

    private static TextBlock CreateNodeCaption(string text, bool isSecondary) =>
        new()
        {
            Text = text,
            FontSize = isSecondary ? 12 : 14,
            Opacity = isSecondary ? 0.85 : 1,
            Foreground = isSecondary
                ? (Brush)Application.Current.Resources["KjTextSecondaryBrush"]
                : (Brush)Application.Current.Resources["KjTextPrimaryBrush"],
            IsTextSelectionEnabled = false,
            IsHitTestVisible = false,
        };

    private PortVisual CreatePortVisual(WorkflowStep step, WorkflowPort port)
    {
        var dot = new Ellipse
        {
            Width = PortDotSize,
            Height = PortDotSize,
            Fill = PortDefaultBrush,
            Stroke = (Brush)Application.Current.Resources["KjStrokeBrush"],
            StrokeThickness = 1.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.85,
        };

        var host = new Grid
        {
            Width = PortHostSize,
            Height = PortHostSize,
            Background = new SolidColorBrush(Colors.Transparent),
        };
        host.Children.Add(dot);

        var visual = new PortVisual { Step = step, Port = port, Host = host, Dot = dot };
        _ports.Add(visual);

        host.PointerEntered += (_, _) => SetPortHighlight(visual, hovered: true);
        host.PointerExited += (_, _) =>
        {
            if (!ReferenceEquals(_linkSnapTarget, visual))
                SetPortHighlight(visual, hovered: false);
        };

        host.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(host).Properties.IsLeftButtonPressed != true)
                return;

            args.Handled = true;
            if (DataContext is not WorkflowEditorViewModel vm)
                return;

            vm.SelectedStep = step;
            BeginLinkDrag(visual, args);
        };

        return visual;
    }

    private void BeginLinkDrag(PortVisual from, PointerRoutedEventArgs args)
    {
        if (DataContext is WorkflowEditorViewModel vm)
            vm.BeginLinkFromPort(from.Step, from.Port);

        _isLinkDragging = true;
        _linkFromPort = from;
        SetPortHighlight(from, hovered: true, dragging: true);

        _linkPreviewPolyline = CreatePreviewPolyline();
        LinkPreviewLayer.Children.Add(_linkPreviewPolyline);
        UpdateLinkPreviewPath(from.Step, from.Port, GetPortPoint(from.Step, from.Port, NodeWidth, NodeHeight));

        _linkCaptureTarget = from.Host;
        from.Host.CapturePointer(args.Pointer);
        from.Host.PointerMoved += PortHost_PointerMoved;
        from.Host.PointerReleased += PortHost_PointerReleased;
        from.Host.PointerCanceled += PortHost_PointerCanceled;
    }

    private void PortHost_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isLinkDragging || _linkPreviewPolyline is null || _linkFromPort is null)
            return;

        var pos = e.GetCurrentPoint(LinkPreviewLayer).Position;
        var snap = FindPortAt(pos, exclude: _linkFromPort);
        if (!ReferenceEquals(_linkSnapTarget, snap))
        {
            if (_linkSnapTarget is not null)
                SetPortHighlight(_linkSnapTarget, hovered: false, snap: false);
            _linkSnapTarget = snap;
            if (_linkSnapTarget is not null)
                SetPortHighlight(_linkSnapTarget, hovered: true, snap: true);
        }

        if (snap is not null)
        {
            var end = GetPortPoint(snap.Step, snap.Port, NodeWidth, NodeHeight);
            UpdateLinkPreviewPath(_linkFromPort.Step, _linkFromPort.Port, end, snap.Step, snap.Port);
        }
        else
        {
            UpdateLinkPreviewPath(_linkFromPort.Step, _linkFromPort.Port, pos);
        }
    }

    private void PortHost_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isLinkDragging || _linkFromPort is null)
            return;

        var pos = e.GetCurrentPoint(LinkPreviewLayer).Position;
        var target = FindPortAt(pos, exclude: _linkFromPort) ?? _linkSnapTarget;

        if (DataContext is WorkflowEditorViewModel vm)
        {
            if (target is not null)
                vm.TryCompleteLinkToPort(target.Step, target.Port);
            else
                vm.CancelLinkInProgress();
        }

        EndLinkDrag(commit: target is not null);
        QueueRedrawLinks();
        e.Handled = true;
    }

    private void PortHost_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (DataContext is WorkflowEditorViewModel vm)
            vm.CancelLinkInProgress();
        EndLinkDrag(commit: false);
        e.Handled = true;
    }

    private void EndLinkDrag(bool commit)
    {
        if (_linkFromPort is not null)
        {
            _linkFromPort.Host.PointerMoved -= PortHost_PointerMoved;
            _linkFromPort.Host.PointerReleased -= PortHost_PointerReleased;
            _linkFromPort.Host.PointerCanceled -= PortHost_PointerCanceled;
        }

        if (_linkCaptureTarget is UIElement capture)
            capture.ReleasePointerCaptures();

        if (_linkPreviewPolyline is not null)
        {
            LinkPreviewLayer.Children.Remove(_linkPreviewPolyline);
            _linkPreviewPolyline = null;
        }

        if (_linkFromPort is not null)
            SetPortHighlight(_linkFromPort, hovered: false, dragging: false);
        if (_linkSnapTarget is not null)
            SetPortHighlight(_linkSnapTarget, hovered: false, snap: false);

        _isLinkDragging = false;
        _linkFromPort = null;
        _linkSnapTarget = null;
        _linkCaptureTarget = null;
        _ = commit;
    }

    private PortVisual? FindPortAt(Windows.Foundation.Point positionOnLinkLayer, PortVisual? exclude)
    {
        PortVisual? best = null;
        var bestDist = PortHitRadius * PortHitRadius;

        foreach (var p in _ports)
        {
            if (ReferenceEquals(p, exclude))
                continue;

            var center = GetPortPoint(p.Step, p.Port, NodeWidth, NodeHeight);
            var dx = positionOnLinkLayer.X - center.X;
            var dy = positionOnLinkLayer.Y - center.Y;
            var d2 = dx * dx + dy * dy;
            if (d2 <= bestDist)
            {
                bestDist = d2;
                best = p;
            }
        }

        return best;
    }

    private static void SetPortHighlight(PortVisual port, bool hovered, bool dragging = false, bool snap = false)
    {
        if (dragging || snap)
        {
            port.Dot.Width = 14;
            port.Dot.Height = 14;
            port.Dot.Opacity = 1;
            port.Dot.Fill = snap ? NodeRunningBrush : PortActiveBrush;
            port.Dot.StrokeThickness = 2;
            return;
        }

        port.Dot.Width = hovered ? 12 : PortDotSize;
        port.Dot.Height = hovered ? 12 : PortDotSize;
        port.Dot.Opacity = hovered ? 1 : 0.75;
        port.Dot.Fill = PortDefaultBrush;
        port.Dot.StrokeThickness = 1;
    }

    private void UpdateNodeStyles()
    {
        if (_hookedVm is null)
            return;

        var activeId = _hookedVm.RuntimeCurrentStepId;
        var selectedId = _hookedVm.SelectedStep?.Id;
        var selectedIds = _hookedVm.SelectedSteps.Select(s => s.Id).ToHashSet();

        foreach (var kv in _nodeById)
        {
            var border = kv.Value;
            var isRunning = activeId is not null && kv.Key == activeId.Value;
            var isSelected = (selectedId is not null && kv.Key == selectedId.Value) || selectedIds.Contains(kv.Key);

            if (isRunning)
            {
                border.BorderBrush = NodeRunningBrush;
                border.BorderThickness = new Microsoft.UI.Xaml.Thickness(3);
            }
            else if (isSelected)
            {
                border.BorderBrush = NodeSelectedBorderBrush;
                border.BorderThickness = new Microsoft.UI.Xaml.Thickness(3);
            }
            else
            {
                border.BorderBrush = NodeDefaultBorderBrush;
                border.BorderThickness = new Microsoft.UI.Xaml.Thickness(1.5);
            }
        }
    }

    private void UpdateCanvasExtent(ViewModels.WorkflowEditorViewModel? vm)
    {
        _canvasPadLeft = CanvasWorldInset;
        _canvasPadTop = CanvasWorldInset;
        _contentMinX = 0;
        _contentMinY = 0;

        double width = 2400;
        double height = 1200;

        if (vm is not null && vm.Steps.Count > 0)
        {
            if (TryGetWorkflowContentBounds(vm, out var minX, out var minY, out var maxX, out var maxY))
            {
                _contentMinX = Math.Min(0, minX - CanvasRouteBoundsPad);
                _contentMinY = Math.Min(0, minY - CanvasRouteBoundsPad);
                width = Math.Max(2400, maxX - _contentMinX + NodeWidth + 320);
                height = Math.Max(1200, maxY - _contentMinY + NodeHeight + 320);
            }
            else
            {
                var stepMaxX = vm.Steps.Max(s => s.X);
                var stepMaxY = vm.Steps.Max(s => s.Y);
                width = Math.Max(2400, stepMaxX + NodeWidth + 320);
                height = Math.Max(1200, stepMaxY + NodeHeight + 320);
            }
        }

        NodesLayer.Width = width;
        NodesLayer.Height = height;
        LinkLayer.Width = width;
        LinkLayer.Height = height;
        PortsLayer.Width = width;
        PortsLayer.Height = height;
        LinkPreviewLayer.Width = width;
        LinkPreviewLayer.Height = height;
        ApplyCanvasWorldPadding();
        SyncCanvasScrollExtent();
        QueueRedrawViewportGrid();
    }

    private void QueueRedrawViewportGrid()
    {
        if (_viewportGridQueued)
            return;
        _viewportGridQueued = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            _viewportGridQueued = false;
            DrawViewportGrid();
        });
    }

    private void DrawViewportGrid()
    {
        if (!TryGetCanvasViewportMetrics(
                out var vw,
                out var vh,
                out var scrollX,
                out var scrollY,
                out var originX,
                out var originY,
                out var clientW,
                out var clientH))
            return;

        ViewportGridLayer.Width = vw;
        ViewportGridLayer.Height = vh;
        ViewportGridLayer.Children.Clear();

        var zoom = _canvasZoom;
        const int g = WorkflowGridLinkRouter.GridSize;

        var worldLeft = (scrollX - originX) / zoom - _canvasPadLeft - LayerOffsetX;
        var worldTop = (scrollY - originY) / zoom - _canvasPadTop - LayerOffsetY;
        var worldRight = worldLeft + clientW / zoom;
        var worldBottom = worldTop + clientH / zoom;

        var minor = new SolidColorBrush(Windows.UI.Color.FromArgb(22, 148, 163, 184));
        var major = new SolidColorBrush(Windows.UI.Color.FromArgb(38, 148, 163, 184));

        var startX = Math.Floor(worldLeft / g) * g - g;
        var endX = Math.Ceiling(worldRight / g) * g + g;
        for (var wx = startX; wx <= endX; wx += g)
        {
            var screenX = originX + WorldToScrollX(wx) * zoom - scrollX;
            if (screenX < -1 || screenX > vw + 1)
                continue;

            var cell = (int)Math.Round(wx / g);
            var isMajor = cell % 5 == 0;
            ViewportGridLayer.Children.Add(new Line
            {
                X1 = screenX,
                Y1 = 0,
                X2 = screenX,
                Y2 = vh,
                Stroke = isMajor ? major : minor,
                StrokeThickness = isMajor ? 1 : 0.5,
            });
        }

        var startY = Math.Floor(worldTop / g) * g - g;
        var endY = Math.Ceiling(worldBottom / g) * g + g;
        for (var wy = startY; wy <= endY; wy += g)
        {
            var screenY = originY + WorldToScrollY(wy) * zoom - scrollY;
            if (screenY < -1 || screenY > vh + 1)
                continue;

            var cell = (int)Math.Round(wy / g);
            var isMajor = cell % 5 == 0;
            ViewportGridLayer.Children.Add(new Line
            {
                X1 = 0,
                Y1 = screenY,
                X2 = vw,
                Y2 = screenY,
                Stroke = isMajor ? major : minor,
                StrokeThickness = isMajor ? 1 : 0.5,
            });
        }
    }

    private void QueueRedrawLinks()
    {
        if (_redrawQueued)
            return;
        _redrawQueued = true;

        // coalesce multiple pointer moves into at most 1 redraw per UI loop
        DispatcherQueue.TryEnqueue(() =>
        {
            _redrawQueued = false;
            RedrawLinks();
        });
    }

    private void RedrawLinks()
    {
        if (DataContext is not ViewModels.WorkflowEditorViewModel vm)
            return;

        LinkLayer.Children.Clear();
        var obstacles = BuildObstacles(vm);
        var reserved = new HashSet<WorkflowGridLinkRouter.GridCell>();
        var lane = 0;

        foreach (var link in vm.Links
                     .OrderBy(l => l.FromStepId)
                     .ThenBy(l => l.ToStepId)
                     .ThenBy(l => l.FromPort)
                     .ThenBy(l => l.ToPort))
        {
            var s = vm.Steps.FirstOrDefault(x => x.Id == link.FromStepId);
            var t = vm.Steps.FirstOrDefault(x => x.Id == link.ToStepId);
            if (s is null || t is null)
                continue;

            var start = GetPortPoint(s, link.FromPort, NodeWidth, NodeHeight);
            var end = GetPortPoint(t, link.ToPort, NodeWidth, NodeHeight);
            var path = WorkflowGridLinkRouter.Route(
                start,
                link.FromPort,
                end,
                link.ToPort,
                obstacles,
                s.Id,
                t.Id,
                reserved,
                lane++);

            WorkflowGridLinkRouter.ReservePathCells(path, reserved);
            AddRoutedLinkVisual(LinkLayer, path, dashed: false);
        }
    }

    private static List<WorkflowGridLinkRouter.NodeObstacle> BuildObstacles(WorkflowEditorViewModel vm) =>
        vm.Steps.Select(s => new WorkflowGridLinkRouter.NodeObstacle(s.Id, s.X, s.Y, NodeWidth, NodeHeight)).ToList();

    private void UpdateLinkPreviewPath(
        WorkflowStep fromStep,
        WorkflowPort fromPort,
        Windows.Foundation.Point end,
        WorkflowStep? toStep = null,
        WorkflowPort? toPort = null)
    {
        if (_linkPreviewPolyline is null || DataContext is not WorkflowEditorViewModel vm)
            return;

        var start = GetPortPoint(fromStep, fromPort, NodeWidth, NodeHeight);
        var obstacles = BuildObstacles(vm);
        IReadOnlyList<Windows.Foundation.Point> path;

        if (toStep is not null && toPort is not null)
        {
            path = WorkflowGridLinkRouter.Route(
                start,
                fromPort,
                end,
                toPort.Value,
                obstacles,
                fromStep.Id,
                toStep.Id);
        }
        else
        {
            path = WorkflowGridLinkRouter.RoutePreview(start, fromPort, end, obstacles, fromStep.Id);
        }

        ApplyPolylinePoints(_linkPreviewPolyline, path);
    }

    private static Polyline CreatePreviewPolyline() => new()
    {
        StrokeThickness = 3,
        Stroke = LinkPreviewStrokeBrush,
        Opacity = 1,
        StrokeDashArray = new DoubleCollection { 6, 4 },
    };

    private void AddRoutedLinkVisual(Canvas layer, IReadOnlyList<Windows.Foundation.Point> path, bool dashed)
    {
        if (path.Count < 2)
            return;

        var poly = new Polyline
        {
            Stroke = LinkStrokeBrush,
            StrokeThickness = 2.5,
            Opacity = 1,
        };
        if (dashed)
            poly.StrokeDashArray = new DoubleCollection { 6, 4 };

        ApplyPolylinePoints(poly, path);
        layer.Children.Add(poly);
        AddArrowHead(layer, path[^2], path[^1]);
    }

    private static void ApplyPolylinePoints(Polyline poly, IReadOnlyList<Windows.Foundation.Point> path)
    {
        poly.Points.Clear();
        foreach (var p in path)
            poly.Points.Add(p);
    }

    private static void AddArrowHead(Canvas layer, Windows.Foundation.Point from, Windows.Foundation.Point to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01)
            return;

        double tipX, tipY, baseX, baseY;
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            var dir = dx >= 0 ? 1 : -1;
            tipX = to.X;
            tipY = to.Y;
            baseX = to.X - dir * 10;
            baseY = to.Y;
        }
        else
        {
            var dir = dy >= 0 ? 1 : -1;
            tipX = to.X;
            tipY = to.Y;
            baseX = to.X;
            baseY = to.Y - dir * 10;
        }

        var arrow = new Polygon
        {
            Fill = LinkStrokeBrush,
            Opacity = 1,
        };

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            arrow.Points.Add(new Windows.Foundation.Point(tipX, tipY));
            arrow.Points.Add(new Windows.Foundation.Point(baseX, baseY - 6));
            arrow.Points.Add(new Windows.Foundation.Point(baseX, baseY + 6));
        }
        else
        {
            arrow.Points.Add(new Windows.Foundation.Point(tipX, tipY));
            arrow.Points.Add(new Windows.Foundation.Point(baseX - 6, baseY));
            arrow.Points.Add(new Windows.Foundation.Point(baseX + 6, baseY));
        }

        layer.Children.Add(arrow);
    }

    private static Windows.Foundation.Point GetPortPoint(WorkflowStep s, WorkflowPort port, double w, double h) =>
        port switch
        {
            WorkflowPort.Top => new Windows.Foundation.Point(s.X + w / 2, s.Y),
            WorkflowPort.Right => new Windows.Foundation.Point(s.X + w, s.Y + h / 2),
            WorkflowPort.Bottom => new Windows.Foundation.Point(s.X + w / 2, s.Y + h),
            WorkflowPort.Left => new Windows.Foundation.Point(s.X, s.Y + h / 2),
            _ => new Windows.Foundation.Point(s.X + w, s.Y + h / 2),
        };
}


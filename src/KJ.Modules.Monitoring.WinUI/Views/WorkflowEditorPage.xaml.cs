using KJ.Workflows;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class WorkflowEditorPage : Page
{
    private WorkflowStep? _dragging;
    private Border? _draggingBorder;
    private readonly Dictionary<Guid, Border> _nodeById = new();
    private ViewModels.WorkflowEditorViewModel? _hookedVm;
    private Windows.Foundation.Point _dragStart;
    private double _originX;
    private double _originY;
    private bool _redrawQueued;

    public WorkflowEditorPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            HookVm();
            RenderNodes();
            RedrawLinks();
        };
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.WorkflowEditorViewModel vm)
        {
            vm.DialogXamlRoot = XamlRoot;
            await vm.TryLoadFromNavigationAsync();
            await vm.BeginEditorSessionAsync();
        }

        HookVm();
        RenderNodes();
        RedrawLinks();
    }

    private async void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.WorkflowEditorViewModel vm)
            await vm.EndEditorSessionAsync();
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
            _hookedVm.PropertyChanged -= Vm_PropertyChanged;
        }

        _hookedVm = vm;
        _hookedVm.Steps.CollectionChanged += Steps_CollectionChanged;
        _hookedVm.PropertyChanged += Vm_PropertyChanged;
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.WorkflowEditorViewModel.RuntimeCurrentStepId))
            UpdateNodeStyles();
    }

    private void Steps_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RenderNodes();
        QueueRedrawLinks();
    }

    private void Node_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border b || b.DataContext is not WorkflowStep step)
            return;

        if (DataContext is ViewModels.WorkflowEditorViewModel vm)
            vm.SelectedStep = step;

        _dragging = step;
        _draggingBorder = b;
        _dragStart = e.GetCurrentPoint(LinkLayer).Position;
        _originX = step.X;
        _originY = step.Y;

        b.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Node_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging is null || _draggingBorder is null)
            return;

        var p = e.GetCurrentPoint(LinkLayer).Position;
        var dx = p.X - _dragStart.X;
        var dy = p.Y - _dragStart.Y;

        var newX = Math.Max(0, _originX + dx);
        var newY = Math.Max(0, _originY + dy);

        // Persist model coordinates (for JSON save / link calc)
        _dragging.X = newX;
        _dragging.Y = newY;

        // Force UI position update
        Canvas.SetLeft(_draggingBorder, newX);
        Canvas.SetTop(_draggingBorder, newY);

        QueueRedrawLinks();
    }

    private void Node_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b)
            b.ReleasePointerCaptures();
        _dragging = null;
        _draggingBorder = null;
        QueueRedrawLinks();
    }

    private void RenderNodes()
    {
        if (DataContext is not ViewModels.WorkflowEditorViewModel vm)
            return;

        NodesLayer.Children.Clear();
        _nodeById.Clear();

        foreach (var step in vm.Steps)
        {
            var b = CreateNode(step);
            NodesLayer.Children.Add(b);
            _nodeById[step.Id] = b;
        }

        UpdateCanvasExtent(vm);
        UpdateNodeStyles();
    }

    private Border CreateNode(WorkflowStep step)
    {
        var border = new Border
        {
            Width = 220,
            Height = 68,
            Background = (Brush)Application.Current.Resources["KjPanel2Brush"],
            BorderBrush = (Brush)Application.Current.Resources["KjStrokeBrush"],
            BorderThickness = new Microsoft.UI.Xaml.Thickness(1.5),
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(10),
            Padding = new Microsoft.UI.Xaml.Thickness(12),
            DataContext = step,
        };

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock { Text = step.Title, FontSize = 14 });
        stack.Children.Add(new TextBlock
        {
            Text = step.Kind,
            FontSize = 12,
            Opacity = 0.85,
            Foreground = (Brush)Application.Current.Resources["KjTextSecondaryBrush"],
        });

        border.Child = stack;

        Canvas.SetLeft(border, step.X);
        Canvas.SetTop(border, step.Y);

        // Keep text in sync with edits from right panel
        step.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(WorkflowStep.X))
                Canvas.SetLeft(border, step.X);
            else if (args.PropertyName == nameof(WorkflowStep.Y))
                Canvas.SetTop(border, step.Y);
            else if (args.PropertyName == nameof(WorkflowStep.Title) && stack.Children[0] is TextBlock t1)
                t1.Text = step.Title;
            else if (args.PropertyName == nameof(WorkflowStep.Kind) && stack.Children[1] is TextBlock t2)
                t2.Text = step.Kind;
            else if (args.PropertyName == nameof(WorkflowStep.NextStepId))
                QueueRedrawLinks();
            else if (args.PropertyName is nameof(WorkflowStep.X) or nameof(WorkflowStep.Y))
                UpdateCanvasExtent(_hookedVm);
        };

        border.PointerPressed += Node_PointerPressed;
        border.PointerMoved += Node_PointerMoved;
        border.PointerReleased += Node_PointerReleased;

        return border;
    }

    private void UpdateNodeStyles()
    {
        if (_hookedVm is null)
            return;

        var activeId = _hookedVm.RuntimeCurrentStepId;
        foreach (var kv in _nodeById)
        {
            var border = kv.Value;
            var isActive = activeId is not null && kv.Key == activeId.Value;

            // Default: keep all nodes orange (accent) for consistent visual language.
            // Active (running/stepping): turn the current node green.
            border.BorderBrush = isActive
                ? new SolidColorBrush(Colors.LimeGreen)
                : (Brush)Application.Current.Resources["KjAccentBrush"];

            border.BorderThickness = isActive
                ? new Microsoft.UI.Xaml.Thickness(3)
                : new Microsoft.UI.Xaml.Thickness(2);
        }
    }

    private void UpdateCanvasExtent(ViewModels.WorkflowEditorViewModel? vm)
    {
        if (vm is null || vm.Steps.Count == 0)
            return;

        var maxX = vm.Steps.Max(s => s.X);
        var maxY = vm.Steps.Max(s => s.Y);

        // Node size (CreateNode) + padding
        var width = Math.Max(900, maxX + 220 + 120);
        var height = Math.Max(520, maxY + 68 + 120);

        NodesLayer.Width = width;
        NodesLayer.Height = height;
        LinkLayer.Width = width;
        LinkLayer.Height = height;
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

        const double w = 220;
        const double h = 68;

        foreach (var s in vm.Steps)
        {
            if (s.NextStepId is not { } nextId)
                continue;

            var t = vm.Steps.FirstOrDefault(x => x.Id == nextId);
            if (t is null)
                continue;

            var x1 = s.X + w;
            var y1 = s.Y + h / 2;
            var x2 = t.X;
            var y2 = t.Y + h / 2;

            var line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                StrokeThickness = 2,
                Stroke = (Brush)Application.Current.Resources["KjStrokeBrush"],
                Opacity = 0.9,
            };
            LinkLayer.Children.Add(line);

            var arrow = new Polygon
            {
                Fill = (Brush)Application.Current.Resources["KjStrokeBrush"],
                Opacity = 0.9,
            };
            arrow.Points.Add(new Windows.Foundation.Point(x2, y2));
            arrow.Points.Add(new Windows.Foundation.Point(x2 + 10, y2 - 5));
            arrow.Points.Add(new Windows.Foundation.Point(x2 + 10, y2 + 5));
            LinkLayer.Children.Add(arrow);
        }
    }
}


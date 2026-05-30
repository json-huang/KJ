using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace KJ.WinUI.Docking;

public sealed partial class DockPane : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(DockPane), new PropertyMetadata("工具窗口"));

    public event EventHandler? AutoHideRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler? FloatRequested;
    public event Action<DockPane, PointerRoutedEventArgs>? HeaderPointerPressed;
    public event Action<DockPane, PointerRoutedEventArgs>? HeaderPointerMoved;
    public event Action<DockPane, PointerRoutedEventArgs>? HeaderPointerReleased;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public DockPane()
    {
        InitializeComponent();
        HeaderBar.PointerPressed += OnHeaderPointerPressed;
        HeaderBar.PointerMoved += OnHeaderPointerMoved;
        HeaderBar.PointerReleased += OnHeaderPointerReleased;
        HeaderBar.PointerCanceled += OnHeaderPointerReleased;
    }

    public void SetPaneContent(UIElement? content) => ContentHost.Content = content;

    public UIElement? TakePaneContent()
    {
        var content = ContentHost.Content as UIElement;
        ContentHost.Content = null;
        return content;
    }

    private void OnHeaderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        HeaderPointerPressed?.Invoke(this, e);
    }

    private void OnHeaderPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        HeaderPointerMoved?.Invoke(this, e);
    }

    private void OnHeaderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        HeaderPointerReleased?.Invoke(this, e);
    }

    private void OnAutoHideClick(object sender, RoutedEventArgs e) => AutoHideRequested?.Invoke(this, EventArgs.Empty);

    private void OnFloatClick(object sender, RoutedEventArgs e) => FloatRequested?.Invoke(this, EventArgs.Empty);

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnResizeGripDragDelta(object sender, DragDeltaEventArgs e)
    {
        // Works when the pane is floating; docked panes will typically ignore Width/Height.
        var newWidth = double.IsNaN(Width) ? ActualWidth : Width;
        var newHeight = double.IsNaN(Height) ? ActualHeight : Height;

        newWidth = Math.Max(MinWidth, newWidth + e.HorizontalChange);
        newHeight = Math.Max(MinHeight, newHeight + e.VerticalChange);

        Width = newWidth;
        Height = newHeight;
    }
}

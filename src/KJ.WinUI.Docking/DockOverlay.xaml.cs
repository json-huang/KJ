using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KJ.WinUI.Docking;

public sealed partial class DockOverlay : UserControl
{
    public static readonly DependencyProperty HintProperty =
        DependencyProperty.Register(nameof(Hint), typeof(string), typeof(DockOverlay), new PropertyMetadata(string.Empty));

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public DockOverlay() => InitializeComponent();

    public void Show(string hint, DockPosition? selectedPosition = null)
    {
        Hint = hint;
        Visibility = Visibility.Visible;
        UpdateGuideHighlight(selectedPosition);
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        UpdateGuideHighlight(null);
    }

    private void UpdateGuideHighlight(DockPosition? selectedPosition)
    {
        LeftGuide.Opacity = selectedPosition == DockPosition.Left ? 1 : 0.75;
        RightGuide.Opacity = selectedPosition == DockPosition.Right ? 1 : 0.75;
        BottomGuide.Opacity = selectedPosition == DockPosition.Bottom ? 1 : 0.75;
        LeftEdgeGuide.Opacity = selectedPosition == DockPosition.Left ? 1 : 0.78;
        RightEdgeGuide.Opacity = selectedPosition == DockPosition.Right ? 1 : 0.78;
        BottomEdgeGuide.Opacity = selectedPosition == DockPosition.Bottom ? 1 : 0.78;
        PreviewBorder.Visibility = selectedPosition is null ? Visibility.Collapsed : Visibility.Visible;
    }
}

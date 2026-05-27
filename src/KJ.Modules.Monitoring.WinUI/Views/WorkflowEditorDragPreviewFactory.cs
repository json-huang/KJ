using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KJ.Modules.Monitoring.Views;

internal static class WorkflowEditorDragPreviewFactory
{
    public const double NodeWidth = 220;
    public const double NodeHeight = 68;

    public static Border CreateNodePreview(string category, string title, string? subtitle = null)
    {
        var accent = Application.Current.Resources["KjAccentBrush"] as Brush;
        var panel = Application.Current.Resources["KjPanel2Brush"] as Brush;
        var textSecondary = Application.Current.Resources["KjTextSecondaryBrush"] as Brush;

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = category,
            FontSize = 11,
            Foreground = accent,
        });
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 11,
                Foreground = textSecondary,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        return new Border
        {
            Width = NodeWidth,
            Height = NodeHeight,
            CornerRadius = new CornerRadius(10),
            Background = panel,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 75, 123, 181)),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(12, 8, 12, 8),
            Child = stack,
        };
    }
}

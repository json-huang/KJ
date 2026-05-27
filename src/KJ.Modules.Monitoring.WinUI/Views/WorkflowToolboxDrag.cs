using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml.Input;

namespace KJ.Modules.Monitoring.Views;

internal static class WorkflowToolboxDrag
{
    public const string KindKey = "KJ.WorkflowToolbox.Kind";
    public const string TitleKey = "KJ.WorkflowToolbox.Title";

    public static event Action<WorkflowToolboxItem, PointerRoutedEventArgs>? PointerDragBegan;

    public static void RaisePointerDragBegan(WorkflowToolboxItem item, PointerRoutedEventArgs e) =>
        PointerDragBegan?.Invoke(item, e);
}

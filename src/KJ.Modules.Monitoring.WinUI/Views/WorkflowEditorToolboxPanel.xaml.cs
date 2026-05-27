using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class WorkflowEditorToolboxPanel : UserControl
{
    public WorkflowEditorToolboxPanel() => InitializeComponent();

    private void OnToolboxPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed)
            return;

        if (sender is not FrameworkElement element || element.DataContext is not WorkflowToolboxItem item)
            return;

        WorkflowToolboxDrag.RaisePointerDragBegan(item, e);
        e.Handled = true;
    }

    private void OnToolboxTapped(object sender, TappedRoutedEventArgs e)
    {
        if (DataContext is not WorkflowEditorViewModel vm)
            return;
        if (sender is not FrameworkElement element || element.DataContext is not WorkflowToolboxItem item)
            return;

        vm.AddStepFromToolbox(item);
        e.Handled = true;
    }
}

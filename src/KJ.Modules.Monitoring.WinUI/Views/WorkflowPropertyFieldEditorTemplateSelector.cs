using KJ.Modules.Monitoring.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed class WorkflowPropertyFieldEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? DeviceTemplate { get; set; }
    public DataTemplate? PlcTypeTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item) =>
        Select(item) ?? base.SelectTemplateCore(item);

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) =>
        Select(item) ?? base.SelectTemplateCore(item, container);

    private DataTemplate? Select(object item)
    {
        if (item is not WorkflowStepPropertyFieldViewModel vm)
            return DefaultTemplate;

        if (vm.IsDeviceSelector)
            return DeviceTemplate ?? DefaultTemplate;

        if (vm.IsPlcTypeSelector)
            return PlcTypeTemplate ?? DefaultTemplate;

        return DefaultTemplate;
    }
}


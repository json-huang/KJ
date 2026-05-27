using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class WorkflowEditorLogPanel : UserControl
{
    public event EventHandler? CloseRequested;

    public WorkflowEditorLogPanel()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}

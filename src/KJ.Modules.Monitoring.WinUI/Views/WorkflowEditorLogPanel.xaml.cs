using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class WorkflowEditorLogPanel : UserControl
{
    public event EventHandler? CloseRequested;

    public WorkflowEditorLogPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (LogTabView.TabItems.Count > 0)
                LogTabView.SelectedIndex = 0;
        };
    }

    /// <summary>展开日志面板时切换到「运行输出」页签。</summary>
    public void SelectRunOutputTab()
    {
        if (LogTabView.TabItems.Count > 0)
            LogTabView.SelectedIndex = 0;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}

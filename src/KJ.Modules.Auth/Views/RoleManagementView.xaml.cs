using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Auth.Views;

public sealed partial class RoleManagementView : Page
{
    public RoleManagementView(ViewModels.RoleManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

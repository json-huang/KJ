using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Auth.Views;

public sealed partial class UserManagementView : Page
{
    public UserManagementView(ViewModels.UserManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void PasswordBox_OnPasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is PasswordBox box && DataContext is ViewModels.UserManagementViewModel vm)
            vm.NewPassword = box.Password;
    }
}

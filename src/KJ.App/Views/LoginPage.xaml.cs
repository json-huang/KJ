using Microsoft.UI.Xaml.Controls;

namespace KJ.App.Views;

public sealed partial class LoginPage : Page
{
    public LoginPage(ViewModels.LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box && DataContext is ViewModels.LoginViewModel vm)
            vm.Password = box.Password;
    }
}

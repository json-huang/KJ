using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Auth.Views;

public sealed partial class LoginView : Page
{
    public LoginView(ViewModels.LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void PasswordBox_OnPasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is PasswordBox box && DataContext is ViewModels.LoginViewModel vm)
            vm.Password = box.Password;
    }
}


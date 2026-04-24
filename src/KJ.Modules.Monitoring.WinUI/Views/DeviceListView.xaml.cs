using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using KJ.Modules.Monitoring.ViewModels;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class DeviceListView : Page
{
    public DeviceListView() => InitializeComponent();

    public DeviceListViewModel? ViewModel => DataContext as DeviceListViewModel;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        await ViewModel.LoadAsync();
    }
}


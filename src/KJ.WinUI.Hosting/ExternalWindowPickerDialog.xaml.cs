using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KJ.WinUI.Hosting;

public sealed partial class ExternalWindowPickerDialog : ContentDialog
{
    private readonly ExternalWindowEnumerator _enumerator = new();

    public ObservableCollection<ExternalWindowInfo> Windows { get; } = new();

    public ExternalWindowInfo? SelectedWindow { get; private set; }

    public ExternalWindowPickerDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => RefreshWindows();

    private void OnRefreshClick(object sender, RoutedEventArgs e) => RefreshWindows();

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SelectedWindow = WindowList.SelectedItem as ExternalWindowInfo;
        if (SelectedWindow is null)
            args.Cancel = true;
    }

    private void RefreshWindows()
    {
        Windows.Clear();
        foreach (var window in _enumerator.Enumerate())
            Windows.Add(window);

        EmptyHint.Text = Windows.Count == 0
            ? "没有找到可嵌入窗口。请先打开目标程序，再点击刷新。"
            : "如果目标窗口没有出现，请确认它不是管理员权限窗口、最小化窗口或无标题窗口。";
    }
}

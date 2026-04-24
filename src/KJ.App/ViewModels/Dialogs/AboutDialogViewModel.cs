using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;

namespace KJ.App.ViewModels.Dialogs;

public sealed class AboutDialogViewModel : BindableBase, IDialogAware
{
    private string _title = "关于 KJ";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _message = "WinUI 3 + Prism 模块化自动化框架（Prism.Dialogs 示例）。";
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public DelegateCommand CloseCommand { get; }

    public DialogCloseListener RequestClose { get; } = new();

    public AboutDialogViewModel() =>
        CloseCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.OK));

    public bool CanCloseDialog() => true;

    public void OnDialogClosed()
    {
    }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters.ContainsKey("title"))
            Title = parameters.GetValue<string>("title") ?? Title;
        if (parameters.ContainsKey("message"))
            Message = parameters.GetValue<string>("message") ?? Message;
    }
}

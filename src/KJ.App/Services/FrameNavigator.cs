using KJ.Modules.Auth;
using Microsoft.UI.Xaml.Controls;
using Prism.Ioc;

namespace KJ.App.Services;

public sealed class FrameNavigator : INavigator
{
    private readonly IContainerProvider _container;
    private readonly ISessionState _sessionState;
    private Frame? _frame;

    public FrameNavigator(IContainerProvider container, ISessionState sessionState)
    {
        _container = container;
        _sessionState = sessionState;
    }

    public void Attach(Frame frame) => _frame = frame;

    public void GoLogin()
    {
        if (_frame is null)
            return;
        _frame.Content = _container.Resolve<Views.LoginPage>();
    }

    public void GoMain()
    {
        if (_frame is null)
            return;
        _frame.Content = _container.Resolve<Views.MainPage>();
    }
}

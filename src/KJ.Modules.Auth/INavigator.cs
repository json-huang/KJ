using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Auth;

/// <summary>
/// Shell 内根 <see cref="Frame"/> 的轻量导航；实现位于 KJ.App。
/// </summary>
public interface INavigator
{
    void Attach(Frame frame);

    void GoLogin();

    /// <summary>
    /// 进入主界面；若当前未登录应回退到登录页（由实现类守卫）。
    /// </summary>
    void GoMain();
}

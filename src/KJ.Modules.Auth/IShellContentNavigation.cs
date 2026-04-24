using Microsoft.UI.Xaml.Controls;

namespace KJ.Modules.Auth;

/// <summary>主窗口内模块内容区导航（与根级登录/主界面 <see cref="INavigator"/> 分离）。</summary>
public interface IShellContentNavigation
{
    void Attach(ContentControl moduleContentHost);

    void Navigate(string routeKey);
}

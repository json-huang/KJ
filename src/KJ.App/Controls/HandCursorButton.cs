using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace KJ.App.Controls;

/// <summary>标题栏等需要手型光标的按钮。</summary>
public sealed class HandCursorButton : Button
{
    public HandCursorButton()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }
}

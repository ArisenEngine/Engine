using ArisenEditorFramework.Utilities;
using Avalonia.Controls;
using ReactiveUI;
using System.Reflection;

namespace ArisenEditor.Core.Views;

internal class MenuItemBarViewModel : ReactiveObject
{
    private Menu m_Menu;

    internal Menu Menu
    {
        get => m_Menu;
        set => this.RaiseAndSetIfChanged(ref m_Menu, value);
    }
    
    public MenuItemBarViewModel()
    {
        Menu = ControlsFactory.CreateMenu(Assembly.GetExecutingAssembly(), ControlsFactory.MenuType.Header);
    }
}
using System.Reflection;
using ArisenEditorFramework.Core.Models;
using ArisenEditorFramework.Utilities;
using ReactiveUI;

namespace ArisenEditor.Core.Views;

internal class MenuItemBarViewModel : ReactiveObject
{
    private System.Collections.ObjectModel.ObservableCollection<MenuItemModel> m_Items;
    public System.Collections.ObjectModel.ObservableCollection<MenuItemModel> Items
    {
        get => m_Items;
        set => this.RaiseAndSetIfChanged(ref m_Items, value);
    }
    
    public MenuItemBarViewModel()
    {
        var model = ControlsFactory.CreateMenuModel(System.AppDomain.CurrentDomain.GetAssemblies(), ControlsFactory.MenuType.Header);
        Items = new System.Collections.ObjectModel.ObservableCollection<MenuItemModel>(model);
    }
}
using System;
using ArisenEditorFramework.Core;
using ReactiveUI;

namespace ArisenEditor.Core.Views;

internal class HeaderViewModel : EditorPanelBase
{
    public override string Title => "Header";
    public override string Id => "Header";
    public override object Content => new HeaderView { DataContext = this };

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public MenuItemBarViewModel MenuViewModel { get; }

    public HeaderViewModel()
    {
        MenuViewModel = new MenuItemBarViewModel();
    }
}

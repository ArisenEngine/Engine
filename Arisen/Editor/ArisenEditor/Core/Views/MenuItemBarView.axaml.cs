using ArisenEditor.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DynamicData;

namespace ArisenEditor.Core.Views;

internal partial class MenuItemBarView : UserControl
{
    public MenuItemBarView()
    {
        InitializeComponent();
        DataContext = new MenuItemBarViewModel();
        LoadMenuItems();
    }

    internal void LoadMenuItems()
    {
        var viewModel = DataContext as MenuItemBarViewModel;
        Root.Children.Clear();
        Root.Children.Add(viewModel.Menu);
    }
}
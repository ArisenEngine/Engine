using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using ArisenEditorFramework.UI.Controls;
using ArisenEditor.ViewModels;
using ArisenEditor.Core.Services;

namespace ArisenEditor.Views;

public partial class AssetsBrowserView : UserControl
{
    public AssetsBrowserView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var folderGrid = this.FindControl<ArisenTreeView>("FolderGrid");
        if (folderGrid != null)
        {
            folderGrid.ItemDoubleTapped += OnFolderDoubleTapped;
        }

        var assetsGrid = this.FindControl<ArisenListView>("AssetsGrid");
        if (assetsGrid != null)
        {
            assetsGrid.ItemDoubleTapped += OnAssetDoubleTapped;
        }
    }

    private void OnFolderDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is AssetsBrowserViewModel vm && vm.SelectedFolder != null)
        {
            var node = vm.SelectedFolder;
            if (node.IsBranch)
            {
                node.IsExpanded = !node.IsExpanded;
            }
        }
    }

    private void OnAssetDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is AssetsBrowserViewModel vm && vm.SelectedAsset != null)
        {
            var node = vm.SelectedAsset;
            if (node.IsBranch)
            {
                vm.NavigateToFolder(node.Path);
            }
            else if (node.Name.EndsWith(".arisen"))
            {
                // User double clicked a scene. Load it.
                SceneManagerService.Instance.LoadScene(node.Path);
            }
        }
    }

    private void OnAssetsPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (DataContext is AssetsBrowserViewModel vm)
            {
                double zoomDelta = e.Delta.Y * 10.0;
                double newSize = vm.IconSize + zoomDelta;
                vm.IconSize = Math.Clamp(newSize, 32.0, 128.0);
                e.Handled = true;
            }
        }
    }
}

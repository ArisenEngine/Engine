using ArisenEditor.Utilities;
using ArisenEditor.ViewModels;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ArisenEditor.Views;

public partial class ContentView : UserControl
{
    private ContentViewModel m_ViewModel;
    public ContentView()
    {
        InitializeComponent();
        m_ViewModel = new ContentViewModel();
        DataContext = m_ViewModel;
    }
    
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        // Folder Tree
        FolderTree.RowPrepared += OnFolderTreeRowPrepared;
        FolderTree.RowClearing += OnFolderTreeRowClearing;

        FolderTree.DataContext = m_ViewModel;
        FolderTree.Bind(TreeDataGrid.SourceProperty, new Binding(nameof(m_ViewModel.FolderSource)));
        
        // Assets Tree
        ContentTree.ContextMenu = ControlsFactory.CreateContextMenu(ControlsFactory.MenuType.Project);

        ContentTree.DataContext = m_ViewModel;
        ContentTree.Bind(TreeDataGrid.SourceProperty, new Binding(nameof(m_ViewModel.ContentSource)));

    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        
        FolderTree.RowPrepared -= OnFolderTreeRowPrepared;
        FolderTree.RowClearing -= OnFolderTreeRowClearing;
    }

    private void OnFolderTreeRowPrepared(object? sender, TreeDataGridRowEventArgs args)
    {
        if (args != null && args.Row != null && args.Row.ContextMenu == null)
        {
            args.Row.ContextMenu = ControlsFactory.CreateContextMenu(ControlsFactory.MenuType.Project);
        }
    }
        
    private void OnFolderTreeRowClearing(object? sender, TreeDataGridRowEventArgs args)
    {
        if (args != null && args.Row != null)
        {
            args.Row.ContextMenu = null;
        }
    }
    
    private void FolderTree_OnRowDragStarted(object? sender, TreeDataGridRowDragStartedEventArgs e)
    {
        foreach (FileTreeNode i in e.Models)
        {
            if (i.AllowDrag)
            {
                e.AllowedEffects = DragDropEffects.Move;
            }
            else
            {
                e.AllowedEffects = DragDropEffects.None;
            }
        }
    }

    private void FolderTree_OnRowDragOver(object? sender, TreeDataGridRowDragEventArgs e)
    {
        if (e.Position == TreeDataGridRowDropPosition.Inside &&
            e.TargetRow.Model is FileTreeNode i &&
            !i.AllowDrop)
        {
            if (i.AllowDrop)
            {
                e.Inner.DragEffects = DragDropEffects.Move;
            }
            else
            {
                e.Inner.DragEffects = DragDropEffects.None;
            }
           
        }
    }

    private void FolderTree_OnRowDrop(object? sender, TreeDataGridRowDragEventArgs e)
    {
        
    }

    private void AssetsTree_OnRowDragStarted(object? sender, TreeDataGridRowDragStartedEventArgs e)
    {
        
    }

    private void AssetsTree_OnRowDragOver(object? sender, TreeDataGridRowDragEventArgs e)
    {
       
    }

    private void AssetsTree_OnRowDrop(object? sender, TreeDataGridRowDragEventArgs e)
    {
       
    }
}
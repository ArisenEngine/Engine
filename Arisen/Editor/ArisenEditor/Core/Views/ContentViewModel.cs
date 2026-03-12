using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using ArisenEngine;
using ReactiveUI;
using ArisenEngine.Core.Lifecycle;
using ArisenEditorFramework.Core;
using ArisenEditor.Views;
using ArisenEditor.Core.Services;

namespace ArisenEditor.ViewModels;

internal class ContentViewModel : EditorPanelBase
{
    private string m_AssetsSearchText = String.Empty;

    public override string Title => "Content";
    public override string Id => "ContentViewModel";
    public override object Content => new ContentView { DataContext = this };

    internal string AssetsSearchText
    {
        get { return m_AssetsSearchText; }
        set { this.RaiseAndSetIfChanged(ref m_AssetsSearchText, value); }
    }

    internal ContentViewModel()
    {
        InitializeFolderSource();
        InitializeAssetsSource();
    }

    #region Project Part

    internal HierarchicalTreeDataGridSource<FileTreeNode> FolderSource { get; set; }

    private void InitializeFolderSource()
    {
        FolderSource = new HierarchicalTreeDataGridSource<FileTreeNode>(Array.Empty<FileTreeNode>())
        {
            Columns =
            {
                new HierarchicalExpanderColumn<FileTreeNode>(
                    new TemplateColumn<FileTreeNode>(
                        "Project://",
                        "NameCell",
                        "NameEditCell",
                        new GridLength(1, GridUnitType.Star),
                        new TemplateColumnOptions<FileTreeNode>()
                        {
                            CompareAscending = FileTreeNode.SortAscending(x => x.Name),
                            CompareDescending = FileTreeNode.SortDescending(x => x.Name),
                            IsTextSearchEnabled = true,
                            TextSearchValueSelector = x => x.Name
                        }),
                    x => x.Children.OfType<FileTreeNode>(),
                    x => x.HasChildren,
                    x => x.IsExpanded
                ),
            }
        };

        FolderSource.RowSelection!.SingleSelect = false;
        
        if (Design.IsDesignMode)
        {
            return;
        }
        
        FolderSource.Items = new FileTreeNode[2]
        {
            new FileTreeNode("Content", Path.Combine(ArisenApplication.s_ProjectRoot, "Content"), true, isRoot: true, true)
            {
                AllowDrag = false,
                AllowDrop = false
            },
            new FileTreeNode("Dependencies", Path.Combine(ArisenApplication.s_ProjectRoot, "Dependencies"), true, isRoot: true,
                true)
            {
                AllowDrag = false,
                AllowDrop = false
            },
        };

        FolderSource.RowSelection.SelectionChanged += FolderSelectionChanged;
       
    }


    private FileTreeNode[] m_FolderSelections = new FileTreeNode[] { };

    public FileTreeNode[] FolderSelections
    {
        get
        {
            return m_FolderSelections;
        }

        set
        {
            this.RaiseAndSetIfChanged(ref m_FolderSelections, value);
        }
    }

    private void FolderSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileTreeNode> e)
    {
        
        m_FolderSelections = FolderSource.RowSelection.SelectedItems.ToArray();

        AssetsItems.Clear();
        foreach (var folderSelection in FolderSelections)
        {
            var folders = Directory.EnumerateDirectories(folderSelection.Path, "*", SearchOption.TopDirectoryOnly);
            var files = Directory.EnumerateFiles(folderSelection.Path, "*", SearchOption.TopDirectoryOnly);
            
            foreach (var folder in folders)
            {
                var folderName = Path.GetFileName(folder);
                var node = new FileTreeNode(folderName, folder, true);
                node.AssetGuid = AssetDatabaseService.Instance.GetGuidFromPath(folder);
                AssetsItems.Add(node);
            }
            
            foreach (var file in files)
            {
                if (file.EndsWith(".meta")) continue;
                
                var fileName = Path.GetFileName(file);
                var node = new FileTreeNode(fileName, file, false);
                node.AssetGuid = AssetDatabaseService.Instance.GetGuidFromPath(file);
                AssetsItems.Add(node);
            }
        }
        
        ContentSource.Items = AssetsItems;
    }

    #endregion


    #region Assets Part

    // Renamed from 'Content' to 'AssetsItems' to avoid conflict with EditorPanelBase.Content
    private ObservableCollection<FileTreeNode> m_AssetsItems = new ObservableCollection<FileTreeNode>();

    private ObservableCollection<FileTreeNode> AssetsItems
    {
        get => m_AssetsItems;
        set
        {
            this.RaiseAndSetIfChanged(ref m_AssetsItems, value);
        }
    }
    public FlatTreeDataGridSource<FileTreeNode> ContentSource { get; set; }


    private string m_ContentHeader;

    public string ContentHeader
    {
        get => m_ContentHeader;
        set { this.RaiseAndSetIfChanged(ref m_ContentHeader, value); }
    }

    private void InitializeAssetsSource()
    {
        // if (!Design.IsDesignMode)
        // {
        //     Assets.Clear();
        // }
        
        ContentSource = new FlatTreeDataGridSource<FileTreeNode>(Array.Empty<FileTreeNode>())
        {
            Columns =
            {
                new TemplateColumn<FileTreeNode>(
                    "Name",
                    "ContentNameCell",
                    "ContentNameEditCell",
                    new GridLength(1, GridUnitType.Star),
                    new TemplateColumnOptions<FileTreeNode>()
                    {
                        CompareAscending = FileTreeNode.SortAscending(x => x.Name),
                        CompareDescending = FileTreeNode.SortDescending(x => x.Name),
                        IsTextSearchEnabled = true,
                        TextSearchValueSelector = x => x.Name
                    }),

                new TextColumn<FileTreeNode, string>(
                    "Size",
                    node => node.SizeString),

                new TextColumn<FileTreeNode, DateTimeOffset>(
                    "Modified",
                    node => node.Modified),
            }
        };

        ContentSource.RowSelection!.SingleSelect = false;

        ContentSource.RowSelection.SelectionChanged += AssetsSelectionChanged;

        ContentSource.Items = AssetsItems;
        
    }

    private void AssetsSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileTreeNode> e)
    {
        m_ContentSelections = e.SelectedItems.ToArray();
    }

    private FileTreeNode[] m_ContentSelections;

    public FileTreeNode[] ContentSelections
    {
        get
        {
            if (m_ContentSelections == null)
            {
                m_ContentSelections = new FileTreeNode[] { };
            }

            return m_ContentSelections;
        }
    }

    #endregion
}
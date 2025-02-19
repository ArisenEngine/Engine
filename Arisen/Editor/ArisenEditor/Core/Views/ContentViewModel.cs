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

namespace ArisenEditor.ViewModels;

internal class ContentViewModel : BaseToolViewModel
{
    private string m_AssetsSearchText = String.Empty;

    internal string AssetsSearchText
    {
        get { return m_AssetsSearchText; }
        set { this.SetProperty(ref m_AssetsSearchText, value); }
    }

    internal ContentViewModel()
    {
        Id = "ContentViewModel";
        Title = "Content";
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
                    x => x.Children<FileTreeNode>(),
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
            this.SetProperty(ref m_FolderSelections, value);
        }
    }

    private void FolderSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileTreeNode> e)
    {
        
        m_FolderSelections = FolderSource.RowSelection.SelectedItems.ToArray();

        Assets.Clear();
        foreach (var folderSelection  in FolderSelections)
        {
            var folders = Directory.EnumerateDirectories(folderSelection.Path, "*", SearchOption.TopDirectoryOnly);
            var files = Directory.EnumerateFiles(folderSelection.Path, "*", SearchOption.TopDirectoryOnly);
            foreach (var folder in folders)
            {
                var folderName = folder.Split(Path.DirectorySeparatorChar)[^1];
                Assets.Add(new FileTreeNode(folderName, folder, true));
            }
            
            foreach (var file in files)
            {
                var fileName = file.Split(Path.DirectorySeparatorChar)[^1];
                Assets.Add(new FileTreeNode(fileName, file, false));
            }
        }
        
        AssetsSource.Items = Assets;
    }

    #endregion


    #region Assets Part

    // TODO: figure out why if dont add a default FileTreeNode, the AssetsView will not auto refresh when selection changed
    private ObservableCollection<FileTreeNode> m_Assets = new ObservableCollection<FileTreeNode>()
    {
        new FileTreeNode("Assets", Path.Combine(ArisenApplication.s_ProjectRoot, "Assets"), true, isRoot: true, true)
    };

    private ObservableCollection<FileTreeNode> Assets
    {
        get => m_Assets;
        set
        {
            this.SetProperty(ref m_Assets, value);
        }
    }
    public FlatTreeDataGridSource<FileTreeNode> AssetsSource { get; set; }


    private string m_AssetsHeader;

    public string AssetsHeader
    {
        get => m_AssetsHeader;
        set { this.SetProperty(ref m_AssetsHeader, value); }
    }

    private void InitializeAssetsSource()
    {
        // if (!Design.IsDesignMode)
        // {
        //     Assets.Clear();
        // }
        
        AssetsSource = new FlatTreeDataGridSource<FileTreeNode>(Array.Empty<FileTreeNode>())
        {
            Columns =
            {
                new TemplateColumn<FileTreeNode>(
                    "Name",
                    "AssetsNameCell",
                    "AssetsNameEditCell",
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

        AssetsSource.RowSelection!.SingleSelect = false;

        AssetsSource.RowSelection.SelectionChanged += AssetsSelectionChanged;

        var rows = AssetsSource.Rows;
        AssetsSource.Items = Assets;
        
    }

    private void AssetsSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileTreeNode> e)
    {
        m_AssetsSelections = e.SelectedItems.ToArray();
    }

    private FileTreeNode[] m_AssetsSelections;

    public FileTreeNode[] AssetsSelections
    {
        get
        {
            if (m_AssetsSelections == null)
            {
                m_AssetsSelections = new FileTreeNode[] { };
            }

            return m_AssetsSelections;
        }
    }

    #endregion
}
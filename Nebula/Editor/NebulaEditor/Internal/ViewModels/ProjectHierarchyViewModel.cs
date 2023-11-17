using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using NebulaEngine;
using ReactiveUI;

namespace NebulaEditor.ViewModels;

public class ProjectHierarchyViewModel : ViewModelBase
{
    private string m_AssetsSearchText = String.Empty;

    public string AssetsSearchText
    {
        get { return m_AssetsSearchText; }
        set { this.RaiseAndSetIfChanged(ref m_AssetsSearchText, value); }
    }

    public ProjectHierarchyViewModel()
    {
        InitializeFolderSource();
        InitializeAssetsSource();
    }

    #region Project Part

    public HierarchicalTreeDataGridSource<FileTreeNode> FolderSource { get; set; }

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

                new TextColumn<FileTreeNode, string>(
                    "Size",
                    node => node.SizeString),

                new TextColumn<FileTreeNode, DateTimeOffset>(
                    "Modified",
                    node => node.Modified),
            }
        };

        FolderSource.RowSelection!.SingleSelect = false;

        FolderSource.Items = new FileTreeNode[2]
        {
            new FileTreeNode("Assets", Path.Combine(GameApplication.projectRoot, "Assets"), true, isRoot: true, true),
            new FileTreeNode("Packages", Path.Combine(GameApplication.projectRoot, "Packages"), true, isRoot: true, true)
            {
                AllowDrag = false,
                AllowDrop = false
            },
        };

        FolderSource.RowSelection.SelectionChanged += FolderSelectionChanged;
    }


    private FileTreeNode[] m_FolderSelections;

    public FileTreeNode[] FolderSelections
    {
        get
        {
            if (m_FolderSelections == null)
            {
                m_FolderSelections = new FileTreeNode[] { };
            }

            return m_FolderSelections;
        }
    }

    private void FolderSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileTreeNode> e)
    {
        m_FolderSelections = e.SelectedItems.ToArray();
    }

    #endregion


    #region Assets Part

    public HierarchicalTreeDataGridSource<FileTreeNode> AssetsSource { get; set; }


    private string m_AssetsHeader;
    public string AssetsHeader
    {
        get => m_AssetsHeader;
        set
        {
            this.RaiseAndSetIfChanged(ref m_AssetsHeader, value);
        }
    }
    
    private void InitializeAssetsSource()
    {
        AssetsSource = new HierarchicalTreeDataGridSource<FileTreeNode>(Array.Empty<FileTreeNode>())
        {
            Columns =
            {
                new HierarchicalExpanderColumn<FileTreeNode>(
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
                    x => x.Children<FileTreeNode>(),
                    x => x.HasChildren,
                    x => x.IsExpanded
                ),

                new TextColumn<FileTreeNode, string>(
                    "Size",
                    node => node.SizeString),

                new TextColumn<FileTreeNode, DateTimeOffset>(
                    "Modified",
                    node => node.Modified),
            }
        };

        AssetsSource.RowSelection!.SingleSelect = false;

        AssetsSource.Items = new FileTreeNode[2]
        {
            new FileTreeNode("Assets", Path.Combine(GameApplication.projectRoot, "Assets"), true, isRoot: true, true),
            new FileTreeNode("Packages", Path.Combine(GameApplication.projectRoot, "Packages"), true, isRoot: true, true)
            {
                AllowDrag = false,
                AllowDrop = false
            },
        };

        AssetsSource.RowSelection.SelectionChanged += AssetsSelectionChanged;
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
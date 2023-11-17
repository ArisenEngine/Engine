using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using NebulaEngine;

namespace NebulaEditor.ViewModels;

public class ProjectHierarchyViewModel : ViewModelBase
{
    public string Header
    {
        get => "Project";
    }

    private void InitializeRoots()
    {
        m_Roots = new FolderTreeNode[2]
        {
            new FolderTreeNode("Assets", Path.Combine(GameApplication.projectRoot, "Assets"), true, isRoot: true, true),
            new FolderTreeNode("Packages", Path.Combine(GameApplication.projectRoot, "Packages"), true, isRoot: true, true),
        };
    }
    
    private string? m_SelectedPath;
    
    private FolderTreeNode[] m_Roots = null;
    
    private HierarchicalTreeDataGridSource<FolderTreeNode> m_FolderSource;

    public HierarchicalTreeDataGridSource<FolderTreeNode> FolderSource
    {
        get
        {
            if (m_FolderSource == null)
            {
                InitializeSource();
            }

            return m_FolderSource;
        }
    }

    public FolderTreeNode[] Roots
    {
        get
        {
            if (m_Roots == null)
            {
                InitializeRoots();
            }

            return m_Roots;
        }
    }
    
    private void InitializeSource()
    {
        m_FolderSource = new HierarchicalTreeDataGridSource<FolderTreeNode>(Array.Empty<FolderTreeNode>())
        {
            Columns =
            {
                new HierarchicalExpanderColumn<FolderTreeNode>(
                    new TemplateColumn<FolderTreeNode>(
                        Header,
                        "NameCell",
                        "NameEditCell",
                        new GridLength(1, GridUnitType.Star),
                        new TemplateColumnOptions<FolderTreeNode>()
                        {
                            CompareAscending = FolderTreeNode.SortAscending(x => x.Name),
                            CompareDescending = FolderTreeNode.SortDescending(x => x.Name),
                            IsTextSearchEnabled = true,
                            TextSearchValueSelector = x => x.Name
                        }),
                    x => x.Children<FolderTreeNode>(),
                    x => x.HasChildren,
                    x => x.IsExpanded
                ),
            }
        };
        
        m_FolderSource.RowSelection!.SingleSelect = false;
        
        m_FolderSource.Items = Roots;
        
        m_FolderSource.RowSelection.SelectionChanged += SelectionChanged;
        
    }

    private FolderTreeNode[] m_FolderSelections;

    public FolderTreeNode[] FolderSelections
    {
        get
        {
            if (m_FolderSelections == null)
            {
                m_FolderSelections = new FolderTreeNode[] { };
            }

            return m_FolderSelections;
        }
    }
    private void SelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FolderTreeNode> e)
    {
        m_FolderSelections = e.SelectedItems.ToArray();
    }
}
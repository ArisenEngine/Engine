using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using NebulaEngine;

namespace NebulaEditor.ViewModels;

public class ProjectHierarchyViewModel : ViewModelBase, IHierarchyVM<FolderTreeNode>
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
    
    private HierarchicalTreeDataGridSource<FolderTreeNode> m_Source;

    public HierarchicalTreeDataGridSource<FolderTreeNode> Source
    {
        get
        {
            if (m_Source == null)
            {
                InitializeSource();
            }

            return m_Source;
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
        m_Source = new HierarchicalTreeDataGridSource<FolderTreeNode>(Array.Empty<FolderTreeNode>())
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
        
        m_Source.RowSelection!.SingleSelect = false;
        
        m_Source.Items = Roots;
        
        m_Source.RowSelection.SelectionChanged += SelectionChanged;
        
    }

    private FolderTreeNode[] m_Selections;

    public FolderTreeNode[] Selections
    {
        get
        {
            if (m_Selections == null)
            {
                m_Selections = new FolderTreeNode[] { };
            }

            return m_Selections;
        }
    }
    private void SelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FolderTreeNode> e)
    {
        m_Selections = e.SelectedItems.ToArray();
    }
}
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using NebulaEngine;

namespace NebulaEditor.ViewModels;

public class FileTreeViewModel : IHierarchyVM<FileTreeNode>
{
    public bool IsFolderView = true;
    public string Header => IsFolderView ? "Project://" : "Assets";

    private void InitializeRoots()
    {
        m_Roots = new FileTreeNode[2]
        {
            new FileTreeNode("Assets", Path.Combine(GameApplication.projectRoot, "Assets"), true, isRoot: true, true),
            new FileTreeNode("Packages", Path.Combine(GameApplication.projectRoot, "Packages"), true, isRoot: true, true)
            {
                AllowDrag = false,
                AllowDrop = false
            },
        };
    }
    
    private string? m_SelectedPath;
    
    private FileTreeNode[] m_Roots = null;
    
    private HierarchicalTreeDataGridSource<FileTreeNode> m_Source;

    public HierarchicalTreeDataGridSource<FileTreeNode> Source
    {
        get
        {
            if (m_Source == null)
            {
                if (IsFolderView)
                {
                    InitializeFolderSource();
                }
                else
                {
                    InitializeAssetsSource();
                }
            }

            return m_Source;
        }
    }

    public FileTreeNode[] Roots
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
    
    private void InitializeFolderSource()
    {
        m_Source = new HierarchicalTreeDataGridSource<FileTreeNode>(Array.Empty<FileTreeNode>())
        {
            Columns =
            {
                new HierarchicalExpanderColumn<FileTreeNode>(
                    new TemplateColumn<FileTreeNode>(
                        Header,
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
        
        m_Source.RowSelection!.SingleSelect = false;
        
        m_Source.Items = Roots;
        
        m_Source.RowSelection.SelectionChanged += FolderSelectionChanged;
        
    }

    private void InitializeAssetsSource()
    {
        
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
}
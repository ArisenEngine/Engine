using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using NebulaEditor.Models;
using NebulaEngine;

namespace NebulaEditor.ViewModels;

public class WorldHierarchyViewModel : ViewModelBase, IHierarchyVM<SceneTreeNode>
{
    public string Header => "Hierarchy";

    private HierarchicalTreeDataGridSource<SceneTreeNode> m_Source;

    public HierarchicalTreeDataGridSource<SceneTreeNode> Source
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

    private SceneTreeNode[] m_Roots;
    public SceneTreeNode[] Roots
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
    
    private void InitializeRoots()
    {
        m_Roots = new SceneTreeNode[2]
        {
            new SceneTreeNode("New Scene", Path.Combine(NebulaApplication.s_ProjectRoot, "Assets"), true, isRoot: true),
            new SceneTreeNode("New Scene", Path.Combine(NebulaApplication.s_ProjectRoot, "Packages"), true, isRoot: true),
        };
    }

    private void InitializeSource()
    {
        m_Source = new HierarchicalTreeDataGridSource<SceneTreeNode>(Array.Empty<SceneTreeNode>())
        {
            Columns =
            {
                new HierarchicalExpanderColumn<SceneTreeNode>(
                    new TemplateColumn<SceneTreeNode>(
                        Header,
                        "NameCell",
                        "NameEditCell",
                        new GridLength(1, GridUnitType.Star),
                        new TemplateColumnOptions<SceneTreeNode>()
                        {
                            CompareAscending = null,
                            CompareDescending = null,
                            IsTextSearchEnabled = true,
                            TextSearchValueSelector = x => x.Name
                        }),
                    x => x.Children<SceneTreeNode>(),
                    x => x.HasChildren,
                    x => x.IsExpanded
                ),
                
                
            }
        };
        
        Source.RowSelection!.SingleSelect = false;
        Source.Items = Roots;
    }

    
}
using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using NebulaEditor.Models;
using NebulaEngine;

namespace NebulaEditor.ViewModels;

public class SceneHierarchyViewModel : HierarchyViewModel
{
    protected override string Header => null;

    protected override void InitializeRoots()
    {
        m_Roots = new SceneTreeNode[2]
        {
            new SceneTreeNode("New Scene", Path.Combine(GameApplication.projectRoot, "Assets"), true, isRoot: true),
            new SceneTreeNode("New Scene", Path.Combine(GameApplication.projectRoot, "Packages"), true, isRoot: true),
        };
    }

    protected override void InitializeSource()
    {
        m_Source = new HierarchicalTreeDataGridSource<TreeNodeBase>(Array.Empty<SceneTreeNode>())
        {
            Columns =
            {
                new HierarchicalExpanderColumn<TreeNodeBase>(
                    new TemplateColumn<TreeNodeBase>(
                        Header,
                        "NameCell",
                        "NameEditCell",
                        new GridLength(1, GridUnitType.Star),
                        new TemplateColumnOptions<TreeNodeBase>()
                        {
                            CompareAscending = null,
                            CompareDescending = null,
                            IsTextSearchEnabled = true,
                            TextSearchValueSelector = x => x.Name
                        }),
                    x => x.Children,
                    x => x.HasChildren,
                    x => x.IsExpanded
                ),
                
                
            }
        };
        
        Source.RowSelection!.SingleSelect = false;
        Source.Items = Roots;
    }
}
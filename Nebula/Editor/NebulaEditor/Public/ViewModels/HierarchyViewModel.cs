using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NebulaEditor.Models;
using NebulaEngine;

namespace NebulaEditor.ViewModels;

public class HierarchyViewModel : ViewModelBase
{
    private static IconConverter? s_iconConverter;

    protected HierarchicalTreeDataGridSource<TreeNodeBase> m_Source;

    public HierarchicalTreeDataGridSource<TreeNodeBase> Source
    {
        get
        {
            if (m_Source == null)
            {
                InitializeSource();
                
                Source.RowSelection.SelectionChanged += SelectionChanged;
                
            }
            
            return m_Source;
        }
    }
    
    protected TreeNodeBase[] m_Roots = null;
    public TreeNodeBase[] Roots
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
    protected virtual string Header => "Project";

    protected virtual void InitializeRoots()
    {
        m_Roots = new TreeNodeBase[2]
        {
            new TreeNodeBase("Assets", Path.Combine(GameApplication.projectRoot, "Assets"), true, isRoot: true),
            new TreeNodeBase("Packages", Path.Combine(GameApplication.projectRoot, "Packages"), true, isRoot: true),
        };
    }
    
    protected virtual void InitializeSource()
    {
        m_Source = new HierarchicalTreeDataGridSource<TreeNodeBase>(Array.Empty<TreeNodeBase>())
        {
            Columns =
            {
                 // new CheckBoxColumn<TreeNodeBase>(
                 //    null,
                 //    x => x.IsChecked,
                 //    (o, v) => o.IsChecked = v,
                 //    options: new()
                 //    {
                 //        CanUserResizeColumn = false,
                 //    }),
                 //
                new HierarchicalExpanderColumn<TreeNodeBase>(
                    new TemplateColumn<TreeNodeBase>(
                        Header,
                        "NameCell",
                        "NameEditCell",
                        new GridLength(1, GridUnitType.Star),
                        new TemplateColumnOptions<TreeNodeBase>()
                        {
                            CompareAscending = TreeNodeBase.SortAscending(x => x.Name),
                            CompareDescending = TreeNodeBase.SortDescending(x => x.Name),
                            IsTextSearchEnabled = true,
                            TextSearchValueSelector = x => x.Name
                        }),
                    x => x.Children,
                    x => x.HasChildren,
                    x => x.IsExpanded
                ),
                
                // new TextColumn<TreeNodeBase, long?>(
                //     "Size",
                //     x => x.Size,
                //     options: new()
                //     {
                //         CompareAscending = TreeNodeBase.SortAscending(x => x.Size),
                //         CompareDescending = TreeNodeBase.SortDescending(x => x.Size),
                //     }),
                //
                // new TextColumn<TreeNodeBase, DateTimeOffset?>(
                //     "Modified",
                //     x => x.Modified,
                //     options: new()
                //     {
                //         CompareAscending = TreeNodeBase.SortAscending(x => x.Modified),
                //         CompareDescending = TreeNodeBase.SortDescending(x => x.Modified),
                //     }),
                
            }
        };
        
        Source.RowSelection!.SingleSelect = false;
        
        Source.Items = Roots;
    }
    
    protected virtual void SelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<TreeNodeBase> e)
    {
        
    }

    public static IMultiValueConverter IconConverter
    {

        get
        {
            if (s_iconConverter is null)
            {
                s_iconConverter = new IconConverter();
            }

            return s_iconConverter;
        }
    }
}

class IconConverter : IMultiValueConverter
{
    /// <summary>
     /**
      * &lt;Binding Path="IsBranch"/&gt
      * &lt;Binding Path="IsExpanded"/&gt;
      * &lt;Binding Path="IsRoot"/&gt;
      */
    /// </summary>
    /// <param name="values"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values != null && values.Count >= 4 
                           && values[0] is bool isBranch 
                           && values[1] is bool IsExpanded
                           && values[2] is bool isRoot
                           && values[3] is TreeNodeBase treeNodeBase
                           )
        {
            if (isRoot)
            {
                return treeNodeBase.RootIcon;
            }
            
            if (isBranch)
            {
                if (IsExpanded)
                {
                    return treeNodeBase.BranchOpenIcon;
                }
                else
                {
                    return treeNodeBase.BranchIcon;
                }
            }
            
            return treeNodeBase.LeafIcon;
        }

        return null;
    }
}
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

    private HierarchicalTreeDataGridSource<TreeNodeBase> m_Source;

    public HierarchicalTreeDataGridSource<TreeNodeBase> Source
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

    private string m_LeafIconPath = "avares://NebulaEditor/Assets/Icons/file.png";
    private string m_BranchIconPath = "avares://NebulaEditor/Assets/Icons/folder.png";
    private string m_BranchOpenPath = "avares://NebulaEditor/Assets/Icons/open-folder.png";

    protected virtual string Header => "Project";
    
    protected TreeNodeBase[] m_Roots = new TreeNodeBase[2]
    {
        new TreeNodeBase("Assets",  Path.Combine(GameApplication.projectRoot, "Assets"), true, isRoot: true),
        new TreeNodeBase("Packages", Path.Combine(GameApplication.projectRoot, "Packages"), true, isRoot: true),
    };
    
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
        Source.RowSelection.SelectionChanged += SelectionChanged;
        Source.Items = m_Roots;
    }
    
    private void SelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<TreeNodeBase> e)
    {
        
    }

    public static IMultiValueConverter IconConverter
    {

        get
        {
            if (s_iconConverter is null)
            {
                using (var fileStream = AssetLoader.Open(new Uri("avares://NebulaEditor/Assets/Icons/file.png")))
                using (var folderStream = AssetLoader.Open(new Uri("avares://NebulaEditor/Assets/Icons/folder.png")))
                using (var folderOpenStream =
                       AssetLoader.Open(new Uri("avares://NebulaEditor/Assets/Icons/open-folder.png")))
                {
                    var fileIcon = new Bitmap(fileStream);
                    var folderIcon = new Bitmap(folderStream);
                    var folderOpenIcon = new Bitmap(folderOpenStream);

                    s_iconConverter = new IconConverter(fileIcon, folderOpenIcon, folderIcon);
                }
            }

            return s_iconConverter;
        }
    }
}

class IconConverter : IMultiValueConverter
{
    private readonly Bitmap _file;
    private readonly Bitmap _folderExpanded;
    private readonly Bitmap _folderCollapsed;

    public IconConverter(Bitmap file, Bitmap folderExpanded, Bitmap folderCollapsed)
    {
        _file = file;
        _folderExpanded = folderExpanded;
        _folderCollapsed = folderCollapsed;
    }
    
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {

        if (values.Count == 2 &&
            values[0] is bool isBranch &&
            values[1] is bool isExpanded)
        {
            if (!isBranch)
                return _file;
            else
                return isExpanded ? _folderExpanded : _folderCollapsed;
        }
        
        return null;
    }
}
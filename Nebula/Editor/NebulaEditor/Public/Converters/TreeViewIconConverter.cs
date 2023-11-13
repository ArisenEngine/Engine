using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using NebulaEditor.ViewModels;

namespace NebulaEditor.Converters;

class TreeViewIconConverter : IMultiValueConverter
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
                           && values[3] is TreeNodeBase treeNode
           )
        {
            if (isRoot)
            {
                return treeNode.RootIcon;
            }
            
            if (isBranch)
            {
                if (IsExpanded)
                {
                    return treeNode.BranchOpenIcon;
                }
                else
                {
                    return treeNode.BranchIcon;
                }
            }
            
            return treeNode.LeafIcon;
        }

        return null;
    }
}
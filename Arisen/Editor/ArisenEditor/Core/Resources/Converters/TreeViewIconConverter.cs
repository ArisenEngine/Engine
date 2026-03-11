using System;
using System.Collections.Generic;
using System.Globalization;
using ArisenEditor.ViewModels;
using Avalonia.Data.Converters;

namespace ArisenEditor.Converters;

internal class TreeViewIconConverter : IMultiValueConverter
{
    /// <summary>
    /// Converts multiple tree node properties into a single icon bitmap.
    /// Expected values: [bool IsBranch, bool IsExpanded, bool IsRoot, TreeNodeBase treeNode]
    /// </summary>
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values != null && values.Count >= 4 && values[3] is TreeNodeBase treeNode)
        {
            return treeNode.Icon;
        }

        return null;
    }
}
using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using NebulaEditor.Models;
using NebulaEditor.Utilities;
using NebulaEditor.ViewModels;

namespace NebulaEditor.Converters;

public class FolderIconConverter : IValueConverter
{
    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs? e)
    {
        Debug.WriteLine(sender);
    }
    
    private void OnContainerClearing(object? sender, ContainerClearingEventArgs? e)
    {
        Debug.WriteLine(sender);
    }
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        Debug.WriteLine($"Convert:{value}");
        if (value == null)
        {
            return null;
        }
        else
        {
            TreeViewItem item = value as TreeViewItem;
            item.ContainerPrepared += OnContainerPrepared;
            item.ContainerClearing += OnContainerClearing;
            
            if (item.IsExpanded)
            {
                return ImageHelper.LoadFromResource(AssetsHierarchyViewModel.FolderOpenIconPath);
            }
            else
            {
                return ImageHelper.LoadFromResource(AssetsHierarchyViewModel.FolderIconPath);
            }
        }
       
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
     
        throw new NotImplementedException();
    }
}
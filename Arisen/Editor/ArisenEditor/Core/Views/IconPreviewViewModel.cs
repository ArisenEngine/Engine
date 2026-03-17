using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ArisenEditorFramework.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ArisenEditor.ViewModels;

public class IconItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public Avalonia.Media.StreamGeometry? Geometry { get; set; }
}

public class IconPreviewViewModel : EditorPanelBase
{
    public override string Title => "Icon Preview";
    public override string Id => "IconPreview";

    public ObservableCollection<IconItemViewModel> Icons { get; } = new();

    public override object Content => new ArisenEditor.Views.IconPreviewView { DataContext = this };

    public IconPreviewViewModel()
    {
        // Try to load Icons.axaml directly or rely on Avalonia's Application.Current.Resources
        // Since they are merged in App.axaml, we can extract them globally:
            if (Application.Current != null)
            {
                // We search for StreamGeometry resources
                foreach (var kvp in Application.Current.Resources)
                {
                    if (kvp.Value is Avalonia.Media.StreamGeometry geom)
                    {
                        Icons.Add(new IconItemViewModel { Name = kvp.Key.ToString()!, Geometry = geom });
                    }
                }
                
                // Also need to search merged dictionaries
                foreach(var provider in Application.Current.Resources.MergedDictionaries)
                {
                    if (provider is Avalonia.Controls.ResourceDictionary dict)
                    {
                        foreach (var kvp in dict)
                        {
                            if (kvp.Value is Avalonia.Media.StreamGeometry geom)
                            {
                                Icons.Add(new IconItemViewModel { Name = kvp.Key.ToString()!, Geometry = geom });
                            }
                        }
                    }
                }
            }
        
        // Sort alphabetically
        var sorted = Icons.OrderBy(x => x.Name).ToList();
        Icons.Clear();
        foreach (var i in sorted) Icons.Add(i);
    }
}

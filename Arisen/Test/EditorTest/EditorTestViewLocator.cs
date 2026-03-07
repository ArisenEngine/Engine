using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ArisenEditorFramework.Docking;
using Dock.Model.Core;

namespace EditorTest;

public class EditorTestViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        var layoutManager = MainWindow.Instance?.LayoutManager;
        
        if (data is EditorWindowTool doc)
        {
            var content = doc.WindowContent;
            if (content is Control control) return control;
            return new TextBlock { Text = content?.ToString() ?? "Empty Content" };
        }
        
        if (data is IDockable dockable && layoutManager != null)
        {
             var window = layoutManager.GetWindow(dockable.Id);
             if (window != null)
             {
                 var content = window.GetContent();
                 if (content is Control control) return control;
                 return new TextBlock { Text = content.ToString() ?? "Empty Content" };
             }
             
             return new TextBlock { 
                 Text = $"Placeholder View: {dockable.Title} ({dockable.Id})", 
                 HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                 VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
             };
        }

        return new TextBlock { Text = "Invalid Data Type for ViewLocator" };
    }

    public bool Match(object? data)
    {
        return data is IDockable;
    }
}

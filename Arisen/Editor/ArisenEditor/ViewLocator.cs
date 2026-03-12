using System;
using ArisenEngine.Core.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Core;
using ReactiveUI;

namespace ArisenEditor;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is Control control)
        {
            return control;
        }

        var name = data?.GetType().FullName?.Replace("ViewModel", "View");
        if (name is null)
        {
            return new TextBlock { Text = "Invalid Data Type" };
        }

        var type = Type.GetType(name);
        if (type is { })
        {
            var instance = Activator.CreateInstance(type);
            if (instance is { })
            {
                var view = (Control)instance;
                view.DataContext = data;
                return view;
            }
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        // Match ViewModels or Controls (Views)
        return data is ReactiveObject ro && ro.GetType().Name.EndsWith("ViewModel") || data is Control;
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ArisenEditor.Core.Services;
using ArisenEditorFramework.Core;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Core;
using ReactiveUI;

namespace ArisenEditor;

/// <summary>
/// Robust, high-performance ViewLocator for Arisen Editor.
/// Follows DOD (Zero-allocation lookup) and handles Docking unwrapping.
/// </summary>
public class ViewLocator : IDataTemplate
{
    private static readonly ConcurrentDictionary<Type, Type?> _cache = new();
    
    // Predetermined patterns to avoid runtime string allocations
    private static readonly string[] NamespacePatterns = { ".ViewModels.", ".Views." };
    private static readonly string[] ViewModelSuffixes = { "ViewModel", "View" };

    public ViewLocator()
    {
        EditorLog.Log("[ViewLocator] Instantiated by Avalonia.");
    }

    public Control Build(object? data)
    {
        if (data == null) return new TextBlock { Text = "Data is null" };

        // Strategy 1: Unwrapping IDockable (Docking system often passes the model itself)
        if (data is IDockable dockable)
        {
            return Build(dockable.Context);
        }

        var vmType = data.GetType();
        
        // Strategy 2: Cached/Convention-based lookup
        var viewType = ResolveViewType(vmType);

        if (viewType != null)
        {
            try
            {
                var view = Activator.CreateInstance(viewType) as Control;
                if (view != null)
                {
                    view.DataContext = data;
                    return view;
                }
            }
            catch (Exception ex)
            {
                EditorLog.Error($"[ViewLocator] Failed to create {viewType.Name}: {ex.Message}");
            }
        }

        // Strategy 3: Explicit Content Fallback (for wrapped panels)
        if (data is IEditorPanel panel && panel.Content is Control explicitContent)
        {
            return explicitContent;
        }

        var error = $"View not found for {vmType.Name}. Searched in {vmType.Assembly.GetName().Name}";
        EditorLog.Warning($"[ViewLocator] {error}");
        return new TextBlock { Text = error };
    }

    private Type? ResolveViewType(Type vmType)
    {
        return _cache.GetOrAdd(vmType, type =>
        {
            var fullName = type.FullName;
            if (string.IsNullOrEmpty(fullName)) return null;

            // Strategy 1: Namespace Replacement (ViewModels -> Views)
            var viewName = fullName
                .Replace(NamespacePatterns[0], NamespacePatterns[1])
                .Replace(ViewModelSuffixes[0], ViewModelSuffixes[1]);
            
            var resolved = type.Assembly.GetType(viewName);
            if (resolved != null) return resolved;

            // Strategy 2: Simple Suffix Replacement (if flattened)
            viewName = fullName.Replace(ViewModelSuffixes[0], ViewModelSuffixes[1]);
            resolved = type.Assembly.GetType(viewName);
            if (resolved != null) return resolved;

            // Strategy 3: Global Views namespace guess
            var shortName = type.Name.Replace(ViewModelSuffixes[0], ViewModelSuffixes[1]);
            resolved = type.Assembly.GetType($"ArisenEditor.Views.{shortName}");
            if (resolved != null) return resolved;

            return null;
        });
    }

    public bool Match(object? data)
    {
        if (data == null || data is Control) return false;

        var type = data.GetType();
        
        // Match ViewModels, Panels, and Dockable containers
        bool isMatch = (data is ReactiveObject && type.Name.EndsWith("ViewModel")) || 
                       data is IEditorPanel ||
                       data is IDockable;
        return isMatch;
    }
}

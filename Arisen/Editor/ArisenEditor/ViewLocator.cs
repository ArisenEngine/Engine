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
            if (dockable.Context == null)
            {
                EditorLog.Warning($"[ViewLocator] IDockable {dockable.Id} has null Context.");
                return new TextBlock { Text = $"Context null for {dockable.Id}" };
            }
            return Build(dockable.Context);
        }

        // Strategy 2: Explicit Content Fallback (for wrapped panels)
        // Check this FIRST because it's the most direct way AEF works
        if (data is IEditorPanel panel && panel.Content is Control explicitContent)
        {
            return explicitContent;
        }

        var vmType = data.GetType();
        
        // Strategy 3: Cached/Convention-based lookup
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

            string[] suffixes = { "ViewModel", "View", "Control" };

            // Strategy 1: Namespace Replacement (ViewModels -> Views)
            var baseName = fullName.Replace(".ViewModels.", ".Views.");
            
            foreach (var suffix in suffixes)
            {
                if (fullName.EndsWith("ViewModel"))
                {
                    var viewName = baseName.Replace("ViewModel", suffix);
                    var resolved = type.Assembly.GetType(viewName);
                    if (resolved != null && resolved != type) return resolved;
                }
            }

            // Strategy 2: Simple Suffix Replacement (anywhere in string)
            foreach (var suffix in suffixes)
            {
                 var viewName = fullName.Replace("ViewModel", suffix);
                 var resolved = type.Assembly.GetType(viewName);
                 if (resolved != null && resolved != type) return resolved;
            }

            // Strategy 3: Global Views namespace guess
            var shortNameBase = type.Name.Replace("ViewModel", "");
            foreach (var suffix in new[] { "View", "Control" })
            {
                var resolved = type.Assembly.GetType($"ArisenEditor.Views.{shortNameBase}{suffix}");
                if (resolved != null && resolved != type) return resolved;
                
                resolved = type.Assembly.GetType($"ArisenEditor.Core.Views.{shortNameBase}{suffix}");
                if (resolved != null && resolved != type) return resolved;
            }

            return null;
        });
    }

    public bool Match(object? data)
    {
        if (data == null || data is Control || data is string) return false;

        // Unwrap IDockable to match its inner context. Dock.Avalonia passes the dockable item itself.
        if (data is IDockable dockable && dockable.Context != null && dockable.Context != dockable)
        {
            return Match(dockable.Context);
        }

        var type = data.GetType();
        
        // Match ViewModels and EditorPanels
        bool isMatch = (data is ReactiveObject && type.Name.EndsWith("ViewModel")) || 
                       data is IEditorPanel;
        
        return isMatch;
    }
}

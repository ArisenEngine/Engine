using System;
using System.Collections.Generic;
using ArisenEditorFramework.Core;
using ArisenEditor.ViewModels;
using ArisenEditor.Views;
using ArisenEditor.Core.Services;
using ArisenEditorFramework.Hierarchy;
using ArisenEditorFramework.Inspector;
using ReactiveUI;

namespace ArisenEditor.Core.Factory;

public class ArisenPanelFactory : DefaultPanelFactory
{
    private readonly SelectionService _selectionService = new();

    public void Initialize()
    {
        var hierarchyVM = new ArisenEditorFramework.Hierarchy.HierarchyViewModel();
        var inspectorVM = new ArisenEditorFramework.Inspector.InspectorViewModel();

        // Sync Selection
        hierarchyVM.WhenAnyValue(x => x.SelectedItem)
            .Subscribe(item => _selectionService.CurrentSelection = item);

        _selectionService.SelectionChanged += (obj) => inspectorVM.TargetObject = obj;

        // Register core panels
        RegisterPanel("Hierarchy", () => new EditorPanelWrapper("Hierarchy", "Hierarchy", new HierarchyControl { DataContext = hierarchyVM }));
        RegisterPanel("Inspector", () => new EditorPanelWrapper("Inspector", "Inspector", new InspectorControl { DataContext = inspectorVM }));
        
        RegisterPanel("Scene", () => new SceneViewModel());
        RegisterPanel("GameView", () => new GameViewModel());
        RegisterPanel("Console", () => new ConsoleViewModel());
        RegisterPanel("Assets", () => new AssetsBrowserViewModel());
        RegisterPanel("PackageManager", () => new PackageManagerViewModel());
        RegisterPanel("ProjectSettings", () => new ProjectSettingsViewModel());

        // Viewport and other placeholders
        RegisterPanel("Viewport", () => new EditorPanelWrapper("Viewport", "Viewport", new Avalonia.Controls.TextBlock { Text = "Viewport Placeholder", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }));
        RegisterPanel("Toolbar", () => new EditorPanelWrapper("Toolbar", "Toolbar", new Avalonia.Controls.TextBlock { Text = "Toolbar Placeholder" }));
    }
}

internal class EditorPanelWrapper : EditorPanelBase
{
    public override string Title { get; }
    public override string Id { get; }
    public override object Content { get; }

    public EditorPanelWrapper(string id, string title, object content)
    {
        Id = id;
        Title = title;
        Content = content;
    }
}

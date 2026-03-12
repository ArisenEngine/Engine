using System;
using System.Collections.Generic;
using ArisenEditorFramework.Core;
using ArisenEditor.ViewModels;
using ArisenEditor.Views;
using ArisenEditor.Core.Services;
using ArisenEditor.Core.Views;
using ArisenEditorFramework.Hierarchy;
using ArisenEditorFramework.Inspector;
using ReactiveUI;

namespace ArisenEditor.Core.Factory;

public class ArisenPanelFactory : DefaultPanelFactory
{
    private readonly SelectionService _selectionService = new();
    private readonly Dictionary<string, IEditorPanel> _panelCache = new();

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

        //other placeholders
        RegisterPanel("Toolbar", () => new EditorPanelWrapper("Toolbar", "Toolbar", new Avalonia.Controls.TextBlock { Text = "Toolbar Placeholder" }));
    }

    public override IEditorPanel CreatePanel(string panelId)
    {
        if (_panelCache.TryGetValue(panelId, out var cachedPanel))
        {
            return cachedPanel;
        }

        var panel = base.CreatePanel(panelId);
        _panelCache[panelId] = panel;
        return panel;
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

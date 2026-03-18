using System;
using System.Collections.Generic;
using ArisenEngine.Core.Lifecycle;
using ArisenEditorFramework.Core;
using ArisenEditorFramework.Hierarchy;
using ArisenEditorFramework.Inspector;
using ArisenEditor.Core.Services;
using ArisenEditor.Core.Views;
using ArisenEditor.ViewModels;
using ArisenEditor.Views;
using ReactiveUI;

namespace ArisenEditor.Core.Factory;

public class ArisenPanelFactory : DefaultPanelFactory
{
    private readonly SelectionService _selectionService = new();
    public ISelectionService SelectionService => _selectionService;
    private readonly SceneService _sceneService = new();
    private readonly Dictionary<string, IEditorPanel> _panelCache = new();

    public void Initialize()
    {
        // 1. Initialize Services
        _sceneService.InitializeNewScene();

        // 2. Initialize ViewModels
        // The view models for Hierarchy and Inspector are now created on demand within RegisterPanel
        // and directly fetch the active entity manager from the SceneSubsystem.

        // 3. Connect Scene to ViewModels (This part is now handled within the panel creation logic)
        // The direct subscription to _sceneService.WhenAnyValue(x => x.CurrentEntityManager) is removed
        // as the view models will get their initial EntityManager from the SceneSubsystem directly.
        // Updates to the EntityManager will need to be handled by the individual view models
        // subscribing to SceneSubsystem.ActiveEntityManager changes if dynamic updates are required.

        // 4. Connect Selection
        // The HierarchyViewModel will now be created when the panel is requested,
        // so the subscription needs to be set up differently or within the ViewModel itself.
        // For now, we'll assume the SelectionService is passed to the ViewModel and it handles its own subscriptions.
        _selectionService.SelectionChanged += (obj) =>
        {
            // This part might need adjustment if InspectorViewModel is not a singleton or globally accessible.
            // For now, we'll leave it as is, assuming the InspectorViewModel instance that is active
            // will somehow receive this update, or the TargetObject will be set when the panel is created.
            // A more robust solution would involve the InspectorViewModel subscribing to SelectionService.
        };

        // 5. Register core panels
        RegisterPanel("Hierarchy", () =>
        {
            var hierarchy = new ArisenEditor.ViewModels.HierarchyViewModel();
            hierarchy.SelectionService = _selectionService;
            // Connect selection for this specific hierarchy instance
            hierarchy.WhenAnyValue(x => x.SelectedItem)
                .Subscribe(item => _selectionService.CurrentSelection = item);
            return hierarchy;
        });
        RegisterPanel("Inspector", () =>
        {
            var inspector = new ArisenEditor.ViewModels.InspectorViewModel();
            inspector.SelectionService = _selectionService;
            // Connect selection for this specific inspector instance
            _selectionService.SelectionChanged += (obj) => inspector.TargetObject = obj;
            return inspector;
        });
        
        RegisterPanel("Scene", () => new SceneViewModel());
        RegisterPanel("GameView", () => new GameViewModel());
        RegisterPanel("Console", () => new ConsoleViewModel());
        RegisterPanel("Assets", () => new AssetsBrowserViewModel());
        RegisterPanel("PackageManager", () => new PackageManagerViewModel());
        RegisterPanel("ProjectSettings", () => new ProjectSettingsViewModel());

        RegisterPanel("Viewport", () => new EditorPanelWrapper("Viewport", "Viewport", new Avalonia.Controls.TextBlock { Text = "Viewport Placeholder", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }));
        RegisterPanel("IconPreview", () => new IconPreviewViewModel());
        RegisterPanel("Header", () => new HeaderViewModel());
        RegisterPanel("Toolbar", () => new ToolbarViewModel());
        RegisterPanel("Footer", () => new FooterViewModel());
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

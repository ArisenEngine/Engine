using Avalonia.Controls;
using Avalonia.Interactivity;
using ArisenEditorFramework.Docking;
using System.Collections.Generic;

namespace EditorTest;

public partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }
    public LayoutManager? LayoutManager => _layoutManager;

    private LayoutManager? _layoutManager;
    private MockCustomWindow? _mockWindow;

    public MainWindow()
    {
        Instance = this;
        InitializeComponent();
        
        _layoutManager = new LayoutManager();
        _layoutManager.Initialize();
        
        // Register the "Standard" Unity-like tools
        // We use the same IDs as in ArisenDockFactory.CreateLayout()
        var hierarchy = new HierarchyWindow();
        var inspector = new InspectorWindow();
        var console = new ConsoleWindow();
        var viewport = new ViewportWindow();
        var toolbar = new ToolbarWindow();

        // Register them so LayoutManager knows about them for ViewLocator lookups
        // Note: These are created once and kept alive.
        _layoutManager.RestoreCustomWindows(
            new List<IEditorWindow> { hierarchy, inspector, console, viewport, toolbar }, 
            new Dictionary<string, string>());
        
        var dockControl = this.FindControl<Dock.Avalonia.Controls.DockControl>("MainDockControl");
        if (dockControl != null)
        {
            dockControl.Layout = _layoutManager.Layout;
        }
        
        _mockWindow = new MockCustomWindow { Id = "MockWindow1", Title = "Test Tool" };
    }

    public void OpenTool()
    {
        if (_mockWindow != null)
        {
            _layoutManager?.OpenWindow(_mockWindow);
        }
    }

    public void SimulateHotReload()
    {
        if (_layoutManager == null || _mockWindow == null) return;

        // 1. Serialize all custom windows state
        var states = _layoutManager.SerializeCustomWindows();
        
        // 2. Serialize full docking layout
        var layoutData = _layoutManager.SaveLayout();
        
        // --- Imagine ALC Unload / Build / Reload here ---
        // For testing we just simulate the state restore process.

        // 3. Create NEW instances (normally this would be done by the new ALC loading)
        _mockWindow = new MockCustomWindow { Id = "MockWindow1", Title = "Test Tool" };
        var hierarchy = new HierarchyWindow();
        var inspector = new InspectorWindow();
        var console = new ConsoleWindow();
        var viewport = new ViewportWindow();
        var toolbar = new ToolbarWindow();

        // 4. Restore full layout data first
        _layoutManager.LoadLayout(layoutData);
        var dockControl = this.FindControl<Dock.Avalonia.Controls.DockControl>("MainDockControl");
        if (dockControl != null)
        {
            dockControl.Layout = _layoutManager.Layout;
        }
        
        // 5. Push state into new instances & Bind
        _layoutManager.RestoreCustomWindows(
            new List<IEditorWindow> { hierarchy, inspector, console, viewport, toolbar, _mockWindow }, 
            states);
    }
}

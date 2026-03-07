using Avalonia.Controls;
using Avalonia.Interactivity;
using ArisenEditorFramework.Docking;
using System.Collections.Generic;

namespace EditorTest;

public partial class MainWindow : Window
{
    private LayoutManager? _layoutManager;
    private MockCustomWindow? _mockWindow;

    public MainWindow()
    {
        InitializeComponent();
        
        _layoutManager = new LayoutManager();
        _layoutManager.Initialize();
        
        var dockControl = this.FindControl<Dock.Avalonia.Controls.DockControl>("MainDockControl");
        if (dockControl != null)
        {
            dockControl.Layout = _layoutManager.Layout;
        }
        
        _mockWindow = new MockCustomWindow { Id = "MockWindow1", Title = "Test Tool" };
    }

    private void OpenToolBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_mockWindow != null)
        {
            _layoutManager?.OpenWindow(_mockWindow);
        }
    }

    private void HotReloadBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_layoutManager == null || _mockWindow == null) return;

        // 1. Serialize all custom windows state
        var states = _layoutManager.SerializeCustomWindows();
        
        // 2. Serialize full docking layout
        var layoutData = _layoutManager.SaveLayout();
        
        // 3. Simulate ALC unloading by dropping the old instance
        _layoutManager.CloseWindow(_mockWindow);
        _mockWindow = null;
        
        // --- Imagine ALC Unload / Build / Reload here ---
        
        // 4. Instantiate NEW mock window
        _mockWindow = new MockCustomWindow { Id = "MockWindow1", Title = "Test Tool" };
        
        // 5. Restore full layout data first
        _layoutManager.LoadLayout(layoutData);
        var dockControl = this.FindControl<Dock.Avalonia.Controls.DockControl>("MainDockControl");
        if (dockControl != null)
        {
            dockControl.Layout = _layoutManager.Layout;
        }
        
        // 6. Push state into new instances & Bind
        _layoutManager.RestoreCustomWindows(new List<IEditorWindow> { _mockWindow }, states);
    }
}

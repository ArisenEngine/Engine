using System.Threading.Tasks;
using ArisenEditorFramework.Utilities;
using ArisenEngine;
using ArisenEngine.Core.Lifecycle;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ArisenEditor.Core.Views;

using ArisenEngine.Core.Diagnostics;
using ArisenEditor.Core.Services;
internal partial class MainEditorHostView : Window
{
    private ArisenFileSystemWatcher m_FileSystemWatcher;
    
    public MainEditorHostView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        var env = EngineKernel.Instance.GetSubsystem<EnvironmentSubsystem>();
        m_BaseTitle = env?.ProjectName ?? "Arisen Editor";
        UpdateTitleText();
            
        // File Watcher
        m_FileSystemWatcher = new ArisenFileSystemWatcher(env?.DataPath ?? string.Empty);
        ArisenFileSystemWatcher.Current = m_FileSystemWatcher;
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (ArisenApplication.Run("Arisen Instance (Attach to Editor)") != 0)
            {
                EditorLog.Error("Arisen instance run error.");
            }
                
        });
        
        ArisenEditor.Core.Services.SceneManagerService.Instance.PropertyChanged += OnSceneManagerPropertyChanged;
        this.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, OnWindowKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }
    
    private string m_BaseTitle = "Arisen Editor";
    
    private void OnSceneManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ArisenEditor.Core.Services.SceneManagerService.IsDirty) || 
            e.PropertyName == nameof(ArisenEditor.Core.Services.SceneManagerService.ActiveScene))
        {
            UpdateTitleText();
        }
    }
    
    private void UpdateTitleText()
    {
        var svc = ArisenEditor.Core.Services.SceneManagerService.Instance;
        var dirtyMark = svc.IsDirty ? "*" : "";
        var activeSceneName = svc.ActiveScene != null ? $" - {svc.ActiveScene.Name}{dirtyMark}" : "";
        
        // Ensure UI updates on the right thread
        Dispatcher.UIThread.Post(() => {
            this.Title = $"{m_BaseTitle}{activeSceneName}";
        });
    }

    private void OnWindowKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.S && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
        {
            var svc = ArisenEditor.Core.Services.SceneManagerService.Instance;
            if (svc.IsDirty && svc.ActiveScene != null)
            {
                svc.SaveCurrentScene();
                e.Handled = true;
            }
        }
    }
    
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        
        this.RemoveHandler(Avalonia.Input.InputElement.KeyDownEvent, OnWindowKeyDown);
        ArisenEditor.Core.Services.SceneManagerService.Instance.PropertyChanged -= OnSceneManagerPropertyChanged;
        EditorLog.Log("Close Editor Window.");
        ArisenEngine.Core.Lifecycle.ArisenApplication.ShutdownEngine();
        m_FileSystemWatcher.Dispose();
        m_FileSystemWatcher = null;
        ArisenApplication.RequestExit();
    }
}
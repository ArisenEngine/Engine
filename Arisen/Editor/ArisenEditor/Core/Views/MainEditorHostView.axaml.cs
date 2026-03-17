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
        Title = env?.ProjectName ?? "Arisen Editor";
            
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
        
    }
    
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        EditorLog.Log("Close Editor Window.");
        ArisenEngine.Core.Lifecycle.ArisenApplication.ShutdownEngine();
        m_FileSystemWatcher.Dispose();
        m_FileSystemWatcher = null;
        ArisenApplication.RequestExit();
    }
}
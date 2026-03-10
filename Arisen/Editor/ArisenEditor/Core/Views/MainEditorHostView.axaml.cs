using System.Threading.Tasks;
using ArisenEditorFramework.Utilities;
using ArisenEngine;
using ArisenEngine.Core.Lifecycle;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ArisenEditor.Core.Views;

using Logger = ArisenEngine.Core.Diagnostics.Logger;
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
        
        Title = ArisenApplication.s_ProjectName;
            
        // File Watcher
        m_FileSystemWatcher = new ArisenFileSystemWatcher(ArisenApplication.s_DataPath);
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (ArisenApplication.Run("Arisen Instance (Attach to Editor)") != 0)
            {
                Logger.Error("Arisen instance run error.");
            }
                
        });
        
    }
    
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        Logger.Log("Close Editor Window.");
        m_FileSystemWatcher.Dispose();
        m_FileSystemWatcher = null;
        ArisenApplication.Exit();
    }
}
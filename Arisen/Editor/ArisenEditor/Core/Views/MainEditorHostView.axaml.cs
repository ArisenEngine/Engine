
using System.Threading.Tasks;
using ArisenEditor.Utilities;
using ArisenEditor.ViewModels.Startup;
using ArisenEditor.Windows;
using ArisenEngine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ArisenEditor.Core.Views;

using Logger = ArisenEngine.Debug.Logger;
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
        m_FileSystemWatcher = new ArisenFileSystemWatcher();
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (ArisenInstance.Run("Arisen Instance (Attach to Editor)") != 0)
            {
                Logger.Error("Arisen instance run error.");
            }
                
        });
        
    }
    
    // TODO: remove this
    private async Task<int> LoadEditorConfigAsync(StartupWindowViewModel startupWindowViewModel)
    {
        int code = await startupWindowViewModel.LoadEditorConfigAsync();
        return code;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        Logger.Log("Close Editor Window.");
        m_FileSystemWatcher.Dispose();
        m_FileSystemWatcher = null;
        ArisenInstance.End();
            
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = new StartupWindowViewModel();

            int resultCode = LoadEditorConfigAsync(mainWindowViewModel).Result;
            if (resultCode != 0)
            {
                App.Shutdown(desktop);

                return;
            }

            desktop.MainWindow = new StartupWindowView()
            {
                DataContext = mainWindowViewModel
            };

            desktop.MainWindow.Show();
        }
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using ArisenEngine;
using System.Threading.Tasks;
using ArisenEditor.Utilities;
using ArisenEditor.ViewModels;
using ArisenEditor.ViewModels.Startup;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Threading;
using ArisenEngine.Debugger;
using ArisenEngine.Views.Rendering;

namespace ArisenEditor.Windows.MainEditor
{
    using Logger = ArisenEngine.Debug.Logger;
    public partial class MainEditorWindow : Window
    {
        private ArisenFileSystemWatcher m_FileSystemWatcher;
        
        public MainEditorWindow()
        {
            InitializeComponent();
        }

        /// <inheritdoc/>
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            this.Title = ArisenApplication.s_ProjectName;
            
            // File Watcher
            m_FileSystemWatcher = new ArisenFileSystemWatcher();

            // Menu Items
            HeaderMenuContainer.Children.Clear();
            HeaderMenuContainer.Children.Add(ControlsFactory.CreateMenu(ControlsFactory.MenuType.Header));
            
            // Preview
            SceneView.Children.Add(new RenderSurfaceView()
            {
                ParentWindow = this,
                IsSceneView = true,
                SurfaceType = ArisenEngine.Rendering.SurfaceType.SceneView,
                DataContext = new RenderSurfaceViewModel(true)
            });
            
            GameView.Children.Add(new RenderSurfaceView()
            {
                ParentWindow = this,
                IsSceneView = false,
                SurfaceType = ArisenEngine.Rendering.SurfaceType.GameView,
                DataContext = new RenderSurfaceViewModel(false)
            });
            
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ArisenInstance.Run("Arisen Instance (Attach to Editor)") != 0)
                {
                    Logger.Error("Arisen instance run error.");
                }
                
            });
            
        }

        // TODO: remove this
        private async Task<int> LoadEditorConfigAsync(MainWindowViewModel mainWindowViewModel)
        {
            int code = await mainWindowViewModel.LoadEditorConfigAsync();
            return code;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            Logger.Log("Close Editor Window.");
            m_FileSystemWatcher.Dispose();
            m_FileSystemWatcher = null;
            // ArisenInstance.End();
            
            if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindowViewModel = new MainWindowViewModel();

                int resultCode = LoadEditorConfigAsync(mainWindowViewModel).Result;
                if (resultCode != 0)
                {
                    App.Shutdown(desktop);

                    return;
                }

                desktop.MainWindow = new MainWindow()
                {
                    DataContext = mainWindowViewModel
                };

                desktop.MainWindow.Show();
            }
        }
    }
}
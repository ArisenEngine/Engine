
using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NebulaEditor.ViewModels.Startup;
using System.Threading.Tasks;
using Avalonia.Controls;
using NebulaEngine;

namespace NebulaEditor
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            GameApplication.IsDesignMode = Design.IsDesignMode;
            EnterNormally();
            
            base.OnFrameworkInitializationCompleted();
        }

        private async void EnterNormally()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                
                var mainWindowViewModel = new MainWindowViewModel();
                var splashWindow = new Splash();
                desktop.MainWindow = splashWindow;
                splashWindow.Show();

                int resultCode = await mainWindowViewModel.LoadEditorConfigAsync();
                if ( resultCode != 0 ) {

                    splashWindow.Close();
                    base.OnFrameworkInitializationCompleted();

                    System.Environment.Exit( resultCode );

                    return;
                }

               
                await Task.Delay( 1000 );

                var mainWindow = new Windows.MainWindow()
                {
                    DataContext = mainWindowViewModel
                };

                desktop.MainWindow = mainWindow;
                
                mainWindow.Show();

                splashWindow.Close();

            }
        }

        public object CreateView(Window window)
        {
            throw new System.NotImplementedException();
        }
        
        private static bool IsProduction()
        {
#if DEBUG
            return false;
#else
        return true;
#endif
        }

        public static void Shutdown(IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
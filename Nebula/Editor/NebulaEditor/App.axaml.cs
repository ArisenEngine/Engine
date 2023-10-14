using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using NebulaEditor.ViewModels.Startup;
using System.Threading.Tasks;

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
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindowViewModel = new MainWindowViewModel();
                var splashWindow = new Splash();
                desktop.MainWindow = splashWindow;
                splashWindow.Show();

                int resultCode = await mainWindowViewModel.LoadEditorConfigAsync(splashWindow);
                if ( resultCode != 0 ) {

                    splashWindow.Close();
                    base.OnFrameworkInitializationCompleted();

                    System.Environment.Exit( resultCode );

                    return;
                }

               
                await Task.Delay( 2000 );

                var mainWindow = new Windows.MainWindow()
                {
                    DataContext = mainWindowViewModel
                };

                desktop.MainWindow = mainWindow;
                

                mainWindow.Show();

                splashWindow.Close();

            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
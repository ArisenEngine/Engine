using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ArisenLauncher.Views;
using ArisenLauncher.Services;
using ArisenLauncher.ViewModels;

namespace ArisenLauncher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var log = new LogService();
        
        // Setup Global Exception Handling
        AppDomain.CurrentDomain.UnhandledException += (s, e) => 
            log.Critical("Unhandled AppDomain Exception", e.ExceptionObject as Exception);
        
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) => 
            log.Error("Unobserved Task Exception", e.Exception);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try 
            {
                var config = new ConfigService(log);
                var discovery = new EngineDiscoveryService(config, log);
                var process = new LauncherProcessService(log);
                var project = new ProjectService(log, config);
                
                var vm = new MainViewModel(config, discovery, process, project, log);
                
                desktop.MainWindow = new MainWindow
                {
                    DataContext = vm
                };
                
                log.Info("Launcher UI initialized successfully.");
            }
            catch (Exception ex)
            {
                log.Critical("Fatal error during launcher initialization.", ex);
                throw; // Re-throw to allow standard crash behavior after logging
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}

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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var config = new ConfigService();
            var discovery = new EngineDiscoveryService(config);
            var process = new LauncherProcessService();
            
            var vm = new MainViewModel(config, discovery, process);
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

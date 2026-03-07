using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace EditorTest;

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
            desktop.MainWindow = new MainWindow();
        }

        // Catch UI thread exceptions
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (s, e) =>
        {
            ArisenEngine.Core.Diagnostics.Logger.Fatal($"UI Thread Exception: {e.Exception}");
            // Optional: e.Handled = true; if we want to prevent crash, 
            // but for a test case we usually want to know it happened.
        };

        base.OnFrameworkInitializationCompleted();
    }
}

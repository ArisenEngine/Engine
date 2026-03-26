using System;
using Avalonia;
using Avalonia.ReactiveUI;

namespace ArisenLauncher.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // P8: Use a named Mutex to ensure only one instance of the launcher runs at a time
        using var mutex = new System.Threading.Mutex(true, "ArisenLauncher-SingleInstance-Mutex", out bool createdNew);
        if (!createdNew)
        {
            // Another instance is already running
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}

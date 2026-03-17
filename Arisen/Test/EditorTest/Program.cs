using System;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using ArisenEngine;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Lifecycle;

namespace EditorTest;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Thread.CurrentThread.Name = "MainThread";
        
        // Setup global exception handling for non-UI threads
        AppDomain.CurrentDomain.UnhandledException += (s, e) => 
        {
            Logger.Fatal($"Unhandled Exception: {e.ExceptionObject}");
            // Ensure logs are flushed on fatal exit
            ArisenApplication.ShutdownEngine();
        };
        
        TaskScheduler.UnobservedTaskException += (s, e) => 
        {
            Logger.Fatal($"Unobserved Task Exception: {e.Exception}");
            e.SetObserved();
        };

        try
        {
            ArisenApplication.InitializeLogging(false);
            Logger.Info("EditorTest application started.");
            
            Setup();
            
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Logger.Info("EditorTest application shutting down.");
            ArisenApplication.ShutdownEngine();
        }
    }

    static void Setup()
    {
        // Setup installation root
        string? installRoot = Environment.GetEnvironmentVariable("ARISEN_ENGINE_ROOT", EnvironmentVariableTarget.User);
        if (installRoot == null)
        {
            installRoot = AppDomain.CurrentDomain.BaseDirectory;
        }
        
        var config = new EngineConfig
        {
            AppName = "EditorTest",
            Platform = RuntimePlatform.Windows,
            StartupPath = installRoot
        };

        if (!ArisenApplication.InitializeEngine(config))
        {
            Console.WriteLine("[EditorTest] Failed to initialize engine via ArisenApplication.InitializeEngine.");
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
}

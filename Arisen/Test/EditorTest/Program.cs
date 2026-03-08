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
            Logger.Dispose();
        };
        
        TaskScheduler.UnobservedTaskException += (s, e) => 
        {
            Logger.Fatal($"Unobserved Task Exception: {e.Exception}");
            e.SetObserved();
        };

        try
        {
            Setup();
            
            Logger.Info("EditorTest application started.");
            
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Logger.Info("EditorTest application shutting down.");
            NativeRuntime.Shutdown();
            Logger.Dispose();
        }
    }

    static void Setup()
    {
        // Centralized Engine Initialization (Logger + Graphics RHI)
        if (!NativeRuntime.Initialize())
        {
            Console.WriteLine("[EditorTest] Failed to initialize engine via NativeRuntime.");
        }
        
        // Setup installation root
        string? installRoot = Environment.GetEnvironmentVariable("ARISEN_ENGINE_ROOT", EnvironmentVariableTarget.User);
        if (installRoot == null)
        {
            installRoot = AppDomain.CurrentDomain.BaseDirectory;
        }
        
        ArisenApplication.s_Platform = RuntimePlatform.Windows;
        ArisenApplication.s_StartupPath = installRoot;
        ArisenApplication.s_IsInEditor = true;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
}

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
        Setup();
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    static void Setup()
    {
        // Initialize Logger with editor flag
        Logger.Initialize(true);
        
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

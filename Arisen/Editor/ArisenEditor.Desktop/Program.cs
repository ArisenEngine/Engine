using System;
using System.Threading;
using ArisenEditor.GameDev;
using Avalonia;
using Avalonia.ReactiveUI;
using ArisenEngine;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.Diagnostics;

namespace ArisenEditor.Desktop.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Thread.CurrentThread.Name = "MainThread";
        
        // Setup global exception handling
        AppDomain.CurrentDomain.UnhandledException += (sender, e) => HandleGlobalException(e.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) => 
        {
            HandleGlobalException(e.Exception);
            e.SetObserved();
        };

        try
        {
            Setup();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            HandleGlobalException(ex);
        }
        finally
        {
            Logger.Dispose();

            // Force the OS to tear down the process. This avoids zombie processes 
            // caused by C++ native threads or unmanaged background C# tasks.
            Environment.Exit(Environment.ExitCode);
        }
    }

    private static void HandleGlobalException(Exception? ex)
    {
        if (ex == null) return;
        
        // Ensure log is recorded
        Logger.Error($"[GlobalException] {ex.Message}\n{ex.StackTrace}");
        
        // Show fatal error message box
        // We can't use MessageBoxUtility here because it might depend on Avalonia state that is already broken
        var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard("Fatal Error", 
            $"A fatal error occurred and the application must close.\n\nError: {ex.Message}\n\nPlease check logs for details.");
        
        // StartWithClassicDesktopLifetime might not have started or might be crashing, 
        // using ShowAsync() and potentially waiting a bit.
        box.ShowAsync().Wait(2000);
        
        // Final flush
        Logger.Dispose();
        Environment.Exit(1);
    }

    static void Setup()
    {
        ArisenApplication.s_IsInEditor = true;
        Logger.Initialize(true);
        ProjectSolution.InstallationRoot = Environment.GetEnvironmentVariable(ProjectSolution.INSTALLATION_ENV_VARIABLE, EnvironmentVariableTarget.User);
        if (ProjectSolution.InstallationRoot == null)
        {
            ProjectSolution.InstallationRoot = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            Environment.SetEnvironmentVariable(ProjectSolution.INSTALLATION_ENV_VARIABLE, ProjectSolution.InstallationRoot, EnvironmentVariableTarget.User);
        }
        
        
        ArisenApplication.s_Platform = RuntimePlatform.Windows;
        ArisenApplication.s_StartupPath = ProjectSolution.InstallationRoot;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
            // .UseReactiveUI();
}

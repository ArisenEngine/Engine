using System;
using System.Threading;
using ArisenEditor.Core.Services;
using ArisenEditor.GameDev;
using Avalonia;
using ArisenEngine.Core.Lifecycle;

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
            ArisenApplication.ShutdownEngine();

            // Force the OS to tear down the process. This avoids zombie processes 
            // caused by C++ native threads or unmanaged background C# tasks.
            Environment.Exit(Environment.ExitCode);
        }
    }

    private static void HandleGlobalException(Exception? ex)
    {
        if (ex == null) return;
        
        // Ensure log is recorded
        EditorLog.Error($"[GlobalException] {ex.Message}\n{ex.StackTrace}");
        
        // Show fatal error message box
        // We can't use MessageBoxUtility here because it might depend on Avalonia state that is already broken
        var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard("Fatal Error", 
            $"A fatal error occurred and the application must close.\n\nError: {ex.Message}\n\nPlease check logs for details.");
        
        // ShowAsync() will try to show the window. Since this is a fatal crash, 
        // we wait for the result to ensure the user has seen it.
        // If the dispatcher is already dead, this might return immediately or hang,
        // but it's better than an arbitrary 2s timeout.
        try 
        {
            var task = box.ShowAsync();
            task.Wait();
        }
        catch
        {
            // If showing the box fails (e.g. Avalonia is too broken), 
            // just proceed to shutdown.
        }
        
        // Final flush
        ArisenApplication.ShutdownEngine();
        Environment.Exit(1);
    }

    static void Setup()
    {
        ArisenApplication.s_IsInEditor = true;
        ArisenApplication.InitializeLogging(true);
        ProjectSolution.InstallationRoot = Environment.GetEnvironmentVariable(ProjectSolution.INSTALLATION_ENV_VARIABLE, EnvironmentVariableTarget.User);
        if (ProjectSolution.InstallationRoot == null)
        {
            ProjectSolution.InstallationRoot = AppContext.BaseDirectory;
            Environment.SetEnvironmentVariable(ProjectSolution.INSTALLATION_ENV_VARIABLE, ProjectSolution.InstallationRoot, EnvironmentVariableTarget.User);
        }
        
        
        ArisenApplication.s_Platform = RuntimePlatform.Windows;
        ArisenApplication.s_StartupPath = ProjectSolution.InstallationRoot;

        // Initialize Editor Logger
        var editorLogService = new ArisenEditor.Core.Services.EditorLogService("editor.log");
        ArisenEditor.Core.Services.EditorLog.Initialize(editorLogService);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
            // .UseReactiveUI();
}

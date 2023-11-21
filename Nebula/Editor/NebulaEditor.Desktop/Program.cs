using System;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using NebulaEditor.GameDev;
using NebulaEngine;

namespace NebulaEditor.Desktop.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
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
        ProjectSolution.InstallationRoot = Environment.GetEnvironmentVariable(ProjectSolution.INSTALLATION_ENV_VARIABLE, EnvironmentVariableTarget.User);
        if (ProjectSolution.InstallationRoot == null)
        {
            ProjectSolution.InstallationRoot = System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            Environment.SetEnvironmentVariable(ProjectSolution.INSTALLATION_ENV_VARIABLE, ProjectSolution.InstallationRoot, EnvironmentVariableTarget.User);
        }
        
        
        NebulaEngine.NebulaApplication.s_Platform = NebulaEngine.RuntimePlatform.Windows;
        NebulaEngine.NebulaApplication.s_StartupPath = ProjectSolution.InstallationRoot;
        NebulaEngine.NebulaApplication.s_IsInEditor = true;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}

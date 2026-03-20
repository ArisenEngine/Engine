using System.Threading;
using System.Threading.Tasks;
using ArisenEditorFramework.Lifecycle;
using ArisenEngine.Core.Lifecycle;

namespace ArisenEditor.Core.Lifecycle.BootSteps;

public class EngineInitializationStep : IBootStep
{
    public string Name => "Engine Initialization";
    public string Description => "Initializing core engine subsystems...";

    public async Task ExecuteAsync(BootContext context, CancellationToken cancellationToken = default)
    {
        var projectRoot = System.IO.Path.GetDirectoryName(context.ProjectPath);
        var projectName = System.IO.Path.GetFileNameWithoutExtension(context.ProjectPath);

        var config = new EngineConfig
        {
            AppName = "ArisenEditor",
            ProjectName = projectName,
            ProjectRoot = projectRoot,
            StartupPath = System.AppDomain.CurrentDomain.BaseDirectory,
            WindowWidth = 1280,
            WindowHeight = 720
        };

        if (System.OperatingSystem.IsWindows())
            config.Platform = ArisenKernel.Lifecycle.RuntimePlatform.Windows;
        else if (System.OperatingSystem.IsMacOS())
            config.Platform = ArisenKernel.Lifecycle.RuntimePlatform.macOS;

        if (!ArisenApplication.InitializeEngine(config))
        {
            context.Success = false;
            context.ErrorMessage = "Failed to initialize Arisen Engine core subsystems.";
            return;
        }

        await Task.CompletedTask;
    }
}


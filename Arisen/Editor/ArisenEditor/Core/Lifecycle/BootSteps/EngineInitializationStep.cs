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
        var config = new EngineConfig
        {
            AppName = "ArisenEditor"
        };

        if (!ArisenApplication.InitializeEngine(config))
        {
            context.Success = false;
            context.ErrorMessage = "Failed to initialize Arisen Engine core subsystems.";
            return;
        }

        await Task.CompletedTask;
    }
}

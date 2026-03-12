using System.Threading;
using System.Threading.Tasks;
using ArisenEditorFramework.Lifecycle;
using ArisenEditor.Core.Services;

namespace ArisenEditor.Core.Lifecycle.BootSteps;

public class AssetDatabaseInitializationStep : IBootStep
{
    public string Name => "Asset Database";
    public string Description => "Scanning assets and generating metadata...";

    public async Task ExecuteAsync(BootContext context, CancellationToken cancellationToken = default)
    {
        AssetDatabaseService.Instance.Initialize();
        await Task.CompletedTask;
    }
}

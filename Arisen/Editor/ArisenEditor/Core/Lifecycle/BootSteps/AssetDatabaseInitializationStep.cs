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
        var projectRoot = System.IO.Path.GetDirectoryName(context.ProjectPath);
        if (string.IsNullOrEmpty(projectRoot))
        {
            context.Success = false;
            context.ErrorMessage = $"Could not determine project root directory from path: {context.ProjectPath}";
            return;
        }

        AssetDatabaseService.Instance.Initialize(projectRoot);
        await Task.CompletedTask;
    }
}

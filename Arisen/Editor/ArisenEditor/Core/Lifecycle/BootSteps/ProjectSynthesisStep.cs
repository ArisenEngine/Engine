using System.Threading;
using System.Threading.Tasks;
using ArisenEditorFramework.Lifecycle;
using ArisenEngine;
using ArisenEngine.Core.Lifecycle;

namespace ArisenEditor.Core.Lifecycle.BootSteps;

public class ProjectSynthesisStep : IBootStep
{
    public string Name => "Project Synthesis";
    public string Description => "Loading project manifest and assembly metadata...";

    public async Task ExecuteAsync(BootContext context, CancellationToken cancellationToken = default)
    {
        // Set the project root for the engine
        var projectRoot = System.IO.Path.GetDirectoryName(context.ProjectPath);
        if (string.IsNullOrEmpty(projectRoot))
        {
            context.Success = false;
            context.ErrorMessage = $"Could not determine project root directory from path: {context.ProjectPath}";
            return;
        }
        ArisenApplication.s_ProjectRoot = projectRoot;
        await Task.Delay(800, cancellationToken);
    }
}

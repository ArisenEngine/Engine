using System.Threading.Tasks;
using ArisenEditorFramework.Lifecycle;
using ArisenEngine;
using ArisenEngine.Core.Lifecycle;

namespace ArisenEditor.Core.Lifecycle.BootSteps;

public class ProjectSynthesisStep : IBootStep
{
    public string Name => "Project Synthesis";
    public string Description => "Loading project manifest and assembly metadata...";

    public async Task ExecuteAsync(BootContext context)
    {
        // Actually set the project root for the engine
        ArisenApplication.s_ProjectRoot = System.IO.Path.GetDirectoryName(context.ProjectPath);
        await Task.Delay(800);
    }
}

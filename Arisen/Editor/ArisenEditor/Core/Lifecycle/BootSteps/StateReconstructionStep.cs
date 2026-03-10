using System.Threading.Tasks;
using ArisenEditorFramework.Lifecycle;

namespace ArisenEditor.Core.Lifecycle.BootSteps;

public class StateReconstructionStep : IBootStep
{
    public string Name => "State Reconstruction";
    public string Description => "Restoring editor workspace and entity hierarchies...";

    public async Task ExecuteAsync(BootContext context)
    {
        await Task.Delay(800);
    }
}

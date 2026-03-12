using System.Threading;
using System.Threading.Tasks;
using ArisenEditorFramework.Lifecycle;

namespace ArisenEditor.Core.Lifecycle.BootSteps;

public class ExecutionHandoverStep : IBootStep
{
    public string Name => "Execution Handover";
    public string Description => "Surrendering control to the main editor thread...";

    public async Task ExecuteAsync(BootContext context, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
    }
}

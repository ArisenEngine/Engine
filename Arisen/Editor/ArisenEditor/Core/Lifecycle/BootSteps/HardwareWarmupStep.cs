using System.Threading;
using System.Threading.Tasks;
using ArisenEditorFramework.Lifecycle;

namespace ArisenEditor.Core.Lifecycle.BootSteps;

public class HardwareWarmupStep : IBootStep
{
    public string Name => "Hardware Warmup";
    public string Description => "Initializing GPU compute buffers and shader caches...";

    public async Task ExecuteAsync(BootContext context, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
    }
}

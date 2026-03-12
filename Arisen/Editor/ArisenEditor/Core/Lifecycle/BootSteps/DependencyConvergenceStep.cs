using System.Threading;
using System.Threading.Tasks;
using ArisenEditorFramework.Lifecycle;

namespace ArisenEditor.Core.Lifecycle.BootSteps;

public class DependencyConvergenceStep : IBootStep
{
    public string Name => "Dependency Convergence";
    public string Description => "Resolving engine packages and external dependencies...";

    public async Task ExecuteAsync(BootContext context, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
    }
}

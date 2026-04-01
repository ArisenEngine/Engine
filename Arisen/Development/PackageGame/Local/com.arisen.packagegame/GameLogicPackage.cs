using ArisenKernel.Packages;
using ArisenKernel.Services;
using ArisenKernel.Diagnostics;

namespace PackageGame;

/// <summary>
/// Main entry point for the user game logic.
/// </summary>
public class GameLogicPackage : IPackageEntry
{
    public void OnLoad(IServiceRegistry registry)
    {
        KernelLog.Info("[GameLogic] Package Loaded.");

        // Verification of TaskGraph system
        var taskGraph = registry.GetService<ArisenEngine.Threading.ITaskGraph>();
        if (taskGraph != null)
        {
            TaskGraphTest.RunTest(taskGraph);
        }
        else
        {
            KernelLog.Warning("[GameLogic] ITaskGraph service not found! TaskGraphTest skipped.");
        }
    }

    public void OnUnload(IServiceRegistry registry)
    {
        KernelLog.Info("[GameLogic] Package Unloaded.");
    }
}

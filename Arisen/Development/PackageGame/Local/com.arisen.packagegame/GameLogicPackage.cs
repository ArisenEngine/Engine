using ArisenKernel.Diagnostics;
using ArisenKernel.Packages;
using ArisenKernel.Services;

namespace PackageGame;

/// <summary>
/// Main entry point for the user game logic.
/// </summary>
public class GameLogicPackage : IPackageEntry
{
    public void OnLoad(IServiceRegistry registry)
    {
        KernelLog.Info("[GameLogic] Package Loaded.");
    }

    public void OnUnload(IServiceRegistry registry)
    {
        KernelLog.Info("[GameLogic] Package Unloaded.");
    }
}

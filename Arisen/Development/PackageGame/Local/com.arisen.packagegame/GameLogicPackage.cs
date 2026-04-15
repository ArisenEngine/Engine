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

        // Verification of Mesh Rendering system
        var scene = EngineKernel.Instance.GetSubsystem<ArisenEngine.ECS.Lifecycle.SceneSubsystem>();
        if (scene != null)
        {
            MeshRenderTest.Setup(scene);
        }
        else
        {
            KernelLog.Warning("[GameLogic] SceneSubsystem not found! MeshRenderTest skipped.");
        }
    }

    public void OnUnload(IServiceRegistry registry)
    {
        KernelLog.Info("[GameLogic] Package Unloaded.");
    }
}

using ArisenKernel.Packages;
using ArisenKernel.Services;
using ArisenKernel.Diagnostics;
using ArisenKernel.Lifecycle;

namespace PackageGame;

/// <summary>
/// Main entry point for the user game logic.
/// </summary>
public class GameLogicPackage : IPackageEntry
{
    private System.Action? m_DeferredSetup;

    public void OnLoad(IServiceRegistry registry)
    {
        KernelLog.Info("[GameLogic] Package Loaded.");

        // MeshRenderTest needs IRHIDevice, which is registered in HardwareWarmupStep (after
        // Avalonia's WinUI compositor is up). OnLoad runs before that, so defer to the first
        // OnFrameEnd — by then the engine loop is running and the device is registered.
        m_DeferredSetup = RunDeferredSetup;
        EngineKernel.Instance.OnFrameEnd += m_DeferredSetup;
    }

    private void RunDeferredSetup()
    {
        if (m_DeferredSetup != null)
        {
            EngineKernel.Instance.OnFrameEnd -= m_DeferredSetup;
            m_DeferredSetup = null;
        }

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
        if (m_DeferredSetup != null)
        {
            EngineKernel.Instance.OnFrameEnd -= m_DeferredSetup;
            m_DeferredSetup = null;
        }
        KernelLog.Info("[GameLogic] Package Unloaded.");
    }
}

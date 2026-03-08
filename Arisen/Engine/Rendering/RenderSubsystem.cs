using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.RHI;
using ArisenEngine.Core.Memory;

namespace ArisenEngine.Rendering;

public class RenderSubsystem : ITickableSubsystem
{
    private RenderPipeline? m_CurrentPipeline;
    private RenderPipelineAsset? m_CurrentAsset;

    // Rendering should typically happen last in the frame
    public int Priority => 100;
    public EnginePhase InitPhase => EnginePhase.Init;

    public void Initialize()
    {
        using var _ = Profiler.Zone("RenderSubsystem.Initialize");
        Logger.Log("[RenderSubsystem] Initializing...");
    }

    public void Tick(float deltaTime)
    {
        using var _ = Profiler.Zone("RenderSubsystem.Tick");

        var asset = Graphics.currentRenderPipelineAsset;
        if (asset == null) return;

        // 1. Manage pipeline lifecycle
        if (!ReferenceEquals(m_CurrentAsset, asset))
        {
            m_CurrentPipeline?.Dispose();
            m_CurrentAsset = asset;
            m_CurrentPipeline = asset.InternalCreatePipeline();
        }

        if (m_CurrentPipeline == null) return;

        // 2. Prepare Context and Render per Surface
        foreach (var surfaceInfo in ArisenApplication.GetActiveSurfaces())
        {
            var surface = surfaceInfo.Surface;
            var device = RHISystem.GetOrCreateDevice(surface.SurfaceId);
            
            // Get the swapchain associated with this surface
            var swapChain = device.GetSurface().GetSwapChain();
            if (!swapChain.IsValid) continue;

            var context = new RenderContext(
                FrameArena.Instance,
                device,
                swapChain,
                EngineKernel.Instance.CurrentFrameIndex,
                deltaTime
            );

            // 3. Render
            var cameras = Array.Empty<Camera>(); // TODO: Get cameras from active world filtered by surface
            m_CurrentPipeline.InternalRender(context, cameras);
        }
    }

    public void Shutdown()
    {
        m_CurrentPipeline?.Dispose();
        m_CurrentPipeline = null;
        m_CurrentAsset = null;
    }

    public void Dispose()
    {
        Shutdown();
    }
}
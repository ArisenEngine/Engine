using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.RHI;
using ArisenEngine.Core.Memory;
using ArisenEngine.Core.ECS;

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
            // Fetch cameras from ECS
            var entityManager = EngineKernel.Instance.GetSubsystem<SceneSubsystem>()?.ActiveEntityManager;
            var cameras = Array.Empty<Camera>();

            if (entityManager != null)
            {
                var cameraPool = entityManager.GetPool<CameraComponent>();
                var transformPool = entityManager.GetPool<TransformComponent>();
                var cameraList = new List<Camera>();

                var cameraComponents = cameraPool.GetRawComponentArray();
                var cameraEntities = cameraPool.GetRawEntityArray();
                int camCount = cameraPool.Count;

                for (int i = 0; i < camCount; i++)
                {
                    Entity entity = cameraEntities[i];
                    if (transformPool.Has(entity))
                    {
                        ref var camComp = ref cameraComponents[i];
                        ref var transComp = ref transformPool.GetRef(entity);

                        cameraList.Add(new Camera
                        {
                            FieldOfView = camComp.VerticalFov,
                            NearClip = camComp.NearPlane,
                            FarClip = camComp.FarPlane,
                            ProjectionType = camComp.IsPerspective ? CameraProjectionType.Perspective : CameraProjectionType.Orthographic,
                            Position = transComp.Position,
                            // Convert Quaternion to Euler if needed, or update Camera to support Quaternions
                            Rotation = transComp.Position // Placeholder: Camera.cs uses Vector3 for rotation
                        });
                    }
                }
                cameras = cameraList.ToArray();
            }

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
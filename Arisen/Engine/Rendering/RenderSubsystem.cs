using System;
using ArisenEngine.Core.Diagnostics;
using ArisenKernel.Lifecycle;
using ArisenEngine.Core.RHI;
using ArisenEngine.Core.Memory;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Math;
using ArisenEngine.Core.Lifecycle;

namespace ArisenEngine.Rendering;

public class RenderSubsystem : ITickableSubsystem
{
    private RenderPipeline? m_CurrentPipeline;
    private RenderPipelineAsset? m_CurrentAsset;

    // Pre-allocated camera buffer to avoid per-frame allocations.
    // Only reallocated when the number of cameras grows beyond capacity.
    private Camera[] m_CameraBuffer = new Camera[4];
    private int m_CameraCount;

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
            // Fetch cameras from ECS �?zero-allocation path using pre-allocated buffer
            var entityManager = EngineKernel.Instance.GetSubsystem<SceneSubsystem>()?.ActiveEntityManager;
            m_CameraCount = 0;

            if (entityManager != null)
            {
                var cameraPool = entityManager.GetPool<CameraComponent>();
                var transformPool = entityManager.GetPool<TransformComponent>();

                var cameraComponents = cameraPool.GetRawComponentArray();
                var cameraEntities = cameraPool.GetRawEntityArray();
                int camCount = cameraPool.Count;

                // Ensure buffer capacity (only reallocates when cameras grow)
                if (camCount > m_CameraBuffer.Length)
                {
                    m_CameraBuffer = new Camera[camCount * 2];
                }

                for (int i = 0; i < camCount; i++)
                {
                    Entity entity = cameraEntities[i];
                    if (transformPool.Has(entity))
                    {
                        ref var camComp = ref cameraComponents[i];
                        ref var transComp = ref transformPool.GetRef(entity);

                        // Directly modify the struct in the array
                        ref Camera cam = ref m_CameraBuffer[m_CameraCount];
                        cam.FieldOfView = camComp.VerticalFov;
                        cam.NearClip = camComp.NearPlane;
                        cam.FarClip = camComp.FarPlane;
                        cam.ProjectionType = camComp.IsPerspective != 0 ? CameraProjectionType.Perspective : CameraProjectionType.Orthographic;
                        cam.Position = transComp.Position;
                        cam.Rotation = transComp.Rotation.QuaternionToEulerDegrees();
                        m_CameraCount++;
                    }
                }
            }

            // Pass a lightweight ReadOnlySpan to avoid intermediate array allocations
            ReadOnlySpan<Camera> cameras = m_CameraCount == 0 
                ? ReadOnlySpan<Camera>.Empty 
                : new ReadOnlySpan<Camera>(m_CameraBuffer, 0, m_CameraCount);

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

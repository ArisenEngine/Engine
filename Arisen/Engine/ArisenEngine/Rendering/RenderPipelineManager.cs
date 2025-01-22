
using ArisenEngine.Debug;

namespace ArisenEngine.Rendering;

public static class RenderPipelineManager
{
    static RenderPipelineAsset s_CurrentPipelineAsset;
    
    public static RenderPipeline currentPipeline { get; private set; }
    
    public static event Action<Camera[]> beginFrameRendering;
    public static event Action<Camera> beginCameraRendering;
    public static event Action<Camera[]> endFrameRendering;
    public static event Action<Camera> endCameraRendering;

    static void PrepareRenderPipeline(RenderPipelineAsset pipelineAsset)
    {
        
        if (!ReferenceEquals(s_CurrentPipelineAsset, pipelineAsset))
        {
            // Required because when switching to a RenderPipeline asset for the first time
            // it will call OnValidate on the new asset before cleaning up the old one. Thus we
            // reset the rebuild in order to cleanup properly.
            CleanupRenderPipeline();
            s_CurrentPipelineAsset = pipelineAsset;
        }
        
        
        if (s_CurrentPipelineAsset != null
            && (currentPipeline == null || currentPipeline.disposed))
        {
            currentPipeline = s_CurrentPipelineAsset.InternalCreatePipeline();
        }
    }

    #region Internal Part
    

    internal static void BeginFrameRendering( Camera[] cameras)
    {
        beginFrameRendering?.Invoke(cameras);
    }

    internal static void BeginCameraRendering(Camera camera)
    {
        beginCameraRendering?.Invoke(camera);
    }

    internal static void EndFrameRendering( Camera[] cameras)
    {
        endFrameRendering?.Invoke(cameras);
    }

    internal static void EndCameraRendering(Camera camera)
    {
        endCameraRendering?.Invoke(camera);
    }

    internal static void DoRenderLoop(RenderPipelineAsset pipe)
    {
        PrepareRenderPipeline(pipe);
        
        if (currentPipeline == null)
        {
            Logger.Warning("Current render pipeline is null");
            return;
        }
        
        currentPipeline.InternalRender();
    }
    
    internal static void CleanupRenderPipeline()
    {
        if (currentPipeline != null && !currentPipeline.disposed)
        {
            currentPipeline.Dispose();
            s_CurrentPipelineAsset = null;
            currentPipeline = null;
        }
    }

    #endregion
}
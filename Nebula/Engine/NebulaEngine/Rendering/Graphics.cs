using NebulaEngine.Debug;

namespace NebulaEngine.Rendering;

public static class Graphics
{
    internal static RenderPipelineAsset currentRenderPipelineAsset;
    public static void SetCurrentRenderPipeline(RenderPipelineAsset asset)
    {
        if (ReferenceEquals(currentRenderPipelineAsset, asset))
        {
            Logger.Warning($"{nameof(asset)} was already set.");
            return;
        }

        currentRenderPipelineAsset = asset;
    }
}
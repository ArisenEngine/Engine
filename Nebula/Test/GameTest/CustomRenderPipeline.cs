using Microsoft.VisualBasic.Logging;
using NebulaEngine.API;

namespace GameTest;

public class CustomRenderPipeline : NebulaEngine.Rendering.RenderPipeline
{
    private CustomRenderPipelineAsset m_Asset;
    public CustomRenderPipeline(CustomRenderPipelineAsset asset)
    {
        m_Asset = asset;
    }
    
    protected override void Render()
    {
       
    }

    protected override void OnDisposed()
    {
        
    }
}
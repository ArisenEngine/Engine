using NebulaEngine.Rendering;

namespace GameTest;

public class CustomRenderPipelineAsset : NebulaEngine.Rendering.RenderPipelineAsset
{
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(this);
    }

    protected override void BeforeSerialize()
    {
       
    }

    protected override void AfterDeserialize()
    {
       
    }
}
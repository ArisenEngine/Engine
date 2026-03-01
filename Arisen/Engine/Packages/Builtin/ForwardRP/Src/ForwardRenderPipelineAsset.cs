using ArisenEngine.Core.Diagnostics;

namespace ArisenEngine.Rendering;

public class ForwardRenderPipelineAsset : RenderPipelineAsset
{
    protected override RenderPipeline CreatePipeline()
    {
        return new ForwardRenderPipeline();
    }

    protected override void BeforeSerialize() { }
    protected override void AfterDeserialize() { }
}

using ArisenEngine.Core.Diagnostics;

using ArisenEngine.Core.Packages;

namespace ArisenEngine.Rendering;

[ArisenPackage("com.arisen.builtin.forward-rp")]
public class ForwardRenderPipelineAsset : RenderPipelineAsset
{
    protected override RenderPipeline CreatePipeline()
    {
        return new ForwardRenderPipeline();
    }

    protected override void BeforeSerialize() { }
    protected override void AfterDeserialize() { }
}

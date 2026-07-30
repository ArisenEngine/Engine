using Arisen.Native.RHI;
using ArisenEngine.Rendering;
using ArisenEngine.Rendering.Resources;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class StaticMeshMaterialPipelineSignatureTests
{
    private static readonly Guid s_ShaderGuid =
        Guid.Parse("8b962780-6146-4daf-b581-aa4188dd18a3");

    [Fact]
    public void EquivalentPreparedMaterialUsesExistingPipeline()
    {
        var current = CreateSignature();
        var replacement = CreateSignature();

        Assert.Equal(current, replacement);
        Assert.False(StaticMeshMaterialPipelinePolicy.RequiresPipelineRebuild(
            currentSlotValid: true,
            current,
            replacement));
    }

    [Fact]
    public void InvalidSlotOrPipelineInputChangeRequiresRebuild()
    {
        var current = CreateSignature();

        Assert.True(StaticMeshMaterialPipelinePolicy.RequiresPipelineRebuild(
            currentSlotValid: false,
            current,
            current));
        AssertRequiresRebuild(current, current with { ShaderGuid = Guid.NewGuid() });
        AssertRequiresRebuild(
            current,
            current with { ShaderDependencyStamp = new AssetDependencyStamp(18) });
        AssertRequiresRebuild(current, current with { ShaderVariantIdentity = "Vulkan|variant-b" });
        AssertRequiresRebuild(
            current,
            current with
            {
                RenderState = current.RenderState with
                {
                    CullMode = ECullModeFlagBits.CULL_MODE_BACK_BIT
                }
            });
        AssertRequiresRebuild(current, current with { RenderQueue = RenderQueueInfo.Transparent });
    }

    private static StaticMeshMaterialPipelineSignature CreateSignature()
    {
        return new StaticMeshMaterialPipelineSignature(
            s_ShaderGuid,
            new AssetDependencyStamp(17),
            "Vulkan|variant-a",
            MaterialRenderState.Default,
            RenderQueueInfo.Opaque);
    }

    private static void AssertRequiresRebuild(
        StaticMeshMaterialPipelineSignature current,
        StaticMeshMaterialPipelineSignature incoming)
    {
        Assert.True(StaticMeshMaterialPipelinePolicy.RequiresPipelineRebuild(
            currentSlotValid: true,
            current,
            incoming));
    }
}

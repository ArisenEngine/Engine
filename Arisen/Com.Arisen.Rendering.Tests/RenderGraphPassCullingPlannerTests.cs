using Arisen.DAG;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderGraphPassCullingPlannerTests
{
    [Fact]
    public void FindCulledPasses_OutputOwnershipRetainsProducerChain()
    {
        var sceneColor = Resource("SceneColor", 1);
        var frameColor = Resource("FrameColor", 2);
        var accesses = new[]
        {
            Write(sceneColor, 10, RenderResourceState.TransferWrite),
            Read(sceneColor, 20, RenderResourceState.ShaderRead),
            Write(frameColor, 20, RenderResourceState.ColorAttachment),
            Write(frameColor, 30, RenderResourceState.OutputOwnership)
        };

        var culled = RenderGraphPassCullingPlanner.FindCulledPasses(
            new uint[] { 10, 20, 30 },
            accesses,
            Array.Empty<GraphEdge>());

        Assert.Empty(culled);
    }

    [Fact]
    public void FindCulledPasses_SideEffectPassRetainsExplicitPredecessor()
    {
        var predecessorOutput = Resource("PredecessorOutput", 1);
        var unusedOutput = Resource("UnusedOutput", 2);
        var accesses = new[]
        {
            Write(predecessorOutput, 10, RenderResourceState.TransferWrite),
            Write(unusedOutput, 30, RenderResourceState.TransferWrite)
        };
        var dependencies = new[]
        {
            new GraphEdge(10, 0, 20, 0)
        };

        var culled = RenderGraphPassCullingPlanner.FindCulledPasses(
            new uint[] { 10, 20, 30 },
            accesses,
            dependencies);

        Assert.Equal(new uint[] { 30 }, culled);
    }

    [Fact]
    public void FindCulledPasses_RemovesUnusedProducer()
    {
        var unusedOutput = Resource("UnusedOutput", 1);
        var accesses = new[]
        {
            Write(unusedOutput, 10, RenderResourceState.TransferWrite)
        };

        var culled = RenderGraphPassCullingPlanner.FindCulledPasses(
            new uint[] { 10, 20 },
            accesses,
            Array.Empty<GraphEdge>());

        Assert.Equal(new uint[] { 10 }, culled);
    }

    private static RenderResource Resource(string name, uint id)
    {
        return new RenderResource(name, RenderResourceType.Texture, id);
    }

    private static RenderGraphResourceAccess Read(
        RenderResource resource,
        uint passNodeId,
        RenderResourceState state)
    {
        return new RenderGraphResourceAccess(
            resource.ResourceId,
            passNodeId,
            RenderGraphResourceAccessKind.Read,
            state);
    }

    private static RenderGraphResourceAccess Write(
        RenderResource resource,
        uint passNodeId,
        RenderResourceState state)
    {
        return new RenderGraphResourceAccess(
            resource.ResourceId,
            passNodeId,
            RenderGraphResourceAccessKind.Write,
            state);
    }
}

using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderGraphResourceLifetimePlannerTests
{
    [Fact]
    public void BuildLifetimePlan_TracksOverlappingTransientTextureIntervals()
    {
        var sceneColor = TransientTexture("SceneColor", 1);
        var frameDepth = TransientTexture("FrameDepth", 2);
        var sortedNodeIds = new uint[] { 10, 20, 30, 40 };
        var accesses = new[]
        {
            Access(sceneColor, 10),
            Access(frameDepth, 20),
            Access(sceneColor, 30),
            Access(frameDepth, 40)
        };

        var lifetimes = RenderGraphResourceLifetimePlanner.BuildLifetimePlan(
            new[] { sceneColor, frameDepth },
            sortedNodeIds,
            sortedNodeIds,
            accesses);

        Assert.Collection(
            lifetimes,
            lifetime => AssertLifetime(lifetime, sceneColor, 0, 2, 10, 30, 2),
            lifetime => AssertLifetime(lifetime, frameDepth, 1, 3, 20, 40, 2));
        Assert.True(lifetimes[0].Overlaps(lifetimes[1]));
        Assert.Equal(2, RenderGraphResourceLifetimePlanner.GetPeakLiveTextureCount(lifetimes));
    }

    [Fact]
    public void BuildLifetimePlan_TracksNonOverlappingTransientTextureIntervals()
    {
        var shadowMap = TransientTexture("DirectionalShadowMap", 1);
        var sceneColor = TransientTexture("SceneColor", 2);
        var sortedNodeIds = new uint[] { 10, 20, 30, 40 };
        var accesses = new[]
        {
            Access(shadowMap, 10),
            Access(shadowMap, 20),
            Access(sceneColor, 30),
            Access(sceneColor, 40)
        };

        var lifetimes = RenderGraphResourceLifetimePlanner.BuildLifetimePlan(
            new[] { shadowMap, sceneColor },
            sortedNodeIds,
            sortedNodeIds,
            accesses);

        Assert.Collection(
            lifetimes,
            lifetime => AssertLifetime(lifetime, shadowMap, 0, 1, 10, 20, 2),
            lifetime => AssertLifetime(lifetime, sceneColor, 2, 3, 30, 40, 2));
        Assert.False(lifetimes[0].Overlaps(lifetimes[1]));
        Assert.Equal(1, RenderGraphResourceLifetimePlanner.GetPeakLiveTextureCount(lifetimes));
    }

    [Fact]
    public void BuildLifetimePlan_IgnoresAccessesFromCulledPasses()
    {
        var sceneColor = TransientTexture("SceneColor", 1);
        var culledOnly = TransientTexture("CulledOnly", 2);
        var accesses = new[]
        {
            Access(sceneColor, 10),
            Access(sceneColor, 20),
            Access(sceneColor, 30),
            Access(culledOnly, 20)
        };

        var lifetimes = RenderGraphResourceLifetimePlanner.BuildLifetimePlan(
            new[] { sceneColor, culledOnly },
            new uint[] { 10, 30 },
            new uint[] { 10, 30 },
            accesses);

        var lifetime = Assert.Single(lifetimes);
        AssertLifetime(lifetime, sceneColor, 0, 1, 10, 30, 2);
    }

    [Fact]
    public void BuildLifetimePlan_IgnoresAccessesFromZeroWorkPasses()
    {
        var firstTexture = TransientTexture("FirstTexture", 1);
        var inactiveOnly = TransientTexture("InactiveOnly", 2);
        var lastTexture = TransientTexture("LastTexture", 3);
        var accesses = new[]
        {
            Access(firstTexture, 10),
            Access(firstTexture, 20),
            Access(inactiveOnly, 20),
            Access(lastTexture, 20),
            Access(lastTexture, 30)
        };

        var lifetimes = RenderGraphResourceLifetimePlanner.BuildLifetimePlan(
            new[] { firstTexture, inactiveOnly, lastTexture },
            new uint[] { 10, 20, 30 },
            new uint[] { 10, 30 },
            accesses);

        Assert.Collection(
            lifetimes,
            lifetime => AssertLifetime(lifetime, firstTexture, 0, 0, 10, 10, 1),
            lifetime => AssertLifetime(lifetime, lastTexture, 2, 2, 30, 30, 1));
        Assert.False(lifetimes[0].Overlaps(lifetimes[1]));
    }

    [Fact]
    public void BuildLifetimePlan_ExcludesImportedTextures()
    {
        var frameColor = new RenderResource(
            "FrameColor",
            RenderResourceType.Texture,
            0,
            isImported: true,
            initialState: RenderResourceState.OutputOwnership);
        var sceneColor = TransientTexture("SceneColor", 1);

        var lifetimes = RenderGraphResourceLifetimePlanner.BuildLifetimePlan(
            new[] { frameColor, sceneColor },
            new uint[] { 10 },
            new uint[] { 10 },
            new[] { Access(frameColor, 10), Access(sceneColor, 10) });

        var lifetime = Assert.Single(lifetimes);
        Assert.Equal(sceneColor.ResourceId, lifetime.ResourceId);
    }

    [Fact]
    public void BuildLifetimePlan_RejectsActivePassMissingFromCompiledOrder()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            RenderGraphResourceLifetimePlanner.BuildLifetimePlan(
                Array.Empty<RenderResource>(),
                new uint[] { 10, 20 },
                new uint[] { 10, 30 },
                Array.Empty<RenderGraphResourceAccess>()));

        Assert.Contains("node 30 is absent", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildLifetimePlan_RejectsActivePassesOutsideCompiledOrder()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            RenderGraphResourceLifetimePlanner.BuildLifetimePlan(
                Array.Empty<RenderResource>(),
                new uint[] { 10, 20 },
                new uint[] { 20, 10 },
                Array.Empty<RenderGraphResourceAccess>()));

        Assert.Contains("unique ordered subset", error.Message, StringComparison.Ordinal);
    }

    private static RenderResource TransientTexture(string name, uint id)
    {
        return new RenderResource(name, RenderResourceType.Texture, id);
    }

    private static RenderGraphResourceAccess Access(RenderResource resource, uint passNodeId)
    {
        return new RenderGraphResourceAccess(
            resource.ResourceId,
            passNodeId,
            RenderGraphResourceAccessKind.Write,
            RenderResourceState.TransferWrite);
    }

    private static void AssertLifetime(
        RenderGraphTransientTextureLifetime lifetime,
        RenderResource resource,
        int firstPassIndex,
        int lastPassIndex,
        uint firstPassNodeId,
        uint lastPassNodeId,
        int accessingPassCount)
    {
        Assert.True(lifetime.IsValid);
        Assert.Equal(resource.ResourceId, lifetime.ResourceId);
        Assert.Equal(firstPassIndex, lifetime.FirstPassIndex);
        Assert.Equal(lastPassIndex, lifetime.LastPassIndex);
        Assert.Equal(firstPassNodeId, lifetime.FirstPassNodeId);
        Assert.Equal(lastPassNodeId, lifetime.LastPassNodeId);
        Assert.Equal(accessingPassCount, lifetime.AccessingPassCount);
    }
}

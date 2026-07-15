using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderGraphResourcePlannerTests
{
    [Fact]
    public void BuildTransitionPlan_TracksTransientWriteReadWriteStateChanges()
    {
        var resource = TransientTexture("Lighting", 2);
        var accesses = new[]
        {
            Write(resource, 10, RenderResourceState.TransferWrite),
            Read(resource, 20, RenderResourceState.ShaderRead),
            Write(resource, 30, RenderResourceState.TransferWrite)
        };

        var transitions = RenderGraphResourcePlanner.BuildTransitionPlan(
            new[] { resource },
            new uint[] { 10, 20, 30 },
            accesses,
            PassName);

        Assert.Collection(
            transitions,
            transition =>
            {
                Assert.Equal(resource.ResourceId, transition.ResourceId);
                Assert.Equal(10u, transition.BeforePassNodeId);
                Assert.Equal(RenderResourceState.Unknown, transition.FromState);
                Assert.Equal(RenderResourceState.TransferWrite, transition.ToState);
            },
            transition =>
            {
                Assert.Equal(20u, transition.BeforePassNodeId);
                Assert.Equal(RenderResourceState.TransferWrite, transition.FromState);
                Assert.Equal(RenderResourceState.ShaderRead, transition.ToState);
            },
            transition =>
            {
                Assert.Equal(30u, transition.BeforePassNodeId);
                Assert.Equal(RenderResourceState.ShaderRead, transition.FromState);
                Assert.Equal(RenderResourceState.TransferWrite, transition.ToState);
            });
    }

    [Fact]
    public void BuildTransitionPlan_UsesImportedInitialStateForFrameColor()
    {
        var frameColor = ImportedTexture(
            "FrameColor",
            0,
            RenderResourceState.OutputOwnership);
        var accesses = new[]
        {
            Write(frameColor, 1, RenderResourceState.ColorAttachment),
            Write(frameColor, 2, RenderResourceState.OutputOwnership)
        };

        var transitions = RenderGraphResourcePlanner.BuildTransitionPlan(
            new[] { frameColor },
            new uint[] { 1, 2 },
            accesses,
            PassName);

        Assert.Collection(
            transitions,
            transition =>
            {
                Assert.Equal(RenderResourceState.OutputOwnership, transition.FromState);
                Assert.Equal(RenderResourceState.ColorAttachment, transition.ToState);
                Assert.Equal(1u, transition.BeforePassNodeId);
            },
            transition =>
            {
                Assert.Equal(RenderResourceState.ColorAttachment, transition.FromState);
                Assert.Equal(RenderResourceState.OutputOwnership, transition.ToState);
                Assert.Equal(2u, transition.BeforePassNodeId);
            });
    }

    [Fact]
    public void BuildTransitionPlan_UsesKnownTransientInitialStateForFirstWrite()
    {
        var sceneColor = TransientTexture(
            "SceneColor",
            6,
            RenderResourceState.ShaderRead);
        var accesses = new[]
        {
            Write(sceneColor, 11, RenderResourceState.ColorAttachment),
            Read(sceneColor, 12, RenderResourceState.ShaderRead)
        };

        var transitions = RenderGraphResourcePlanner.BuildTransitionPlan(
            new[] { sceneColor },
            new uint[] { 11, 12 },
            accesses,
            PassName);

        Assert.Collection(
            transitions,
            transition =>
            {
                Assert.Equal(RenderResourceState.ShaderRead, transition.FromState);
                Assert.Equal(RenderResourceState.ColorAttachment, transition.ToState);
                Assert.Equal(11u, transition.BeforePassNodeId);
            },
            transition =>
            {
                Assert.Equal(RenderResourceState.ColorAttachment, transition.FromState);
                Assert.Equal(RenderResourceState.ShaderRead, transition.ToState);
                Assert.Equal(12u, transition.BeforePassNodeId);
            });
    }

    [Fact]
    public void BuildTransitionPlan_RejectsTransientReadBeforeWrite()
    {
        var resource = TransientTexture("History", 4);

        var error = Assert.Throws<InvalidOperationException>(() =>
            RenderGraphResourcePlanner.BuildTransitionPlan(
                new[] { resource },
                new uint[] { 7 },
                new[] { Read(resource, 7, RenderResourceState.ShaderRead) },
                PassName));

        Assert.Contains("reads resource", error.Message, StringComparison.Ordinal);
        Assert.Contains("before any graph pass writes it", error.Message, StringComparison.Ordinal);
        Assert.Contains("Pass7", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTransitionPlan_RejectsConflictingStatesForSamePass()
    {
        var resource = TransientTexture("Albedo", 5);

        var error = Assert.Throws<InvalidOperationException>(() =>
            RenderGraphResourcePlanner.BuildTransitionPlan(
                new[] { resource },
                new uint[] { 8 },
                new[]
                {
                    Read(resource, 8, RenderResourceState.ShaderRead),
                    Write(resource, 8, RenderResourceState.TransferWrite)
                },
                PassName));

        Assert.Contains("incompatible states", error.Message, StringComparison.Ordinal);
        Assert.Contains("Pass8", error.Message, StringComparison.Ordinal);
        Assert.Contains("Albedo", error.Message, StringComparison.Ordinal);
    }

    private static RenderResource TransientTexture(string name, uint id)
    {
        return new RenderResource(name, RenderResourceType.Texture, id);
    }

    private static RenderResource TransientTexture(
        string name,
        uint id,
        RenderResourceState initialState)
    {
        return new RenderResource(
            name,
            RenderResourceType.Texture,
            id,
            initialState: initialState);
    }

    private static RenderResource ImportedTexture(string name, uint id, RenderResourceState initialState)
    {
        return new RenderResource(
            name,
            RenderResourceType.Texture,
            id,
            isImported: true,
            initialState: initialState);
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

    private static string PassName(uint nodeId)
    {
        return $"Pass{nodeId}";
    }
}

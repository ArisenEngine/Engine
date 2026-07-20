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
            Write(
                frameColor,
                1,
                RenderResourceState.ColorAttachment,
                RenderAttachmentIntent.ClearStore),
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
            Write(
                sceneColor,
                11,
                RenderResourceState.ColorAttachment,
                RenderAttachmentIntent.ClearStore),
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
    public void BuildTransitionPlan_TracksDepthWriteThenReadOnlyAttachmentUse()
    {
        var frameDepth = TransientTexture("FrameDepth", 2);
        var accesses = new[]
        {
            Read(
                frameDepth,
                10,
                RenderResourceState.DepthAttachment,
                RenderAttachmentIntent.ClearThenLoadStore),
            Write(
                frameDepth,
                10,
                RenderResourceState.DepthAttachment,
                RenderAttachmentIntent.ClearThenLoadStore),
            Read(
                frameDepth,
                20,
                RenderResourceState.DepthReadAttachment,
                RenderAttachmentIntent.ReadOnlyLoadStore)
        };

        var transitions = RenderGraphResourcePlanner.BuildTransitionPlan(
            new[] { frameDepth },
            new uint[] { 10, 20 },
            accesses,
            PassName);

        Assert.Collection(
            transitions,
            transition =>
            {
                Assert.Equal(RenderResourceState.Unknown, transition.FromState);
                Assert.Equal(RenderResourceState.DepthAttachment, transition.ToState);
                Assert.Equal(10u, transition.BeforePassNodeId);
            },
            transition =>
            {
                Assert.Equal(RenderResourceState.DepthAttachment, transition.FromState);
                Assert.Equal(RenderResourceState.DepthReadAttachment, transition.ToState);
                Assert.Equal(20u, transition.BeforePassNodeId);
            });
    }

    [Fact]
    public void BuildTransitionPlan_TracksFrameDepthThroughTransferReadback()
    {
        var frameDepth = TransientTexture("FrameDepth", 20);
        var accesses = new[]
        {
            Read(
                frameDepth,
                10,
                RenderResourceState.DepthAttachment,
                RenderAttachmentIntent.ClearThenLoadStore),
            Write(
                frameDepth,
                10,
                RenderResourceState.DepthAttachment,
                RenderAttachmentIntent.ClearThenLoadStore),
            Read(
                frameDepth,
                20,
                RenderResourceState.DepthReadAttachment,
                RenderAttachmentIntent.ReadOnlyLoadStore),
            Read(frameDepth, 30, RenderResourceState.TransferRead)
        };

        var transitions = RenderGraphResourcePlanner.BuildTransitionPlan(
            new[] { frameDepth },
            new uint[] { 10, 20, 30 },
            accesses,
            PassName);

        Assert.Collection(
            transitions,
            transition =>
            {
                Assert.Equal(RenderResourceState.Unknown, transition.FromState);
                Assert.Equal(RenderResourceState.DepthAttachment, transition.ToState);
                Assert.Equal(10u, transition.BeforePassNodeId);
            },
            transition =>
            {
                Assert.Equal(RenderResourceState.DepthAttachment, transition.FromState);
                Assert.Equal(RenderResourceState.DepthReadAttachment, transition.ToState);
                Assert.Equal(20u, transition.BeforePassNodeId);
            },
            transition =>
            {
                Assert.Equal(RenderResourceState.DepthReadAttachment, transition.FromState);
                Assert.Equal(RenderResourceState.TransferRead, transition.ToState);
                Assert.Equal(30u, transition.BeforePassNodeId);
            });
    }

    [Fact]
    public void BuildTransitionPlan_TracksShadowDepthWriteThenShaderRead()
    {
        var shadowMap = TransientTexture("DirectionalShadowMap", 3);
        var accesses = new[]
        {
            Write(
                shadowMap,
                10,
                RenderResourceState.DepthAttachment,
                RenderAttachmentIntent.ClearStore),
            Read(shadowMap, 20, RenderResourceState.ShaderRead)
        };

        var transitions = RenderGraphResourcePlanner.BuildTransitionPlan(
            new[] { shadowMap },
            new uint[] { 10, 20 },
            accesses,
            PassName);

        Assert.Collection(
            transitions,
            transition =>
            {
                Assert.Equal(RenderResourceState.Unknown, transition.FromState);
                Assert.Equal(RenderResourceState.DepthAttachment, transition.ToState);
                Assert.Equal(10u, transition.BeforePassNodeId);
            },
            transition =>
            {
                Assert.Equal(RenderResourceState.DepthAttachment, transition.FromState);
                Assert.Equal(RenderResourceState.ShaderRead, transition.ToState);
                Assert.Equal(20u, transition.BeforePassNodeId);
            });
    }

    [Fact]
    public void BuildTransitionPlan_IgnoresResourceAccessesFromInactivePasses()
    {
        var frameDepth = TransientTexture("FrameDepth", 2);
        var accesses = new[]
        {
            Write(
                frameDepth,
                10,
                RenderResourceState.DepthAttachment,
                RenderAttachmentIntent.ClearStore),
            Read(
                frameDepth,
                20,
                RenderResourceState.DepthReadAttachment,
                RenderAttachmentIntent.ReadOnlyLoadStore)
        };

        var transitions = RenderGraphResourcePlanner.BuildTransitionPlan(
            new[] { frameDepth },
            new uint[] { 10 },
            accesses,
            PassName);

        var transition = Assert.Single(transitions);
        Assert.Equal(RenderResourceState.Unknown, transition.FromState);
        Assert.Equal(RenderResourceState.DepthAttachment, transition.ToState);
        Assert.Equal(10u, transition.BeforePassNodeId);
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

    [Fact]
    public void BuildTransitionPlan_AcceptsClearThenLoadForReadWriteAttachment()
    {
        var frameDepth = TransientTexture("FrameDepth", 9);
        var accesses = new[]
        {
            Read(
                frameDepth,
                12,
                RenderResourceState.DepthAttachment,
                RenderAttachmentIntent.ClearThenLoadStore),
            Write(
                frameDepth,
                12,
                RenderResourceState.DepthAttachment,
                RenderAttachmentIntent.ClearThenLoadStore)
        };

        var transitions = RenderGraphResourcePlanner.BuildTransitionPlan(
            new[] { frameDepth },
            new uint[] { 12 },
            accesses,
            PassName);

        var transition = Assert.Single(transitions);
        Assert.Equal(RenderResourceState.Unknown, transition.FromState);
        Assert.Equal(RenderResourceState.DepthAttachment, transition.ToState);
    }

    [Fact]
    public void BuildTransitionPlan_RejectsLoadOfUninitializedTransientAttachment()
    {
        var sceneColor = TransientTexture("SceneColor", 10);
        var accesses = new[]
        {
            Read(
                sceneColor,
                13,
                RenderResourceState.ColorAttachment,
                RenderAttachmentIntent.LoadStore),
            Write(
                sceneColor,
                13,
                RenderResourceState.ColorAttachment,
                RenderAttachmentIntent.LoadStore)
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            RenderGraphResourcePlanner.BuildTransitionPlan(
                new[] { sceneColor },
                new uint[] { 13 },
                accesses,
                PassName));

        Assert.Contains("loads resource", error.Message, StringComparison.Ordinal);
        Assert.Contains("before any graph pass stores it", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTransitionPlan_RejectsClearWithoutWriteAccess()
    {
        var frameDepth = TransientTexture("FrameDepth", 11);

        var error = Assert.Throws<InvalidOperationException>(() =>
            RenderGraphResourcePlanner.BuildTransitionPlan(
                new[] { frameDepth },
                new uint[] { 14 },
                new[]
                {
                    Read(
                        frameDepth,
                        14,
                        RenderResourceState.DepthAttachment,
                        RenderAttachmentIntent.ClearStore)
                },
                PassName));

        Assert.Contains("clears attachment", error.Message, StringComparison.Ordinal);
        Assert.Contains("without write access", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTransitionPlan_RejectsWriteThroughReadOnlyDepthAttachment()
    {
        var frameDepth = ImportedTexture(
            "FrameDepth",
            12,
            RenderResourceState.DepthAttachment);
        var accesses = new[]
        {
            Read(
                frameDepth,
                15,
                RenderResourceState.DepthReadAttachment,
                RenderAttachmentIntent.ReadOnlyLoadStore),
            Write(
                frameDepth,
                15,
                RenderResourceState.DepthReadAttachment,
                RenderAttachmentIntent.ReadOnlyLoadStore)
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            RenderGraphResourcePlanner.BuildTransitionPlan(
                new[] { frameDepth },
                new uint[] { 15 },
                accesses,
                PassName));

        Assert.Contains("write access for read-only depth attachment", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTransitionPlan_RejectsLoadAfterDiscardedStore()
    {
        var sceneColor = TransientTexture("SceneColor", 13);
        var clearDiscard = new RenderAttachmentIntent(
            RenderAttachmentLoadIntent.Clear,
            RenderAttachmentStoreIntent.Discard);
        var accesses = new[]
        {
            Write(sceneColor, 16, RenderResourceState.ColorAttachment, clearDiscard),
            Read(
                sceneColor,
                17,
                RenderResourceState.ColorAttachment,
                RenderAttachmentIntent.LoadStore),
            Write(
                sceneColor,
                17,
                RenderResourceState.ColorAttachment,
                RenderAttachmentIntent.LoadStore)
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            RenderGraphResourcePlanner.BuildTransitionPlan(
                new[] { sceneColor },
                new uint[] { 16, 17 },
                accesses,
                PassName));

        Assert.Contains("Pass17", error.Message, StringComparison.Ordinal);
        Assert.Contains("before any graph pass stores it", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTransitionPlan_RejectsLoadAfterReadOnlyAttachmentDiscardsContent()
    {
        var frameDepth = ImportedTexture(
            "FrameDepth",
            16,
            RenderResourceState.DepthAttachment);
        var readOnlyDiscard = new RenderAttachmentIntent(
            RenderAttachmentLoadIntent.ReadOnlyLoad,
            RenderAttachmentStoreIntent.Discard);
        var accesses = new[]
        {
            Read(
                frameDepth,
                20,
                RenderResourceState.DepthReadAttachment,
                readOnlyDiscard),
            Read(
                frameDepth,
                21,
                RenderResourceState.DepthReadAttachment,
                RenderAttachmentIntent.ReadOnlyLoadStore)
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            RenderGraphResourcePlanner.BuildTransitionPlan(
                new[] { frameDepth },
                new uint[] { 20, 21 },
                accesses,
                PassName));

        Assert.Contains("Pass21", error.Message, StringComparison.Ordinal);
        Assert.Contains("before any graph pass stores it", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTransitionPlan_RejectsMismatchedAttachmentIntentsWithinPass()
    {
        var sceneColor = ImportedTexture(
            "SceneColor",
            17,
            RenderResourceState.ColorAttachment);
        var accesses = new[]
        {
            Read(
                sceneColor,
                18,
                RenderResourceState.ColorAttachment,
                RenderAttachmentIntent.LoadStore),
            Write(
                sceneColor,
                18,
                RenderResourceState.ColorAttachment,
                RenderAttachmentIntent.ClearThenLoadStore)
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            RenderGraphResourcePlanner.BuildTransitionPlan(
                new[] { sceneColor },
                new uint[] { 18 },
                accesses,
                PassName));

        Assert.Contains("incompatible attachment intents", error.Message, StringComparison.Ordinal);
        Assert.Contains("Load/Store", error.Message, StringComparison.Ordinal);
        Assert.Contains("ClearThenLoad/Store", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTransitionPlan_RejectsIncompleteAttachmentIntent()
    {
        var sceneColor = TransientTexture("SceneColor", 18);
        var incompleteIntent = new RenderAttachmentIntent(
            RenderAttachmentLoadIntent.Clear,
            RenderAttachmentStoreIntent.None);

        var error = Assert.Throws<InvalidOperationException>(() =>
            RenderGraphResourcePlanner.BuildTransitionPlan(
                new[] { sceneColor },
                new uint[] { 19 },
                new[]
                {
                    Write(
                        sceneColor,
                        19,
                        RenderResourceState.ColorAttachment,
                        incompleteIntent)
                },
                PassName));

        Assert.Contains("must declare both load and store intent", error.Message, StringComparison.Ordinal);
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
        RenderResourceState state,
        RenderAttachmentIntent attachmentIntent = default)
    {
        return new RenderGraphResourceAccess(
            resource.ResourceId,
            passNodeId,
            RenderGraphResourceAccessKind.Read,
            state,
            attachmentIntent);
    }

    private static RenderGraphResourceAccess Write(
        RenderResource resource,
        uint passNodeId,
        RenderResourceState state,
        RenderAttachmentIntent attachmentIntent = default)
    {
        return new RenderGraphResourceAccess(
            resource.ResourceId,
            passNodeId,
            RenderGraphResourceAccessKind.Write,
            state,
            attachmentIntent);
    }

    private static string PassName(uint nodeId)
    {
        return $"Pass{nodeId}";
    }
}

using ArisenEngine.Core.RHI;
using ArisenEngine.Rendering;
using ArisenEngine.Rendering.Resources;
using System.Numerics;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class DirectionalShadowFitterTests
{
    [Fact]
    public void CreateFallsBackWhenReceiverBoundsAreUnavailable()
    {
        var result = DirectionalShadowFitter.Create(
            DirectionalLight.Default.Direction,
            MeshBounds.Empty,
            2048);

        Assert.False(result.IsSceneFitted);
        Assert.Equal(10.0f, result.Diameter);
        Assert.True(result.WorldUnitsPerTexel > 0.0f);
        AssertFinite(result.ViewProjection);
    }

    [Fact]
    public void CreateFitsEveryReceiverBoundsCornerInsideShadowClipSpace()
    {
        var bounds = new MeshBounds(
            new Vector3(-2.0f, -1.0f, -3.0f),
            new Vector3(4.0f, 5.0f, 2.0f));

        var result = DirectionalShadowFitter.Create(
            Vector3.Normalize(new Vector3(0.35f, 0.8f, -0.2f)),
            bounds,
            2048);

        Assert.True(result.IsSceneFitted);
        Assert.True(result.Diameter > 0.0f);
        Assert.True(result.Depth > 0.0f);
        for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
        {
            var corner = new Vector3(
                (cornerIndex & 1) == 0 ? bounds.Min.X : bounds.Max.X,
                (cornerIndex & 2) == 0 ? bounds.Min.Y : bounds.Max.Y,
                (cornerIndex & 4) == 0 ? bounds.Min.Z : bounds.Max.Z);
            var clip = Vector4.Transform(new Vector4(corner, 1.0f), result.ViewProjection);
            var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
            Assert.InRange(ndc.X, -1.001f, 1.001f);
            Assert.InRange(ndc.Y, -1.001f, 1.001f);
            Assert.InRange(ndc.Z, -0.001f, 1.001f);
        }
    }

    [Fact]
    public void CreateKeepsProjectionStableForSubTexelReceiverMotion()
    {
        var bounds = new MeshBounds(
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, 1.0f));
        var initial = DirectionalShadowFitter.Create(Vector3.UnitY, bounds, 1024);
        float subTexelShift = initial.WorldUnitsPerTexel * 0.25f;
        var shiftedBounds = new MeshBounds(
            bounds.Min + new Vector3(subTexelShift, 0.0f, 0.0f),
            bounds.Max + new Vector3(subTexelShift, 0.0f, 0.0f));

        var shifted = DirectionalShadowFitter.Create(Vector3.UnitY, shiftedBounds, 1024);

        Assert.True(initial.IsSceneFitted);
        Assert.Equal(initial.SnappedLightSpaceCenter, shifted.SnappedLightSpaceCenter);
        Assert.Equal(initial.ViewProjection, shifted.ViewProjection);
    }

    [Fact]
    public void FittedProjectionCullsCasterOutsideReceiverShadowVolume()
    {
        var receiverBounds = new MeshBounds(
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, 1.0f));
        var projection = DirectionalShadowFitter.Create(
            Vector3.Normalize(new Vector3(0.4f, 1.0f, -0.25f)),
            receiverBounds,
            1024);
        var localBounds = new MeshBounds(
            new Vector3(-0.5f),
            new Vector3(0.5f));
        var caster = new StaticMeshRenderItem
        {
            MeshGuid = Guid.NewGuid(),
            LocalToWorld = Matrix4x4.Identity,
            Visible = 1
        };

        Assert.True(StaticMeshFrustumCuller.IsVisible(
            caster,
            localBounds,
            projection.ViewProjection));

        caster.LocalToWorld = Matrix4x4.CreateTranslation(50.0f, 0.0f, 0.0f);

        Assert.False(StaticMeshFrustumCuller.IsVisible(
            caster,
            localBounds,
            projection.ViewProjection));
    }

    [Fact]
    public void BoundsAccumulatorUnionsBoundedVisibleItems()
    {
        var accumulator = new DirectionalShadowBoundsAccumulator();

        Assert.True(accumulator.Add(new MeshBounds(
            new Vector3(-2.0f, -1.0f, 0.0f),
            new Vector3(0.0f, 3.0f, 2.0f))));
        Assert.True(accumulator.Add(new MeshBounds(
            new Vector3(-1.0f, -4.0f, -2.0f),
            new Vector3(5.0f, 1.0f, 1.0f))));

        Assert.True(accumulator.IsValid);
        Assert.Equal(2, accumulator.Count);
        Assert.Equal(new Vector3(-2.0f, -4.0f, -2.0f), accumulator.Bounds.Min);
        Assert.Equal(new Vector3(5.0f, 3.0f, 2.0f), accumulator.Bounds.Max);
    }

    private static void AssertFinite(Matrix4x4 matrix)
    {
        Assert.All(
            new[]
            {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            },
            value => Assert.True(float.IsFinite(value)));
    }
}

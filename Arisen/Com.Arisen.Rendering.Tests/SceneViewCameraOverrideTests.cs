using System.Numerics;
using ArisenEngine.Rendering;
using ArisenEngine.Resources.Serialization;
using ArisenKernel.Contracts;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class SceneViewCameraOverrideTests
{
    [Fact]
    public void Resolve_AppliesOnlyToSceneViewAndTracksRenderOrigin()
    {
        var source = new SceneViewCameraOverride(
            new WorldPosition(1_000_010.0, 42.0, -2_000_030.0),
            new Vector3(20.0f, 35.0f, 0.0f),
            50.0f,
            0.1f,
            4000.0f);

        Assert.False(SurfaceCameraOverrideResolver.TryResolve(
            SurfaceType.GameView,
            source,
            new WorldPosition(1_000_000.0, 0.0, -2_000_000.0),
            16.0f / 9.0f,
            out _));

        Assert.True(SurfaceCameraOverrideResolver.TryResolve(
            SurfaceType.SceneView,
            source,
            new WorldPosition(1_000_000.0, 0.0, -2_000_000.0),
            16.0f / 9.0f,
            out Camera first));
        Assert.Equal(new Vector3(10.0f, 42.0f, -30.0f), first.Position);
        Assert.Equal(source.Rotation, first.Rotation);
        Assert.Equal(16.0f / 9.0f, first.AspectRatio);

        var rebasedOrigin = new WorldPosition(1_000_008.0, 32.0, -2_000_024.0);
        Assert.True(SurfaceCameraOverrideResolver.TryResolve(
            SurfaceType.SceneView,
            source,
            rebasedOrigin,
            1.5f,
            out Camera rebased));
        Assert.Equal(new Vector3(2.0f, 10.0f, -6.0f), rebased.Position);
        Assert.Equal(source.Position.X, rebasedOrigin.X + rebased.Position.X, 6);
        Assert.Equal(source.Position.Y, rebasedOrigin.Y + rebased.Position.Y, 6);
        Assert.Equal(source.Position.Z, rebasedOrigin.Z + rebased.Position.Z, 6);
    }

    [Fact]
    public void Resolve_RejectsInvalidCameraData()
    {
        var invalid = new SceneViewCameraOverride(
            new WorldPosition(double.NaN, 0.0, 0.0),
            Vector3.Zero,
            50.0f,
            0.1f,
            1000.0f);

        Assert.False(SurfaceCameraOverrideResolver.TryResolve(
            SurfaceType.SceneView,
            invalid,
            default,
            1.0f,
            out _));
    }
}

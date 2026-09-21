using ArisenEngine.Rendering;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

/// <summary>
/// The vegetation passes render in the view frame: every vertex position they consume is relative to
/// the render camera and the frame view-projection is the rotation-only
/// <see cref="Camera.ViewRelativeViewMatrix"/> times the projection. These tests pin the two
/// properties that make the pass bit-stable across a world-origin rebase.
/// </summary>
public sealed class VegetationViewFrameTests
{
    private static Camera CreateCamera(Vector3 position, Vector3 rotation) => new()
    {
        ProjectionType = CameraProjectionType.Perspective,
        FieldOfView = 60.0f,
        NearClip = 0.1f,
        FarClip = 2000.0f,
        AspectRatio = 16.0f / 9.0f,
        Position = position,
        Rotation = rotation
    };

    [Fact]
    public void ViewRelativeViewMatrixIsTheRotationOnlyBasisOfTheSameCamera()
    {
        Camera camera = CreateCamera(
            new Vector3(-2.0f, 2.0f, -6.0f),
            new Vector3(12.0f, -35.0f, 4.0f));
        Camera originCamera = camera;
        originCamera.Position = Vector3.Zero;

        Assert.Equal(originCamera.ViewMatrix, camera.ViewRelativeViewMatrix);
    }

    [Fact]
    public void ViewRelativeViewMatrixIgnoresTheCameraPosition()
    {
        Camera camera = CreateCamera(
            new Vector3(1536.0f, 12.0f, -2048.0f),
            new Vector3(0.0f, 90.0f, 0.0f));
        Camera translated = camera;
        translated.Position += new Vector3(4096.0f, 0.0f, 8192.0f);

        Assert.Equal(camera.ViewRelativeViewMatrix, translated.ViewRelativeViewMatrix);
    }

    [Theory]
    [InlineData(1536.0)]
    [InlineData(-1536.0)]
    [InlineData(65536.0)]
    [InlineData(0.0)]
    public void ToViewRelativePositionIsInvariantWhenTheCameraAndThePositionShiftTogether(double rebaseDelta)
    {
        WorldPosition cameraWorld = new(-2.0, 2.0, -6.0);
        WorldPosition clusterWorld = new(1534.25, 2.5, -6.75);
        WorldPosition rebasedCameraWorld = new(
            cameraWorld.X + rebaseDelta,
            cameraWorld.Y,
            cameraWorld.Z + rebaseDelta);
        WorldPosition rebasedClusterWorld = new(
            clusterWorld.X + rebaseDelta,
            clusterWorld.Y,
            clusterWorld.Z + rebaseDelta);

        Vector3 baseline = VegetationViewFrame.ToViewRelativePosition(clusterWorld, cameraWorld);
        Vector3 rebased = VegetationViewFrame.ToViewRelativePosition(
            rebasedClusterWorld,
            rebasedCameraWorld);

        Assert.Equal(baseline, rebased);
    }

    [Fact]
    public void ToViewRelativePositionSubtractsTheCameraInDoublePrecision()
    {
        WorldPosition cameraWorld = new(1000000.25, -2000000.5, 3000000.75);
        WorldPosition clusterWorld = new(1000000.75, -2000000.25, 3000000.25);

        Vector3 result = VegetationViewFrame.ToViewRelativePosition(clusterWorld, cameraWorld);

        Assert.Equal(0.5f, result.X);
        Assert.Equal(0.25f, result.Y);
        Assert.Equal(-0.5f, result.Z);
    }

    [Fact]
    public void ToViewRelativePositionRejectsPositionsOutsideTheFloatRange()
    {
        WorldPosition cameraWorld = new(0.0, 0.0, 0.0);
        WorldPosition tooFar = new((double)float.MaxValue * 4.0, 0.0, 0.0);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => VegetationViewFrame.ToViewRelativePosition(tooFar, cameraWorld));
        Assert.Contains("view-frame float range", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToViewRelativePositionRejectsNonFinitePositions()
    {
        WorldPosition cameraWorld = new(0.0, 0.0, 0.0);

        Assert.Throws<InvalidOperationException>(
            () => VegetationViewFrame.ToViewRelativePosition(
                new WorldPosition(double.NaN, 0.0, 0.0),
                cameraWorld));
    }
}
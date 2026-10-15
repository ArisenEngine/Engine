using System.Numerics;
using ArisenEngine.App;
using ArisenEngine.Core.Math;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeViewTargetCameraTests
{
    [Fact]
    public void DraggingRightTurnsRightByDecreasingYaw()
    {
        (float yaw, float pitch) = RuntimeViewTargetCamera.ApplyLook(
            yawDegrees: 20.0f,
            pitchDegrees: 0.0f,
            deltaX: 100.0f,
            deltaY: 0.0f);

        Assert.Equal(
            20.0f - 100.0f * RuntimeViewTargetCamera.LookDegreesPerPixel,
            yaw,
            3);
        Assert.Equal(0.0f, pitch, 3);
    }

    [Fact]
    public void DraggingDownLooksDownByIncreasingPitchWithinItsClamp()
    {
        (_, float pitch) = RuntimeViewTargetCamera.ApplyLook(0.0f, 0.0f, 0.0f, 100.0f);
        Assert.Equal(100.0f * RuntimeViewTargetCamera.LookDegreesPerPixel, pitch, 3);

        (_, float clampedUp) = RuntimeViewTargetCamera.ApplyLook(0.0f, 0.0f, 0.0f, -100_000.0f);
        Assert.Equal(-RuntimeViewTargetCamera.MaxPitchDegrees, clampedUp, 3);

        (_, float clampedDown) = RuntimeViewTargetCamera.ApplyLook(0.0f, 0.0f, 0.0f, 100_000.0f);
        Assert.Equal(RuntimeViewTargetCamera.MaxPitchDegrees, clampedDown, 3);
    }

    [Fact]
    public void LongSessionsKeepYawWithinItsWrappedRange()
    {
        float yaw = 0.0f;
        for (int step = 0; step < 10_000; step++)
        {
            (yaw, _) = RuntimeViewTargetCamera.ApplyLook(yaw, 0.0f, 37.0f, 0.0f);
        }

        Assert.InRange(yaw, -180.0f, 180.0f);
        Assert.Equal(MathExtensions.WrapDegrees(yaw), yaw, 3);
    }

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(90.0f, 0.0f)]
    [InlineData(-135.0f, 25.0f)]
    [InlineData(42.0f, -60.0f)]
    public void FollowPoseKeepsTheDistanceAndWatchesTheTarget(float yaw, float pitch)
    {
        var target = new Vector3(3.0f, 1.0f, -7.0f);
        const float distance = RuntimeViewTargetCamera.DefaultDistance;
        const float height = RuntimeViewTargetCamera.DefaultHeight;

        RuntimeViewTargetCamera.ComputeFollowPose(
            target,
            yaw,
            pitch,
            distance,
            height,
            out Vector3 position,
            out Quaternion rotation);

        Vector3 focus = target + new Vector3(0.0f, height, 0.0f);
        Assert.Equal(distance, Vector3.Distance(position, focus), 3);

        Vector3 forward = Vector3.Transform(MathExtensions.Forward, rotation);
        Vector3 expected = Vector3.Normalize(focus - position);
        Assert.Equal(1.0f, Vector3.Dot(forward, expected), 3);
    }

    [Fact]
    public void UnrotatedFollowCameraSitsBehindTheTargetOnItsViewAxis()
    {
        var target = new Vector3(0.0f, 0.0f, 0.0f);

        RuntimeViewTargetCamera.ComputeFollowPose(
            target,
            0.0f,
            0.0f,
            RuntimeViewTargetCamera.DefaultDistance,
            RuntimeViewTargetCamera.DefaultHeight,
            out Vector3 position,
            out Quaternion rotation);

        Assert.Equal(0.0f, position.X, 3);
        Assert.Equal(RuntimeViewTargetCamera.DefaultHeight, position.Y, 3);
        Assert.Equal(-RuntimeViewTargetCamera.DefaultDistance, position.Z, 3);

        Vector3 forward = Vector3.Transform(MathExtensions.Forward, rotation);
        Assert.Equal(1.0f, Vector3.Dot(forward, new Vector3(0.0f, 0.0f, 1.0f)), 3);
    }

    [Fact]
    public void PitchingDownRaisesTheCameraBehindTheTarget()
    {
        var target = new Vector3(0.0f, 0.0f, 0.0f);

        RuntimeViewTargetCamera.ComputeFollowPose(target, 0.0f, 0.0f, 6.0f, 1.6f, out Vector3 level, out _);
        RuntimeViewTargetCamera.ComputeFollowPose(target, 0.0f, 30.0f, 6.0f, 1.6f, out Vector3 tilted, out _);

        Assert.True(tilted.Y > level.Y);
    }
}

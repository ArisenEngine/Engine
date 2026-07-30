using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class DepthReconstructionTests
{
    [Theory]
    [InlineData(DeviceDepthConvention.ForwardZeroToOne, 0.0f, 0.1f)]
    [InlineData(DeviceDepthConvention.ForwardZeroToOne, 1.0f, 1000.0f)]
    [InlineData(DeviceDepthConvention.ReversedZeroToOne, 1.0f, 0.1f)]
    [InlineData(DeviceDepthConvention.ReversedZeroToOne, 0.0f, 1000.0f)]
    public void PerspectiveEndpointsMatchClipPlanes(
        DeviceDepthConvention convention,
        float deviceDepth,
        float expected)
    {
        float actual = DepthReconstruction.LinearizeZeroToOne(
            deviceDepth,
            0.1f,
            1000.0f,
            CameraProjectionType.Perspective,
            convention);

        float tolerance = MathF.Max(0.001f, expected * 0.001f);
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }

    [Theory]
    [InlineData(DeviceDepthConvention.ForwardZeroToOne, 0.0f, 2.0f)]
    [InlineData(DeviceDepthConvention.ForwardZeroToOne, 1.0f, 42.0f)]
    [InlineData(DeviceDepthConvention.ReversedZeroToOne, 1.0f, 2.0f)]
    [InlineData(DeviceDepthConvention.ReversedZeroToOne, 0.0f, 42.0f)]
    public void OrthographicEndpointsMatchClipPlanes(
        DeviceDepthConvention convention,
        float deviceDepth,
        float expected)
    {
        float actual = DepthReconstruction.LinearizeZeroToOne(
            deviceDepth,
            2.0f,
            42.0f,
            CameraProjectionType.Orthographic,
            convention);

        Assert.Equal(expected, actual, precision: 5);
    }

    [Fact]
    public void ForwardAndReversedDepthRemainMonotonicAndEquivalent()
    {
        float previous = 0.1f;
        for (int index = 1; index <= 100; index++)
        {
            float forwardDepth = index / 100.0f;
            float reversedDepth = 1.0f - forwardDepth;
            float forward = DepthReconstruction.LinearizeZeroToOne(
                forwardDepth,
                0.1f,
                500.0f,
                CameraProjectionType.Perspective,
                DeviceDepthConvention.ForwardZeroToOne);
            float reversed = DepthReconstruction.LinearizeZeroToOne(
                reversedDepth,
                0.1f,
                500.0f,
                CameraProjectionType.Perspective,
                DeviceDepthConvention.ReversedZeroToOne);

            Assert.True(forward >= previous);
            Assert.Equal(forward, reversed, precision: 3);
            previous = forward;
        }
    }

    [Theory]
    [InlineData(DeviceDepthConvention.ForwardZeroToOne, 1.0f, true)]
    [InlineData(DeviceDepthConvention.ForwardZeroToOne, 0.999f, false)]
    [InlineData(DeviceDepthConvention.ReversedZeroToOne, 0.0f, true)]
    [InlineData(DeviceDepthConvention.ReversedZeroToOne, 0.001f, false)]
    public void ClearDepthDetectionFollowsExplicitConvention(
        DeviceDepthConvention convention,
        float deviceDepth,
        bool expected)
    {
        Assert.Equal(
            expected,
            DepthReconstruction.IsClearDepth(deviceDepth, convention));
    }
}

using System.Buffers.Binary;
using Arisen.Native.RHI;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderOutputImageSummaryTests
{
    [Fact]
    public void Build_DecodesBgraChannelsAndProducesBoundedStatistics()
    {
        byte[] pixels =
        {
            0, 0, 255, 255,
            255, 0, 0, 255
        };

        var artifact = RenderOutputImageSummaryBuilder.Build(
            pixels,
            2,
            1,
            EFormat.FORMAT_B8G8R8A8_UNORM,
            CreateDepthPixels(0.25f, 1.0f),
            EFormat.FORMAT_D32_SFLOAT,
            "Development",
            RenderOutputKind.NativeSwapchain,
            7,
            1);

        Assert.True(artifact.Passed);
        Assert.Equal("BGRA", artifact.ChannelOrder);
        Assert.Equal("linear", artifact.ColorSpace);
        Assert.Equal(2, artifact.PixelCount);
        Assert.Equal(2, artifact.FinitePixelCount);
        Assert.Equal(2, artifact.NonBlankPixelCount);
        Assert.Equal(2, artifact.OpaquePixelCount);
        Assert.Equal(1.0, artifact.MaximumRgb[0], 6);
        Assert.Equal(1.0, artifact.MaximumRgb[2], 6);
        Assert.Equal(0.5, artifact.AverageRgb[0], 6);
        Assert.Equal(0.5, artifact.AverageRgb[2], 6);
        Assert.Equal(artifact.PixelCount, artifact.LuminanceHistogram.Sum());
        Assert.Equal(16, artifact.LuminanceHistogram.Length);
        Assert.Equal(16, artifact.SpatialLuminanceGrid.Length);
        Assert.Equal(2, artifact.SchemaVersion);
        Assert.True(artifact.Depth.Passed);
    }

    [Fact]
    public void Build_LinearizesSrgbColorChannels()
    {
        byte[] pixels =
        {
            128, 128, 128, 255,
            255, 255, 255, 255
        };

        var artifact = RenderOutputImageSummaryBuilder.Build(
            pixels,
            2,
            1,
            EFormat.FORMAT_R8G8B8A8_SRGB,
            CreateDepthPixels(0.25f, 1.0f),
            EFormat.FORMAT_D32_SFLOAT,
            "Production",
            RenderOutputKind.NativeSwapchain,
            9,
            1);

        Assert.True(artifact.Passed);
        Assert.Equal("linearized-sRGB", artifact.ColorSpace);
        Assert.InRange(artifact.MinimumRgb[0], 0.215, 0.217);
        Assert.Equal(1.0, artifact.MaximumRgb[0], 6);
    }

    [Fact]
    public void Build_RejectsBlankOutputThroughChecksWithoutThrowing()
    {
        var pixels = new byte[4 * 4 * 4];
        for (int pixel = 0; pixel < 16; pixel++)
        {
            pixels[pixel * 4 + 3] = 255;
        }

        var artifact = RenderOutputImageSummaryBuilder.Build(
            pixels,
            4,
            4,
            EFormat.FORMAT_R8G8B8A8_UNORM,
            CreatePassingDepthPixels(16),
            EFormat.FORMAT_D32_SFLOAT,
            "Development",
            RenderOutputKind.NativeSwapchain,
            1,
            1);

        Assert.False(artifact.Passed);
        Assert.False(artifact.Checks.HasNonBlankCoverage);
        Assert.False(artifact.Checks.HasLuminanceVariation);
        Assert.True(artifact.Checks.AllPixelsFinite);
    }

    [Fact]
    public void Build_RejectsFlatNonBlackOutput()
    {
        var pixels = Enumerable.Repeat((byte)96, 4 * 4 * 4).ToArray();
        for (int pixel = 0; pixel < 16; pixel++)
        {
            pixels[pixel * 4 + 3] = 255;
        }

        var artifact = RenderOutputImageSummaryBuilder.Build(
            pixels,
            4,
            4,
            EFormat.FORMAT_R8G8B8A8_UNORM,
            CreatePassingDepthPixels(16),
            EFormat.FORMAT_D32_SFLOAT,
            "Development",
            RenderOutputKind.NativeSwapchain,
            1,
            1);

        Assert.False(artifact.Passed);
        Assert.True(artifact.Checks.HasNonBlankCoverage);
        Assert.False(artifact.Checks.HasLuminanceVariation);
    }

    [Fact]
    public void GetRequiredByteCount_RejectsUnsupportedOutputFormat()
    {
        Assert.Throws<NotSupportedException>(() =>
            RenderOutputImageSummaryBuilder.GetRequiredByteCount(
                16,
                16,
                EFormat.FORMAT_R16G16B16A16_SFLOAT));
    }

    [Fact]
    public void DepthBuild_DecodesD32AndProducesBoundedStatistics()
    {
        var artifact = RenderDepthImageSummaryBuilder.Build(
            CreateDepthPixels(0.2f, 0.5f, 1.0f, 1.0f),
            2,
            2,
            EFormat.FORMAT_D32_SFLOAT);

        Assert.True(artifact.Passed);
        Assert.Equal(16, artifact.ByteCount);
        Assert.Equal(4, artifact.PixelCount);
        Assert.Equal(4, artifact.FiniteDepthPixelCount);
        Assert.Equal(4, artifact.NormalizedDepthPixelCount);
        Assert.Equal(2, artifact.ClearDepthPixelCount);
        Assert.Equal(2, artifact.WrittenDepthPixelCount);
        Assert.Equal(0.2, artifact.MinimumDepth, 5);
        Assert.Equal(1.0, artifact.MaximumDepth, 6);
        Assert.Equal(artifact.NormalizedDepthPixelCount, artifact.DepthHistogram.Sum());
        Assert.Equal(16, artifact.DepthHistogram.Length);
        Assert.Equal(16, artifact.SpatialDepthGrid.Length);
    }

    [Fact]
    public void DepthBuild_RejectsAllClearDepthThroughChecksWithoutThrowing()
    {
        var artifact = RenderDepthImageSummaryBuilder.Build(
            CreateDepthPixels(Enumerable.Repeat(1.0f, 16).ToArray()),
            4,
            4,
            EFormat.FORMAT_D32_SFLOAT);

        Assert.False(artifact.Passed);
        Assert.True(artifact.Checks.AllDepthValuesFinite);
        Assert.True(artifact.Checks.AllDepthValuesNormalized);
        Assert.False(artifact.Checks.HasWrittenDepthCoverage);
        Assert.False(artifact.Checks.HasDepthVariation);
    }

    [Fact]
    public void DepthBuild_RejectsNonFiniteAndOutOfRangeValues()
    {
        var artifact = RenderDepthImageSummaryBuilder.Build(
            CreateDepthPixels(float.NaN, -0.1f, 1.1f, 0.5f),
            2,
            2,
            EFormat.FORMAT_D32_SFLOAT);

        Assert.False(artifact.Passed);
        Assert.False(artifact.Checks.AllDepthValuesFinite);
        Assert.False(artifact.Checks.AllDepthValuesNormalized);
        Assert.Equal(3, artifact.FiniteDepthPixelCount);
        Assert.Equal(1, artifact.NormalizedDepthPixelCount);
    }

    [Fact]
    public void DepthGetRequiredByteCount_UsesFourBytesAndRejectsUnsupportedFormat()
    {
        Assert.Equal(
            16L * 8 * sizeof(float),
            RenderDepthImageSummaryBuilder.GetRequiredByteCount(
                16,
                8,
                EFormat.FORMAT_D32_SFLOAT));
        Assert.Throws<NotSupportedException>(() =>
            RenderDepthImageSummaryBuilder.GetRequiredByteCount(
                16,
                8,
                EFormat.FORMAT_D24_UNORM_S8_UINT));
    }

    private static byte[] CreatePassingDepthPixels(int pixelCount)
    {
        var values = Enumerable.Repeat(1.0f, pixelCount).ToArray();
        values[0] = 0.25f;
        return CreateDepthPixels(values);
    }

    private static byte[] CreateDepthPixels(params float[] values)
    {
        var bytes = new byte[checked(values.Length * sizeof(float))];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(
                bytes.AsSpan(i * sizeof(float), sizeof(float)),
                values[i]);
        }

        return bytes;
    }
}

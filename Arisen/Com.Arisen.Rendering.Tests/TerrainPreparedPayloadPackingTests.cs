using System.Numerics;
using System.Runtime.InteropServices;
using ArisenEngine.Terrain.Assets;
using ArisenEngine.Terrain.GenericRenderPipeline;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainPreparedPayloadPackingTests
{
    [Fact]
    public void HeightAndWeightPackingPreservesCanonicalSampleOrder()
    {
        Assert.Equal(
            [0u, 1u, ushort.MaxValue],
            TerrainPreparedPayloadPacking.ExpandHeights([0, 1, ushort.MaxValue]));
        Assert.Equal(
            [0x04030201u, 0xff00807fu],
            TerrainPreparedPayloadPacking.PackWeights(
                [1, 2, 3, 4, 0x7f, 0x80, 0x00, 0xff]));
    }

    [Fact]
    public void GeometricErrorsUseStableSixteenByteGpuRecords()
    {
        TerrainGpuGeometricError[] packed =
            TerrainPreparedPayloadPacking.PackGeometricErrors(
            [
                new TerrainGeometricErrorLevel(0, 1, 0.0),
                new TerrainGeometricErrorLevel(1, 2, 3.5)
            ]);

        Assert.Equal(TerrainGpuGeometricError.Stride, Marshal.SizeOf<TerrainGpuGeometricError>());
        Assert.Equal(0u, packed[0].Level);
        Assert.Equal(1u, packed[0].SampleStep);
        Assert.Equal(0.0f, packed[0].MaxError);
        Assert.Equal(1u, packed[1].Level);
        Assert.Equal(2u, packed[1].SampleStep);
        Assert.Equal(3.5f, packed[1].MaxError);
        Assert.All(packed, value => Assert.Equal(0u, value.Padding));
    }

    [Fact]
    public void WeightAndErrorPackingRejectMalformedInput()
    {
        Assert.Throws<ArgumentException>(() =>
            TerrainPreparedPayloadPacking.PackWeights([1, 2, 3]));
        Assert.Throws<InvalidDataException>(() =>
            TerrainPreparedPayloadPacking.PackGeometricErrors(
                [new TerrainGeometricErrorLevel(0, 0, 0.0)]));
    }

    [Fact]
    public void PackedWeightsNormalizeDeclaredChannelsAndUseDeterministicFallback()
    {
        Vector4 normalized = TerrainPreparedPayloadPacking.NormalizeWeights(
            0x40302010u,
            layerCount: 3);

        Assert.Equal(1.0f, normalized.X + normalized.Y + normalized.Z + normalized.W, 5);
        Assert.Equal(0.0f, normalized.W);
        Assert.Equal(Vector4.UnitX, TerrainPreparedPayloadPacking.NormalizeWeights(0, 4));
        Assert.Equal(Vector4.UnitX, TerrainPreparedPayloadPacking.NormalizeWeights(
            0xff000000u,
            layerCount: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TerrainPreparedPayloadPacking.NormalizeWeights(0, 0));
    }

    [Fact]
    public void LayerDescriptorsUseStableLayoutAndLargeWorldUvPhase()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            1,
            1,
            worldPlacement: new ArisenEngine.Resources.Serialization.WorldPosition(
                -1_000_000_000.25,
                32.0,
                1_000_000_000.125));
        CookedTerrainLayer layer = fixture.Root.Layers[0];
        var descriptor = new TerrainGpuLayerDescriptor(
            fixture.Root,
            layer,
            albedoImageIndex: 11,
            albedoSamplerIndex: 12,
            normalImageIndex: 21,
            normalSamplerIndex: 22,
            ormImageIndex: 31,
            ormSamplerIndex: 32);

        Assert.Equal(TerrainGpuLayerDescriptor.Stride, Marshal.SizeOf<TerrainGpuLayerDescriptor>());
        Assert.Equal(11u, descriptor.AlbedoImageIndex);
        Assert.Equal(22u, descriptor.NormalSamplerIndex);
        Assert.Equal(31u, descriptor.OrmImageIndex);
        Assert.Equal(Vector4.One, descriptor.Tint);
        Assert.Equal(new Vector4(1.0f, 0.0f, 1.0f, 0.0f), descriptor.MaterialParameters);
        Assert.Equal(2.0f, descriptor.WorldTilingAndPhase.X);
        Assert.Equal(2.0f, descriptor.WorldTilingAndPhase.Y);
        Assert.Equal(0.5f, descriptor.WorldTilingAndPhase.Z);
        Assert.Equal(0.25f, descriptor.WorldTilingAndPhase.W);
    }
}

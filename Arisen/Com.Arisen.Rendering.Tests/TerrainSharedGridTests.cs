using ArisenEngine.Terrain.GenericRenderPipeline;
using ArisenEngine.Terrain;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainSharedGridTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(17)]
    [InlineData(33)]
    public void SharedGridBuildsCanonicalVerticesAndCounterClockwiseIndices(int resolution)
    {
        TerrainGridVertex[] vertices = TerrainSharedGridBuilder.CreateVertices(resolution);
        uint[] indices = TerrainSharedGridBuilder.CreateIndices(resolution);

        Assert.Equal(resolution * resolution, vertices.Length);
        Assert.Equal((resolution - 1) * (resolution - 1) * 6, indices.Length);
        Assert.Equal(0.0f, vertices[0].GridData.X);
        Assert.Equal(0.0f, vertices[0].GridData.Y);
        Assert.Equal(0.0f, vertices[0].GridData.Z);
        Assert.Equal(resolution, vertices[0].GridData.W);
        Assert.Equal(
            [0u, (uint)resolution, 1u, 1u, (uint)resolution, (uint)resolution + 1u],
            indices[..6]);
        Assert.All(indices, index => Assert.InRange(index, 0u, (uint)vertices.Length - 1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(4098)]
    public void SharedGridRejectsNonCanonicalResolution(int resolution)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TerrainSharedGridBuilder.CreateVertices(resolution));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TerrainSharedGridBuilder.CreateIndices(resolution));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(17)]
    [InlineData(33)]
    public void PatchIndexPatternsArePackedBoundedAndCoverEveryLod(int resolution)
    {
        TerrainGridIndexPatternSet patterns =
            TerrainSharedGridBuilder.CreateIndexPatterns(resolution);
        int expectedPatchIntervals = Math.Min(
            TerrainPatchTopology.MaximumPatchIntervalCount,
            resolution - 1);
        int expectedLodLevels = System.Numerics.BitOperations.Log2(
            checked((uint)expectedPatchIntervals)) + 1;

        Assert.Equal(resolution, patterns.Resolution);
        Assert.Equal(expectedPatchIntervals, patterns.PatchIntervalCount);
        Assert.Equal(expectedLodLevels, patterns.LodLevelCount);
        for (int lodLevel = 0; lodLevel < expectedLodLevels; lodLevel++)
        {
            int sampleStep = 1 << lodLevel;
            uint expectedIndexCount = checked((uint)(
                (expectedPatchIntervals / sampleStep) *
                (expectedPatchIntervals / sampleStep) * 6));
            for (int mask = 0; mask < 16; mask++)
            {
                TerrainGridIndexRange range = patterns.GetRange(
                    lodLevel,
                    (TerrainPatchStitchMask)mask);
                Assert.True(range.IsValid);
                Assert.Equal(expectedIndexCount, range.IndexCount);
                Assert.InRange(
                    checked(range.FirstIndex + range.IndexCount),
                    1u,
                    checked((uint)patterns.Indices.Length));
                ReadOnlySpan<uint> indices = patterns.Indices.AsSpan(
                    checked((int)range.FirstIndex),
                    checked((int)range.IndexCount));
                Assert.All(
                    indices.ToArray(),
                    index => Assert.InRange(
                        index,
                        0u,
                        checked((uint)(expectedPatchIntervals * resolution +
                                       expectedPatchIntervals))));
            }
        }
    }

    [Theory]
    [InlineData(TerrainPatchStitchMask.NegativeX)]
    [InlineData(TerrainPatchStitchMask.PositiveX)]
    [InlineData(TerrainPatchStitchMask.NegativeZ)]
    [InlineData(TerrainPatchStitchMask.PositiveZ)]
    public void StitchedFineEdgeUsesExactlyTheNeighborCoarseVertices(
        TerrainPatchStitchMask stitchMask)
    {
        const int resolution = 33;
        const int fineLod = 1;
        TerrainGridIndexPatternSet patterns =
            TerrainSharedGridBuilder.CreateIndexPatterns(resolution);
        TerrainGridIndexRange fineRange = patterns.GetRange(fineLod, stitchMask);
        TerrainGridIndexRange coarseRange = patterns.GetRange(
            fineLod + 1,
            TerrainPatchStitchMask.None);

        int[] fineCoordinates = GetEdgeCoordinates(
            patterns,
            fineRange,
            stitchMask);
        int[] coarseCoordinates = GetEdgeCoordinates(
            patterns,
            coarseRange,
            stitchMask);

        Assert.Equal(coarseCoordinates, fineCoordinates);
        Assert.Equal(
            Enumerable.Range(0, 5).Select(index => index * 4),
            fineCoordinates);
    }

    private static int[] GetEdgeCoordinates(
        TerrainGridIndexPatternSet patterns,
        TerrainGridIndexRange range,
        TerrainPatchStitchMask edge)
    {
        int boundary = patterns.PatchIntervalCount;
        return patterns.Indices
            .AsSpan(checked((int)range.FirstIndex), checked((int)range.IndexCount))
            .ToArray()
            .Select(index => (
                X: checked((int)(index % patterns.Resolution)),
                Z: checked((int)(index / patterns.Resolution))))
            .Where(vertex => edge switch
            {
                TerrainPatchStitchMask.NegativeX => vertex.X == 0,
                TerrainPatchStitchMask.PositiveX => vertex.X == boundary,
                TerrainPatchStitchMask.NegativeZ => vertex.Z == 0,
                TerrainPatchStitchMask.PositiveZ => vertex.Z == boundary,
                _ => false
            })
            .Select(vertex => edge is TerrainPatchStitchMask.NegativeX or
                                      TerrainPatchStitchMask.PositiveX
                ? vertex.Z
                : vertex.X)
            .Distinct()
            .Order()
            .ToArray();
    }
}

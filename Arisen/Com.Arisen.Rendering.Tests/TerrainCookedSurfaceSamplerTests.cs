using System.Numerics;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainCookedSurfaceSamplerTests
{
    [Fact]
    public void SamplesSignedTileCoordinatesFromArbitraryTileOrder()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            tileCountX: 3,
            tileCountZ: 2,
            resolution: 3,
            tileOrigin: new TerrainTileCoordinate(-2, -3),
            worldPlacement: new WorldPosition(-10_000.25, 7.0, -20_000.5),
            sampleSpacing: new TerrainSampleSpacing(2.0, 3.0));
        CookedTerrainTile[] arbitraryOrder =
        [
            fixture.Tiles[4],
            fixture.Tiles[0],
            fixture.Tiles[5],
            fixture.Tiles[2],
            fixture.Tiles[1],
            fixture.Tiles[3]
        ];
        var sampler = new CookedTerrainSurfaceSampler(fixture.Root, arbitraryOrder);

        foreach (CookedTerrainTile tile in fixture.Tiles)
        {
            var position = new WorldPosition(
                tile.WorldPlacement.X + (0.5 * tile.SampleSpacing.X),
                -123.0,
                tile.WorldPlacement.Z + (0.5 * tile.SampleSpacing.Z));

            Assert.True(sampler.TrySample(position, out CookedTerrainSurfaceSample sample));
            Assert.Equal(fixture.Root.Guid, sample.RootGuid);
            Assert.Equal(tile.Guid, sample.TileGuid);
            Assert.Equal(tile.Coordinate, sample.Coordinate);
            Assert.Equal(position.X, sample.SurfacePosition.X);
            Assert.Equal(position.Z, sample.SurfacePosition.Z);
        }
    }

    [Fact]
    public void PositiveInteriorBordersAndOuterMaximumAreOwnedInclusively()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            tileCountX: 2,
            tileCountZ: 2,
            resolution: 3,
            tileOrigin: new TerrainTileCoordinate(-2, -3),
            worldPlacement: new WorldPosition(-100.0, 5.0, -200.0),
            sampleSpacing: new TerrainSampleSpacing(2.0, 4.0));
        var sampler = new CookedTerrainSurfaceSampler(
            fixture.Root,
            [fixture.Tiles[3], fixture.Tiles[1], fixture.Tiles[2], fixture.Tiles[0]]);
        double sharedX = fixture.Tiles[1].WorldPlacement.X;
        double sharedZ = fixture.Tiles[2].WorldPlacement.Z;

        AssertSampleOwner(
            sampler,
            new WorldPosition(sharedX, 0.0, fixture.Tiles[1].WorldPlacement.Z + 1.0),
            fixture.Tiles[1]);
        AssertSampleOwner(
            sampler,
            new WorldPosition(fixture.Tiles[2].WorldPlacement.X + 1.0, 0.0, sharedZ),
            fixture.Tiles[2]);
        AssertSampleOwner(
            sampler,
            new WorldPosition(sharedX, 0.0, sharedZ),
            fixture.Tiles[3]);

        double maxX = fixture.Root.WorldPlacement.X +
                      ((fixture.Root.HeightSourceWidth - 1) * fixture.Root.SampleSpacing.X);
        double maxZ = fixture.Root.WorldPlacement.Z +
                      ((fixture.Root.HeightSourceHeight - 1) * fixture.Root.SampleSpacing.Z);
        AssertSampleOwner(sampler, new WorldPosition(maxX, 0.0, maxZ), fixture.Tiles[3]);
        Assert.False(sampler.TrySample(
            new WorldPosition(Math.BitIncrement(maxX), 0.0, maxZ),
            out _));
        Assert.False(sampler.TrySample(
            new WorldPosition(maxX, 0.0, Math.BitIncrement(maxZ)),
            out _));
    }

    [Theory]
    [InlineData(0.0, 0.7, 3)]
    [InlineData(1_000_000.0, 0.05, 1)]
    public void DecimalSpacingInteriorBordersUseCanonicalPositiveTileStarts(
        double rootX,
        double spacingX,
        int positiveTileIndex)
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            tileCountX: 4,
            tileCountZ: 1,
            resolution: 3,
            worldPlacement: new WorldPosition(rootX, 7.0, -20.0),
            sampleSpacing: new TerrainSampleSpacing(spacingX, 0.7));
        var sampler = new CookedTerrainSurfaceSampler(fixture.Root, fixture.Tiles);
        CookedTerrainTile positiveTile = fixture.Tiles[positiveTileIndex];
        var border = new WorldPosition(
            positiveTile.WorldPlacement.X,
            0.0,
            positiveTile.WorldPlacement.Z + 0.35);

        AssertSampleOwner(sampler, border, positiveTile);
        AssertSampleOwner(
            sampler,
            border with { X = Math.BitDecrement(border.X) },
            fixture.Tiles[positiveTileIndex - 1]);
    }

    [Fact]
    public void ReturnsBilinearWorldHeightUnitNormalAndNormalizedWeights()
    {
        var placement = new WorldPosition(-10.0, 7.0, -20.0);
        var spacing = new TerrainSampleSpacing(2.0, 4.0);
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            tileCountX: 1,
            tileCountZ: 1,
            resolution: 3,
            tileOrigin: new TerrainTileCoordinate(-1, -1),
            worldPlacement: placement,
            sampleSpacing: spacing,
            height: (x, z) => checked((ushort)(1_000 + (x * 300) + (z * 500))),
            weights: (x, z) => (x, z) switch
            {
                (0, 0) => (255, 0, 0, 0),
                (1, 0) => (0, 255, 0, 0),
                (0, 1) => (0, 0, 255, 0),
                _ => (0, 0, 0, 255)
            });
        var sampler = new CookedTerrainSurfaceSampler(fixture.Root, fixture.Tiles);
        var position = new WorldPosition(
            placement.X + (0.5 * spacing.X),
            -999.0,
            placement.Z + (0.5 * spacing.Z));

        Assert.True(sampler.TrySample(position, out CookedTerrainSurfaceSample sample));

        double expectedLocalHeight = Decode(fixture.Root.HeightRange, 1_400.0);
        double heightStepX = Decode(fixture.Root.HeightRange, 1_300.0) -
                             Decode(fixture.Root.HeightRange, 1_000.0);
        double heightStepZ = Decode(fixture.Root.HeightRange, 1_500.0) -
                             Decode(fixture.Root.HeightRange, 1_000.0);
        Vector3 expectedNormal = Vector3.Normalize(new Vector3(
            (float)(-heightStepX / spacing.X),
            1.0f,
            (float)(-heightStepZ / spacing.Z)));
        Assert.InRange(
            Math.Abs(sample.SurfacePosition.Y - (placement.Y + expectedLocalHeight)),
            0.0,
            1e-12);
        Assert.InRange(Vector3.Distance(expectedNormal, sample.Normal), 0.0f, 1e-6f);
        Assert.InRange(sample.Normal.Length(), 0.999999f, 1.000001f);
        Assert.True(sample.Normal.Y > 0.0f);
        Assert.Equal(new Vector4(0.25f), sample.LayerWeights);
        Assert.InRange(
            sample.LayerWeights.X + sample.LayerWeights.Y +
            sample.LayerWeights.Z + sample.LayerWeights.W,
            0.999999f,
            1.000001f);
    }

    [Fact]
    public void ReturnsFiniteNearHorizontalNormalForVerySteepFiniteTerrain()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            tileCountX: 1,
            tileCountZ: 1,
            resolution: 3,
            height: (x, _) => x == 0 ? (ushort)0 : ushort.MaxValue);
        var range = new TerrainHeightRange(0.0, 1.0e200);
        CookedTerrainTile tile = CloneWithHeightRange(fixture.Tiles[0], range);
        CookedTerrainRoot root = fixture.Root with
        {
            HeightRange = range,
            Tiles =
            [
                fixture.Root.Tiles[0] with
                {
                    MinHeight = range.Min,
                    MaxHeight = range.Max
                }
            ]
        };
        var sampler = new CookedTerrainSurfaceSampler(root, [tile]);
        var position = new WorldPosition(
            root.WorldPlacement.X + 0.5,
            0.0,
            root.WorldPlacement.Z + 0.5);

        Assert.True(sampler.TrySample(position, out CookedTerrainSurfaceSample sample));
        Assert.True(float.IsFinite(sample.Normal.X));
        Assert.True(float.IsFinite(sample.Normal.Y));
        Assert.True(float.IsFinite(sample.Normal.Z));
        Assert.True(sample.Normal.X < -0.999999f);
        Assert.InRange(sample.Normal.Y, 0.0f, 0.000001f);
        Assert.InRange(sample.Normal.Length(), 0.999999f, 1.000001f);
    }

    [Fact]
    public void SaddleGradientCancellationDoesNotOverflowInterpolation()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            tileCountX: 1,
            tileCountZ: 1,
            resolution: 3,
            height: (x, z) => ((x + z) & 1) == 0 ? ushort.MaxValue : (ushort)0);
        var range = new TerrainHeightRange(-5.0e307, 5.0e307);
        CookedTerrainTile tile = CloneWithHeightRange(fixture.Tiles[0], range);
        CookedTerrainRoot root = fixture.Root with
        {
            HeightRange = range,
            Tiles =
            [
                fixture.Root.Tiles[0] with
                {
                    MinHeight = range.Min,
                    MaxHeight = range.Max
                }
            ]
        };
        var sampler = new CookedTerrainSurfaceSampler(root, [tile]);
        var position = new WorldPosition(
            root.WorldPlacement.X + 0.5,
            0.0,
            root.WorldPlacement.Z + 0.5);

        Assert.True(sampler.TrySample(position, out CookedTerrainSurfaceSample sample));
        Assert.True(double.IsFinite(sample.SurfacePosition.Y));
        Assert.Equal(Vector3.UnitY, sample.Normal);
    }

    [Fact]
    public void TrySampleRejectsInvalidAndOutsidePositionsWithoutAllocating()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(1, 1, resolution: 3);
        var sampler = new CookedTerrainSurfaceSampler(fixture.Root, fixture.Tiles);
        WorldPosition inside = fixture.Root.WorldPlacement;

        Assert.False(sampler.TrySample(new WorldPosition(double.NaN, 0.0, 0.0), out _));
        Assert.False(sampler.TrySample(
            new WorldPosition(Math.BitDecrement(inside.X), inside.Y, inside.Z),
            out _));
        Assert.True(sampler.TrySample(inside, out _));
        bool allSucceeded = true;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1_000; index++)
        {
            allSucceeded &= sampler.TrySample(inside, out _);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.True(allSucceeded);
        Assert.Equal(before, after);
    }

    [Fact]
    public void ConstructorRejectsMalformedRootAndTileSets()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(2, 1, resolution: 3);

        Assert.Throws<InvalidOperationException>(() =>
            new CookedTerrainSurfaceSampler(fixture.Root, [fixture.Tiles[0]]));
        Assert.Throws<InvalidOperationException>(() =>
            new CookedTerrainSurfaceSampler(
                fixture.Root,
                [fixture.Tiles[0], fixture.Tiles[0]]));

        CookedTerrainTile mismatched = CloneWithRootGuid(
            fixture.Tiles[1],
            Guid.Parse("72000000-0000-0000-0000-000000000001"));
        Assert.Throws<InvalidOperationException>(() =>
            new CookedTerrainSurfaceSampler(
                fixture.Root,
                [fixture.Tiles[0], mismatched]));

        CookedTerrainTile brokenBorder = CloneWithChangedFirstHeight(fixture.Tiles[1]);
        Assert.Throws<InvalidOperationException>(() =>
            new CookedTerrainSurfaceSampler(
                fixture.Root,
                [fixture.Tiles[0], brokenBorder]));

        CookedTerrainTileReference[] duplicateReferences =
        [fixture.Root.Tiles[0], fixture.Root.Tiles[0]];
        Assert.Throws<InvalidOperationException>(() =>
            new CookedTerrainSurfaceSampler(
                fixture.Root with { Tiles = duplicateReferences },
                fixture.Tiles));

        var overflowingRange = new TerrainHeightRange(1.0e308, 1.1e308);
        CookedTerrainRoot overflowingVerticalCoverage = fixture.Root with
        {
            WorldPlacement = fixture.Root.WorldPlacement with { Y = 1.0e308 },
            HeightRange = overflowingRange
        };
        InvalidOperationException verticalError = Assert.Throws<InvalidOperationException>(() =>
            new CookedTerrainSurfaceSampler(overflowingVerticalCoverage, fixture.Tiles));
        Assert.Contains("sampling domain", verticalError.Message, StringComparison.Ordinal);
    }

    private static void AssertSampleOwner(
        CookedTerrainSurfaceSampler sampler,
        WorldPosition position,
        CookedTerrainTile expectedTile)
    {
        Assert.True(sampler.TrySample(position, out CookedTerrainSurfaceSample sample));
        Assert.Equal(expectedTile.Guid, sample.TileGuid);
        Assert.Equal(expectedTile.Coordinate, sample.Coordinate);
    }

    private static CookedTerrainTile CloneWithRootGuid(
        CookedTerrainTile tile,
        Guid rootGuid) => new(
            tile.Guid,
            rootGuid,
            tile.LayerSetGuid,
            tile.PackageId,
            tile.SourceSchemaVersion,
            tile.Coordinate,
            tile.Resolution,
            tile.LayerCount,
            tile.WorldPlacement,
            tile.SampleSpacing,
            tile.HeightRange,
            tile.MinHeight,
            tile.MaxHeight,
            tile.BorderPolicy,
            tile.SourceSampleOffsetX,
            tile.SourceSampleOffsetZ,
            tile.Heights.ToArray(),
            tile.LayerWeights.ToArray(),
            tile.GeometricErrors.ToArray());

    private static CookedTerrainTile CloneWithChangedFirstHeight(CookedTerrainTile tile)
    {
        ushort[] heights = tile.Heights.ToArray();
        heights[0]++;
        return new CookedTerrainTile(
            tile.Guid,
            tile.RootGuid,
            tile.LayerSetGuid,
            tile.PackageId,
            tile.SourceSchemaVersion,
            tile.Coordinate,
            tile.Resolution,
            tile.LayerCount,
            tile.WorldPlacement,
            tile.SampleSpacing,
            tile.HeightRange,
            tile.MinHeight,
            tile.MaxHeight,
            tile.BorderPolicy,
            tile.SourceSampleOffsetX,
            tile.SourceSampleOffsetZ,
            heights,
            tile.LayerWeights.ToArray(),
            tile.GeometricErrors.ToArray());
    }

    private static CookedTerrainTile CloneWithHeightRange(
        CookedTerrainTile tile,
        TerrainHeightRange heightRange) => new(
            tile.Guid,
            tile.RootGuid,
            tile.LayerSetGuid,
            tile.PackageId,
            tile.SourceSchemaVersion,
            tile.Coordinate,
            tile.Resolution,
            tile.LayerCount,
            tile.WorldPlacement,
            tile.SampleSpacing,
            heightRange,
            heightRange.Min,
            heightRange.Max,
            tile.BorderPolicy,
            tile.SourceSampleOffsetX,
            tile.SourceSampleOffsetZ,
            tile.Heights.ToArray(),
            tile.LayerWeights.ToArray(),
            tile.GeometricErrors.ToArray());

    private static double Decode(TerrainHeightRange range, double value) =>
        range.Min + (value / ushort.MaxValue * range.Scale);
}

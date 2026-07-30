using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainTileAccelerationTests
{
    [Fact]
    public void AccelerationBuildsContiguousConservativeFixedPatchHierarchy()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            1,
            1,
            resolution: 33,
            height: (x, z) => checked((ushort)(
                1000 +
                ((x * x * 29) % 20_000) +
                ((z * z * 17) % 20_000) +
                (((x + z) & 1) * 500))));
        CookedTerrainTile tile = fixture.Tiles[0];

        TerrainTileAcceleration acceleration = TerrainTileAcceleration.Build(tile);

        Assert.Equal(2, acceleration.PatchCountX);
        Assert.Equal(2, acceleration.PatchCountZ);
        Assert.Equal(4, acceleration.PatchCount);
        Assert.Equal(5, acceleration.LodLevelCount);
        for (int patchIndex = 0; patchIndex < acceleration.PatchCount; patchIndex++)
        {
            ref readonly TerrainPatchAcceleration patch = ref acceleration.GetPatch(patchIndex);
            Assert.Equal(patchIndex % 2, patch.PatchX);
            Assert.Equal(patchIndex / 2, patch.PatchZ);
            Assert.Equal(16, patch.IntervalCount);
            Assert.True(patch.MinHeight <= patch.MaxHeight);
            for (int z = patch.SampleZ; z <= patch.SampleZ + patch.IntervalCount; z++)
            {
                for (int x = patch.SampleX; x <= patch.SampleX + patch.IntervalCount; x++)
                {
                    double height = tile.DecodeHeight(tile.GetHeightSample(x, z));
                    Assert.InRange(height, patch.MinHeight, patch.MaxHeight);
                }
            }

            double previous = 0.0;
            for (int lod = 0; lod < acceleration.LodLevelCount; lod++)
            {
                double error = acceleration.GetGeometricError(patchIndex, lod);
                Assert.True(double.IsFinite(error));
                Assert.True(error >= previous);
                Assert.True(error <= tile.GeometricErrors[lod].MaxError + 1.0e-9);
                previous = error;
            }
        }
    }

    [Fact]
    public void RuntimeStoreRejectsRootTileIdentityOrPlacementMismatch()
    {
        TerrainRuntimeFixture fixture = TerrainRuntimeTestData.Create(
            1,
            1,
            resolution: 3,
            tileOrigin: new TerrainTileCoordinate(-7, 4),
            worldPlacement: new WorldPosition(5_000_000.25, 20.0, -6_000_000.5));
        CookedTerrainTile source = fixture.Tiles[0];
        var mismatched = new CookedTerrainTile(
            source.Guid,
            source.RootGuid,
            source.LayerSetGuid,
            source.PackageId,
            source.SourceSchemaVersion,
            source.Coordinate,
            source.Resolution,
            source.LayerCount,
            new WorldPosition(
                source.WorldPlacement.X + 1.0,
                source.WorldPlacement.Y,
                source.WorldPlacement.Z),
            source.SampleSpacing,
            source.HeightRange,
            source.MinHeight,
            source.MaxHeight,
            source.BorderPolicy,
            source.SourceSampleOffsetX,
            source.SourceSampleOffsetZ,
            source.Heights.ToArray(),
            source.LayerWeights.ToArray(),
            source.GeometricErrors.ToArray());
        var runtimeData = new TerrainRuntimeDataStore();
        runtimeData.PublishRoot(fixture.Root);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => runtimeData.PublishTile(mismatched));

        Assert.Contains("placement", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(runtimeData.Tiles.ToArray());
    }
}

using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;

namespace Com.Arisen.Rendering.Tests;

internal sealed record TerrainRuntimeFixture(
    CookedTerrainRoot Root,
    CookedTerrainTile[] Tiles);

internal static class TerrainRuntimeTestData
{
    public static readonly Guid RootGuid =
        Guid.Parse("71000000-0000-0000-0000-000000000001");
    public static readonly Guid LayerSetGuid =
        Guid.Parse("71000000-0000-0000-0000-000000000002");
    public const string PackageId = "com.arisen.tests.terrain";

    public static TerrainRuntimeFixture Create(
        int tileCountX,
        int tileCountZ,
        int resolution = 17,
        TerrainTileCoordinate? tileOrigin = null,
        WorldPosition? worldPlacement = null,
        TerrainSampleSpacing? sampleSpacing = null,
        Func<int, int, ushort>? height = null,
        Func<int, int, (byte X, byte Y, byte Z, byte W)>? weights = null)
    {
        var origin = tileOrigin ?? new TerrainTileCoordinate(0, 0);
        var placement = worldPlacement ?? new WorldPosition(0.0, 0.0, 0.0);
        var spacing = sampleSpacing ?? new TerrainSampleSpacing(1.0, 1.0);
        var heightRange = new TerrainHeightRange(0.0, 100.0);
        int intervals = resolution - 1;
        int sourceWidth = checked((tileCountX * intervals) + 1);
        int sourceHeight = checked((tileCountZ * intervals) + 1);
        var tiles = new CookedTerrainTile[checked(tileCountX * tileCountZ)];
        var references = new CookedTerrainTileReference[tiles.Length];
        var guids = new Guid[tileCountX, tileCountZ];
        for (int z = 0; z < tileCountZ; z++)
        {
            for (int x = 0; x < tileCountX; x++)
            {
                TerrainTileCoordinate coordinate = new(origin.X + x, origin.Z + z);
                guids[x, z] = TerrainTileIdentity.CreateGuid(
                    RootGuid,
                    PackageId,
                    coordinate);
            }
        }

        int tileIndex = 0;
        for (int z = 0; z < tileCountZ; z++)
        {
            for (int x = 0; x < tileCountX; x++)
            {
                TerrainTileCoordinate coordinate = new(origin.X + x, origin.Z + z);
                int sourceOffsetX = x * intervals;
                int sourceOffsetZ = z * intervals;
                var samples = new ushort[checked(resolution * resolution)];
                var layerWeights = new byte[
                    checked(samples.Length * TerrainCookedFormat.WeightChannelCount)];
                ushort minimumSample = ushort.MaxValue;
                ushort maximumSample = ushort.MinValue;
                for (int sampleZ = 0; sampleZ < resolution; sampleZ++)
                {
                    for (int sampleX = 0; sampleX < resolution; sampleX++)
                    {
                        int globalX = sourceOffsetX + sampleX;
                        int globalZ = sourceOffsetZ + sampleZ;
                        int sampleIndex = (sampleZ * resolution) + sampleX;
                        ushort value = height?.Invoke(globalX, globalZ) ??
                                       checked((ushort)((globalX * 97 + globalZ * 53) & 0xffff));
                        samples[sampleIndex] = value;
                        minimumSample = Math.Min(minimumSample, value);
                        maximumSample = Math.Max(maximumSample, value);
                        var channels = weights?.Invoke(globalX, globalZ) ??
                                       ((byte)255, (byte)0, (byte)0, (byte)0);
                        int weightOffset = sampleIndex * TerrainCookedFormat.WeightChannelCount;
                        layerWeights[weightOffset] = channels.Item1;
                        layerWeights[weightOffset + 1] = channels.Item2;
                        layerWeights[weightOffset + 2] = channels.Item3;
                        layerWeights[weightOffset + 3] = channels.Item4;
                    }
                }

                var tilePlacement = new WorldPosition(
                    placement.X + (sourceOffsetX * spacing.X),
                    placement.Y,
                    placement.Z + (sourceOffsetZ * spacing.Z));
                TerrainGeometricErrorLevel[] errors =
                    TerrainTileAssetCooker.BuildGeometricErrors(
                        samples,
                        resolution,
                        heightRange);
                double minimumHeight = Decode(heightRange, minimumSample);
                double maximumHeight = Decode(heightRange, maximumSample);
                var tile = new CookedTerrainTile(
                    guids[x, z],
                    RootGuid,
                    LayerSetGuid,
                    PackageId,
                    sourceSchemaVersion: 1,
                    coordinate,
                    resolution,
                    layerCount: 1,
                    tilePlacement,
                    spacing,
                    heightRange,
                    minimumHeight,
                    maximumHeight,
                    TerrainBorderPolicy.SharedEdgeSamples,
                    sourceOffsetX,
                    sourceOffsetZ,
                    samples,
                    layerWeights,
                    errors);
                tiles[tileIndex] = tile;
                references[tileIndex] = new CookedTerrainTileReference(
                    coordinate,
                    tile.Guid,
                    new TerrainTileNeighborSet(
                        x > 0 ? guids[x - 1, z] : Guid.Empty,
                        x + 1 < tileCountX ? guids[x + 1, z] : Guid.Empty,
                        z > 0 ? guids[x, z - 1] : Guid.Empty,
                        z + 1 < tileCountZ ? guids[x, z + 1] : Guid.Empty),
                    minimumHeight,
                    maximumHeight,
                    PayloadBytes: 1024,
                    Enumerable.Repeat((byte)(tileIndex + 1), 32).ToArray());
                tileIndex++;
            }
        }

        Guid albedo = Guid.Parse("71000000-0000-0000-0000-000000000011");
        Guid normal = Guid.Parse("71000000-0000-0000-0000-000000000012");
        Guid orm = Guid.Parse("71000000-0000-0000-0000-000000000013");
        var root = new CookedTerrainRoot(
            RootGuid,
            PackageId,
            SourceSchemaVersion: 1,
            "Runtime fixture",
            placement,
            spacing,
            heightRange,
            sourceWidth,
            sourceHeight,
            resolution,
            TerrainBorderPolicy.SharedEdgeSamples,
            origin,
            LayerSetGuid,
            PackageId,
            [new CookedTerrainLayer(
                "base",
                new CookedTerrainTextureReference(albedo, PackageId),
                new CookedTerrainTextureReference(normal, PackageId),
                new CookedTerrainTextureReference(orm, PackageId),
                TerrainLayerTint.White,
                1.0f,
                0.0f,
                1.0f,
                TerrainLayerWorldTiling.Default)],
            references);
        return new TerrainRuntimeFixture(root, tiles);
    }

    public static TerrainTileComponent CreateComponent(
        CookedTerrainTile tile,
        bool visible = true) => new()
    {
        TerrainRootGuid = tile.RootGuid,
        TileGuid = tile.Guid,
        LayerSetGuid = tile.LayerSetGuid,
        TileX = tile.Coordinate.X,
        TileZ = tile.Coordinate.Z,
        WorldX = tile.WorldPlacement.X,
        WorldY = tile.WorldPlacement.Y,
        WorldZ = tile.WorldPlacement.Z,
        Flags = visible ? TerrainTileFlags.Visible : TerrainTileFlags.None
    };

    private static double Decode(TerrainHeightRange range, ushort value) =>
        range.Min + ((double)value / ushort.MaxValue * range.Scale);
}

using System.Globalization;
using System.Numerics;
using ArisenEngine.Core.Assets;
using ArisenEngine.Rendering.Resources;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Com.Arisen.Rendering.Tests;

/// <summary>
/// Pins the coverage contract of the terrain-streaming smoke fixture.
///
/// Every checkpoint the fixture captures must keep every canonical ShowcaseValley tile inside the
/// render frustum: the summary validator rejects a checkpoint whose tile selects no patch, and a
/// fully culled tile would leave the captured frame without that tile. The canonical root covers a
/// 512 m square, which is wider than the frustum the fixture renders with - roughly 73 degrees
/// horizontally at the scene camera's 45 degree vertical field of view and 16:9 - so a pose anywhere
/// inside the raster has tiles behind and beside the view direction and selects no patch for them.
/// Only the root's corner regions put the whole raster ahead of the camera, so the fixture derives
/// its own corner poses, aim and height from the loaded terrain bounds and the runtime surface
/// query, and keeps only the authored camera bearing. Reusing the authored showcase rotation would
/// leave the canonical tile behind the view direction fully culled, and reusing the authored camera
/// height would walk a pose that moves across the terrain into the hillside, which captures the
/// inside of the terrain and reads back as an inverted frame. This test rebuilds the fixture path
/// from the authored showcase camera and the cooked package content and culls the canonical tiles
/// with the production frustum test, so a content framing change fails here instead of in the
/// runtime gate.
/// </summary>
public sealed class TerrainStreamingCameraPathTests
{
    private const int ViewportWidth = 1280;
    private const int ViewportHeight = 720;

    /// <summary>
    /// Synthetic valley floor the fixture tests stand their derived poses on. The path contract is
    /// not about content heights: a derived pose has to be measured against whatever surface the
    /// runtime reports for the pose's own horizontal position, so the tests pin a flat plane whose
    /// resulting eye height the assertions can predict exactly.
    /// </summary>
    private const double SyntheticSurfaceHeight = 4.0;

    private static double FlatSurface(double worldX, double worldZ) => SyntheticSurfaceHeight;

    /// <summary>
    /// Surface query that cannot answer for any position, which is the state the fixture builds its
    /// discovery path in.
    /// </summary>
    private static double UnavailableSurface(double worldX, double worldZ) => double.NaN;

    private static readonly Guid s_TerrainLayerSetGuid =
        Guid.Parse("5dcaa6bd-2b51-498d-9fa9-bd68f4642761");

    /// <summary>
    /// Cooked ShowcaseValley tile bounds as the runtime reports them to the fixture: sixteen tiles of
    /// 128 m covering world -256..256, which is the 512 m square one showcase cell spans. The bounds
    /// are cooked from the shipped package content instead of being pinned as literals, because the
    /// fixture derives every pose from whatever bounds the runtime loads: a content change that
    /// resizes the raster has to fail here instead of silently invalidating the coverage contract.
    /// Origin-relative positions equal world positions at rebase sequence 0, which is the state every
    /// pre-rebase checkpoint is captured in.
    /// </summary>
    private static readonly Lazy<TerrainPatchWorldBounds[]> s_Tiles = new(LazyCanonicalTileBounds);

    private static TerrainPatchWorldBounds[] CanonicalTiles() => s_Tiles.Value;

    private static TerrainPatchWorldBounds[] LazyCanonicalTileBounds()
    {
        string packageRoot = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            VegetationCanonicalFixture.PackageId);
        string cookedRoot = Path.Combine(
            Path.GetTempPath(),
            "ArisenTerrainStreamingCameraPathTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, cookedRoot);
            AddCanonicalTerrainAssets(database, packageRoot);
            var rootRef = new AssetRef<TerrainRootSourceAsset>(
                VegetationCanonicalFixture.TerrainRootGuid,
                TerrainAssetTypes.Root,
                VegetationCanonicalFixture.PackageId);
            TerrainRootAssetCooker.Cook(database, rootRef);
            Assert.True(
                TerrainRootAssetCooker.TryLoadCooked(
                    database, rootRef, out CookedTerrainRoot root, out string rootDiagnostic),
                rootDiagnostic);
            Assert.Equal(VegetationCanonicalFixture.TerrainTileCount, root.Tiles.Count);

            var bounds = new TerrainPatchWorldBounds[VegetationCanonicalFixture.TerrainTileCount];
            for (int tileZ = 0; tileZ < VegetationCanonicalFixture.TerrainTilesPerAxis; tileZ++)
            {
                for (int tileX = 0; tileX < VegetationCanonicalFixture.TerrainTilesPerAxis; tileX++)
                {
                    CookedTerrainTile tile = LoadCanonicalTerrainTile(database, root, tileX, tileZ);
                    bounds[(tileZ * VegetationCanonicalFixture.TerrainTilesPerAxis) + tileX] =
                        TileWorldBounds(tile);
                }
            }

            // The corner poses only frame the whole raster while the canonical root stays the 512 m
            // square the fixture path is built for, so the footprint itself is part of the contract.
            TerrainPatchWorldBounds union = UnionBounds(bounds);
            Assert.Equal(-256.0, union.Min.X);
            Assert.Equal(256.0, union.Max.X);
            Assert.Equal(-256.0, union.Min.Z);
            Assert.Equal(256.0, union.Max.Z);
            Assert.True(union.Max.Y > union.Min.Y, "Cooked terrain bounds span no height.");
            return bounds;
        }
        finally
        {
            try
            {
                if (Directory.Exists(cookedRoot))
                {
                    Directory.Delete(cookedRoot, recursive: true);
                }
            }
            catch
            {
                // Best-effort test cleanup.
            }
        }
    }

    /// <summary>
    /// World bounds of one cooked tile, from the placement and height the runtime reports for it.
    /// The terrain pipeline builds the same extent out of the cooked placement, the sample spacing
    /// and the tile's own minimum and maximum height.
    /// </summary>
    private static TerrainPatchWorldBounds TileWorldBounds(CookedTerrainTile tile) => new(
        new WorldPosition(
            tile.WorldPlacement.X,
            tile.WorldPlacement.Y + tile.MinHeight,
            tile.WorldPlacement.Z),
        new WorldPosition(
            tile.WorldPlacement.X + ((tile.Resolution - 1) * tile.SampleSpacing.X),
            tile.WorldPlacement.Y + tile.MaxHeight,
            tile.WorldPlacement.Z + ((tile.Resolution - 1) * tile.SampleSpacing.Z)));

    private static CookedTerrainTile LoadCanonicalTerrainTile(
        TestAssetDatabase database,
        in CookedTerrainRoot root,
        int tileX,
        int tileZ)
    {
        var coordinate = new TerrainTileCoordinate(tileX, tileZ);
        Guid tileGuid = TerrainTileIdentity.CreateGuid(
            VegetationCanonicalFixture.TerrainRootGuid,
            VegetationCanonicalFixture.PackageId,
            coordinate);
        Assert.True(
            TerrainTileAssetCooker.TryLoadCooked(
                database,
                new AssetRef<TerrainTileSourceAsset>(
                    tileGuid, TerrainAssetTypes.Tile, VegetationCanonicalFixture.PackageId),
                VegetationCanonicalFixture.TerrainRootGuid,
                s_TerrainLayerSetGuid,
                out CookedTerrainTile tile,
                out string diagnostic),
            diagnostic);
        Assert.Equal(coordinate, tile.Coordinate);
        Assert.Equal(
            root.WorldPlacement.X +
            (tileX * (tile.Resolution - 1) * tile.SampleSpacing.X),
            tile.WorldPlacement.X);
        Assert.Equal(
            root.WorldPlacement.Z +
            (tileZ * (tile.Resolution - 1) * tile.SampleSpacing.Z),
            tile.WorldPlacement.Z);
        return tile;
    }

    private static void AddCanonicalTerrainAssets(TestAssetDatabase database, string packageRoot)
    {
        database.AddAsset(
            VegetationCanonicalFixture.TerrainRootGuid,
            TerrainAssetTypes.Root,
            Path.Combine(packageRoot, "Assets", "Terrain", "ShowcaseValley.aristerrain"),
            VegetationCanonicalFixture.PackageId);
        database.AddAsset(
            s_TerrainLayerSetGuid,
            TerrainAssetTypes.LayerSet,
            Path.Combine(packageRoot, "Assets", "Terrain", "ShowcaseValley.ariterrainlayers"),
            VegetationCanonicalFixture.PackageId);
        for (int tileZ = 0; tileZ < VegetationCanonicalFixture.TerrainTilesPerAxis; tileZ++)
        {
            for (int tileX = 0; tileX < VegetationCanonicalFixture.TerrainTilesPerAxis; tileX++)
            {
                database.AddAsset(
                    TerrainTileIdentity.CreateGuid(
                        VegetationCanonicalFixture.TerrainRootGuid,
                        VegetationCanonicalFixture.PackageId,
                        new TerrainTileCoordinate(tileX, tileZ)),
                    TerrainAssetTypes.Tile,
                    Path.Combine(
                        packageRoot,
                        "Assets",
                        "Terrain",
                        "Generated",
                        "ShowcaseValley",
                        $"x_{tileX}_z_{tileZ}.ariterraingenerated"),
                    VegetationCanonicalFixture.PackageId);
            }
        }
    }

    [Fact]
    public void LookRotation_PointsAtTarget()
    {
        TerrainPatchWorldBounds rootBounds = UnionBounds(CanonicalTiles());
        WorldPosition target = Center(rootBounds);
        WorldPosition[] positions =
        [
            new WorldPosition(3.0, 2.0, -6.0),
            new WorldPosition(target.X, 2.0, -6.0),
            new WorldPosition(34.125829, 7.616215, -42.105961),
            new WorldPosition(target.X, 12.0, target.Z)
        ];

        foreach (WorldPosition position in positions)
        {
            Quaternion rotation = TerrainStreamingCameraPath.LookRotation(position, target);
            Vector3 forward = TerrainStreamingCameraPath.Forward(rotation);
            Vector3 expected = Vector3.Normalize(new Vector3(
                (float)(target.X - position.X),
                (float)(target.Y - position.Y),
                (float)(target.Z - position.Z)));

            Assert.Equal(1.0f, rotation.Length(), precision: 5);
            AssertDirectionMatches(expected, forward, "LookRotation forward");
        }
    }

    /// <summary>
    /// The fixture must never capture a pose whose aim is inherited from authored content: the
    /// authored showcase rotation culls a canonical tile once the camera looks along the terrain.
    /// Each pose aims at the bounds centre on the horizontal axis with the elevation inside the
    /// fixture's horizon band, because the exact aim at the centre of the bounding box points the
    /// view above the ridge line from a camera that stands inside the valley.
    /// </summary>
    [Fact]
    public void Build_AimsEveryCapturedPoseAtTheTerrainBounds()
    {
        ShowcaseCamera authored = ReadAuthoredShowcaseCamera();
        TerrainPatchWorldBounds rootBounds = UnionBounds(CanonicalTiles());
        TerrainStreamingCameraPoses poses = TerrainStreamingCameraPath.Build(
            rootBounds,
            authored.Position,
            FlatSurface);

        AssertAimsAtBounds("near", poses.NearRotation, poses.NearPosition, rootBounds);
        AssertAimsAtBounds(
            "boundary",
            poses.BoundaryRotation,
            poses.BoundaryPosition,
            rootBounds);
        AssertAimsAtBounds("far", poses.FarRotation, poses.FarPosition, rootBounds);

        // Every derived pose stands the fixture clearance above the surface the runtime reported at
        // its own horizontal position. Inheriting the authored camera height instead walks the pose
        // into the hillside as soon as the terrain under it rises above the authored pose, which is
        // what captured the inside of the terrain and failed the upright orientation check.
        double expectedEyeHeight =
            SyntheticSurfaceHeight + TerrainStreamingCameraPath.EyeClearanceMetres;
        Assert.Equal(expectedEyeHeight, poses.BoundaryPosition.Y, precision: 5);
        Assert.Equal(expectedEyeHeight, poses.FarPosition.Y, precision: 5);
    }

    /// <summary>
    /// The fixture builds its path once during discovery, before the canonical tiles answer the
    /// runtime surface query. The fallback pose therefore has to be safe without any query at all:
    /// the highest point of the terrain plus the fixture clearance keeps every derived camera above
    /// the surface, which is exactly what the authored height failed to guarantee.
    /// </summary>
    [Fact]
    public void Build_WithoutARuntimeSurfaceQuery_KeepsEveryDerivedPoseAboveTheTerrain()
    {
        ShowcaseCamera authored = ReadAuthoredShowcaseCamera();
        TerrainPatchWorldBounds rootBounds = UnionBounds(CanonicalTiles());
        double expectedEyeHeight = rootBounds.Max.Y + TerrainStreamingCameraPath.EyeClearanceMetres;
        TerrainStreamingCameraPoses[] poses =
        [
            TerrainStreamingCameraPath.Build(rootBounds, authored.Position),
            TerrainStreamingCameraPath.Build(rootBounds, authored.Position, UnavailableSurface)
        ];

        foreach (TerrainStreamingCameraPoses built in poses)
        {
            Assert.Equal(expectedEyeHeight, built.BoundaryPosition.Y, precision: 5);
            Assert.Equal(expectedEyeHeight, built.FarPosition.Y, precision: 5);
            Assert.True(built.BoundaryPosition.Y > rootBounds.Max.Y);
            Assert.True(built.FarPosition.Y > rootBounds.Max.Y);
        }
    }

    /// <summary>
    /// Pins the framing contract the summary validator calls upright: every fixture pose pitches
    /// under the horizon, which keeps the streaming surface in the lower frame, while the top edge
    /// of the frame stays above the horizon, which keeps sky over the ridge line. A pose that looks
    /// up reads back as terrain over sky and fails the upright orientation check.
    /// </summary>
    [Fact]
    public void FixtureCheckpoints_KeepTheHorizonInsideTheUpperFrame()
    {
        ShowcaseCamera authored = ReadAuthoredShowcaseCamera();
        TerrainPatchWorldBounds rootBounds = UnionBounds(CanonicalTiles());
        TerrainStreamingCameraPoses poses = TerrainStreamingCameraPath.Build(
            rootBounds,
            authored.Position,
            FlatSurface);
        float halfFieldOfView = authored.VerticalFieldOfViewDegrees * 0.5f;

        AssertKeepsHorizonInUpperFrame("near", poses.NearRotation, halfFieldOfView);
        AssertKeepsHorizonInUpperFrame("boundary-mixed-lod", poses.BoundaryRotation, halfFieldOfView);
        AssertKeepsHorizonInUpperFrame("far-cascade", poses.FarRotation, halfFieldOfView);
    }

    private static void AssertKeepsHorizonInUpperFrame(
        string pose,
        Quaternion rotation,
        float halfFieldOfView)
    {
        float elevation = TerrainStreamingCameraPath.ElevationDegrees(rotation);
        Assert.True(elevation < 0.0f, $"Fixture pose '{pose}' looks above the horizon.");
        Assert.True(
            elevation + halfFieldOfView > 0.0f,
            $"Fixture pose '{pose}' pitches the whole frame below the horizon.");
    }

    [Fact]
    public void LookRotation_RejectsDegenerateDirection()
    {
        var position = new WorldPosition(3.0, 2.0, -6.0);

        Assert.Throws<InvalidOperationException>(
            () => TerrainStreamingCameraPath.LookRotation(position, position));
    }

    [Fact]
    public void FixtureCheckpoints_CoverEveryCanonicalTile()
    {
        ShowcaseCamera authored = ReadAuthoredShowcaseCamera();
        TerrainPatchWorldBounds rootBounds = UnionBounds(CanonicalTiles());
        TerrainStreamingCameraPoses poses = TerrainStreamingCameraPath.Build(
            rootBounds,
            authored.Position,
            FlatSurface);

        AssertCoversEveryTile("near", poses.NearPosition, poses.NearRotation, authored);
        AssertCoversEveryTile(
            "boundary-mixed-lod",
            poses.BoundaryPosition,
            poses.BoundaryRotation,
            authored);
        // The post-rebase checkpoint parks the camera on the far pose and captures it again, so the
        // far assertion above is the coverage assertion for it.
        AssertCoversEveryTile("far-cascade", poses.FarPosition, poses.FarRotation, authored);

        // Negative control: a camera far outside the terrain aimed away from it must cull every
        // tile, so the coverage assertions above cannot pass vacuously. The fixture poses all sit
        // inside or next to the terrain, where some tile always stays in view.
        WorldPosition target = Center(rootBounds);
        var awayPosition = new WorldPosition(
            rootBounds.Max.X + 512.0,
            authored.Position.Y,
            target.Z);
        var awayTarget = new WorldPosition(
            awayPosition.X + 512.0,
            awayPosition.Y,
            awayPosition.Z);
        Quaternion away = TerrainStreamingCameraPath.LookRotation(awayPosition, awayTarget);
        Matrix4x4 awayViewProjection = BuildViewProjection(awayPosition, away, authored);
        foreach (TerrainPatchWorldBounds tile in CanonicalTiles())
        {
            Assert.False(
                TerrainPatchFrustum.IsVisible(
                    ToVector3(tile.Min),
                    ToVector3(tile.Max),
                    awayViewProjection),
                "The terrain frustum test reported a tile visible from a camera aimed away from it.");
        }
    }

    private static void AssertCoversEveryTile(
        string checkpoint,
        WorldPosition position,
        Quaternion rotation,
        ShowcaseCamera camera)
    {
        Matrix4x4 viewProjection = BuildViewProjection(position, rotation, camera);
        foreach (TerrainPatchWorldBounds tile in CanonicalTiles())
        {
            Assert.True(
                TerrainPatchFrustum.IsVisible(
                    ToVector3(tile.Min),
                    ToVector3(tile.Max),
                    viewProjection),
                $"Terrain checkpoint '{checkpoint}' culled tile {tile.Min.X},{tile.Min.Z}.");
        }
    }

    /// <summary>
    /// Compares the horizontal aim against the direction to the bounds centre and pins the
    /// elevation inside the fixture's horizon band. The elevation cannot be compared component
    /// wise: the band deliberately clamps it, because an exact aim at the centre of the bounding
    /// box points the view above the ridge line from a camera that stands inside the valley.
    /// </summary>
    private static void AssertAimsAtBounds(
        string pose,
        Quaternion rotation,
        WorldPosition position,
        in TerrainPatchWorldBounds bounds)
    {
        Vector3 forward = TerrainStreamingCameraPath.Forward(rotation);
        Vector3 expected = Vector3.Normalize(ToVector3(Center(bounds)) - ToVector3(position));
        Vector3 expectedHorizontal = Vector3.Normalize(new Vector3(expected.X, 0.0f, expected.Z));
        Vector3 actualHorizontal = Vector3.Normalize(new Vector3(forward.X, 0.0f, forward.Z));
        AssertDirectionMatches(
            expectedHorizontal,
            actualHorizontal,
            $"Fixture pose '{pose}' does not aim at the terrain bounds centre");
        Assert.InRange(
            TerrainStreamingCameraPath.ElevationDegrees(rotation),
            TerrainStreamingCameraPath.MinimumElevationDegrees - 0.01f,
            TerrainStreamingCameraPath.MaximumElevationDegrees + 0.01f);
    }

    /// <summary>
    /// Compares directions within the float error of the yaw/pitch round trip through a normalized
    /// quaternion. Component rounding instead of a tolerance would fail on rounding boundaries even
    /// though the vectors agree far beyond the engine's own precision.
    /// </summary>
    private static void AssertDirectionMatches(
        Vector3 expected,
        Vector3 actual,
        string description)
    {
        const float tolerance = 1e-5f;
        Assert.True(
            MathF.Abs(expected.X - actual.X) <= tolerance &&
            MathF.Abs(expected.Y - actual.Y) <= tolerance &&
            MathF.Abs(expected.Z - actual.Z) <= tolerance,
            $"{description}: expected ({expected.X}, {expected.Y}, {expected.Z}), " +
            $"got ({actual.X}, {actual.Y}, {actual.Z}).");
    }

    /// <summary>
    /// Mirrors the render camera's basis and the pipeline's world-frame view-projection.
    /// </summary>
    private static Matrix4x4 BuildViewProjection(
        WorldPosition position,
        Quaternion rotation,
        ShowcaseCamera camera)
    {
        Vector3 forward = TerrainStreamingCameraPath.Forward(rotation);
        Vector3 up = Vector3.Transform(Vector3.UnitY, rotation);
        Vector3 eye = ToVector3(position);
        Matrix4x4 view = Matrix4x4.CreateLookAt(eye, eye + forward, up);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            camera.VerticalFieldOfViewDegrees * (MathF.PI / 180.0f),
            ViewportWidth / (float)ViewportHeight,
            camera.NearPlane,
            camera.FarPlane);
        return view * projection;
    }

    /// <summary>
    /// A pose can keep every tile inside the frustum and still capture an empty frame. The fixture
    /// used to aim the boundary pose at the bounds centre from directly underneath it, which made
    /// the view direction vertical: every ground patch fell outside the frustum and the captured
    /// frame held nothing but sky, so the runtime visual gate failed on zero written depth. Every
    /// captured frame therefore has to look down at the terrain surface across its lower half.
    /// </summary>
    [Fact]
    public void FixtureCheckpoints_FrameTheTerrainSurface()
    {
        ShowcaseCamera authored = ReadAuthoredShowcaseCamera();
        TerrainPatchWorldBounds rootBounds = UnionBounds(CanonicalTiles());
        TerrainStreamingCameraPoses poses = TerrainStreamingCameraPath.Build(
            rootBounds,
            authored.Position,
            FlatSurface);

        AssertFramesTerrainSurface(
            "near",
            poses.NearPosition,
            poses.NearRotation,
            authored,
            rootBounds);
        AssertFramesTerrainSurface(
            "boundary-mixed-lod",
            poses.BoundaryPosition,
            poses.BoundaryRotation,
            authored,
            rootBounds);
        AssertFramesTerrainSurface(
            "far-cascade",
            poses.FarPosition,
            poses.FarRotation,
            authored,
            rootBounds);

        // Negative control: the retired framing aimed straight up at the bounds centre from
        // directly underneath it, which keeps the surface out of frame.
        WorldPosition centre = Center(rootBounds);
        var underneath = new WorldPosition(centre.X, centre.Y - 20.0, centre.Z);
        Quaternion overhead = TerrainStreamingCameraPath.LookRotation(
            underneath,
            new WorldPosition(underneath.X, underneath.Y + 100.0, underneath.Z));
        foreach ((float x, float y) in s_FramingSamples)
        {
            Assert.False(
                TryRaycastTerrainFloor(underneath, overhead, authored, x, y, rootBounds, out _),
                "Terrain framing reported the surface in frame for a camera aimed straight up.");
        }
    }

    /// <summary>
    /// Device coordinates inside the lower half of the frame. The runtime capture measures written
    /// depth over the whole frame, and the surface has to cover that band for the capture to pass.
    /// </summary>
    private static readonly (float X, float Y)[] s_FramingSamples =
    [
        (0.0f, -0.35f),
        (0.0f, -0.75f),
        (-0.7f, -0.75f),
        (0.7f, -0.75f)
    ];

    private static void AssertFramesTerrainSurface(
        string checkpoint,
        WorldPosition position,
        Quaternion rotation,
        ShowcaseCamera camera,
        in TerrainPatchWorldBounds bounds)
    {
        foreach ((float x, float y) in s_FramingSamples)
        {
            Assert.True(
                TryRaycastTerrainFloor(position, rotation, camera, x, y, bounds, out float distance),
                $"Terrain checkpoint '{checkpoint}' left the surface out of frame at " +
                $"device coordinates ({x}, {y}).");
            Assert.InRange(distance, camera.NearPlane, camera.FarPlane);
        }
    }

    /// <summary>
    /// Distance along the view ray through the given device coordinates down to the terrain floor,
    /// or false when that ray does not look down at the terrain footprint. The floor is the lowest
    /// sample of the cooked terrain, so a ray that reaches it inside the footprint is blocked by
    /// the surface above it and writes depth in the captured frame.
    /// </summary>
    private static bool TryRaycastTerrainFloor(
        WorldPosition position,
        Quaternion rotation,
        ShowcaseCamera camera,
        float deviceX,
        float deviceY,
        in TerrainPatchWorldBounds bounds,
        out float distance)
    {
        distance = 0.0f;
        Vector3 forward = TerrainStreamingCameraPath.Forward(rotation);
        Vector3 up = Vector3.Transform(Vector3.UnitY, rotation);
        Vector3 right = Vector3.Normalize(Vector3.Cross(up, forward));
        float halfHeight = MathF.Tan(camera.VerticalFieldOfViewDegrees * (MathF.PI / 360.0f));
        float halfWidth = halfHeight * (ViewportWidth / (float)ViewportHeight);
        Vector3 direction = Vector3.Normalize(
            forward + (right * deviceX * halfWidth) + (up * deviceY * halfHeight));
        if (direction.Y >= -1.0e-3f)
        {
            return false;
        }

        float drop = (float)(bounds.Min.Y - position.Y);
        float candidate = drop / direction.Y;
        if (!float.IsFinite(candidate) || candidate <= 0.0f)
        {
            return false;
        }

        float hitX = (float)position.X + (direction.X * candidate);
        float hitZ = (float)position.Z + (direction.Z * candidate);
        if (hitX < (float)bounds.Min.X || hitX > (float)bounds.Max.X ||
            hitZ < (float)bounds.Min.Z || hitZ > (float)bounds.Max.Z)
        {
            return false;
        }

        distance = candidate;
        return true;
    }
    private static TerrainPatchWorldBounds UnionBounds(
        IReadOnlyList<TerrainPatchWorldBounds> bounds)
    {
        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;
        double maxZ = double.NegativeInfinity;
        foreach (TerrainPatchWorldBounds bound in bounds)
        {
            minX = Math.Min(minX, bound.Min.X);
            minY = Math.Min(minY, bound.Min.Y);
            minZ = Math.Min(minZ, bound.Min.Z);
            maxX = Math.Max(maxX, bound.Max.X);
            maxY = Math.Max(maxY, bound.Max.Y);
            maxZ = Math.Max(maxZ, bound.Max.Z);
        }

        return new TerrainPatchWorldBounds(
            new WorldPosition(minX, minY, minZ),
            new WorldPosition(maxX, maxY, maxZ));
    }

    private static WorldPosition Center(in TerrainPatchWorldBounds bounds) => new(
        (bounds.Min.X + bounds.Max.X) * 0.5,
        (bounds.Min.Y + bounds.Max.Y) * 0.5,
        (bounds.Min.Z + bounds.Max.Z) * 0.5);

    private static ShowcaseCamera ReadAuthoredShowcaseCamera()
    {
        string scenePath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.packagegame",
            "Assets",
            "Scenes",
            "LanternShowcaseScene.arisenscene");
        var stream = new YamlStream();
        using (var reader = new StreamReader(scenePath))
        {
            stream.Load(reader);
        }

        var root = Assert.IsType<YamlMappingNode>(stream.Documents[0].RootNode);
        var entities = Assert.IsType<YamlSequenceNode>(root.Children[new YamlScalarNode("Entities")]);
        foreach (YamlNode node in entities.Children)
        {
            var entity = Assert.IsType<YamlMappingNode>(node);
            if (ReadScalar(entity, "Name") != "Main Camera")
            {
                continue;
            }

            var transform = Assert.IsType<YamlMappingNode>(
                entity.Children[new YamlScalarNode("Transform")]);
            var position = Assert.IsType<YamlMappingNode>(
                transform.Children[new YamlScalarNode("Position")]);
            var rotation = Assert.IsType<YamlMappingNode>(
                transform.Children[new YamlScalarNode("Rotation")]);
            var cameraNode = Assert.IsType<YamlMappingNode>(
                entity.Children[new YamlScalarNode("Camera")]);
            return new ShowcaseCamera(
                new WorldPosition(
                    ReadNumber(position, "X"),
                    ReadNumber(position, "Y"),
                    ReadNumber(position, "Z")),
                Quaternion.Normalize(new Quaternion(
                    ReadNumber(rotation, "X"),
                    ReadNumber(rotation, "Y"),
                    ReadNumber(rotation, "Z"),
                    ReadNumber(rotation, "W"))),
                ReadNumber(cameraNode, "VerticalFov"),
                ReadNumber(cameraNode, "NearPlane"),
                ReadNumber(cameraNode, "FarPlane"));
        }

        throw new InvalidOperationException(
            "LanternShowcaseScene does not declare a 'Main Camera' entity.");
    }

    private static string ReadScalar(YamlMappingNode mapping, string key) =>
        Assert.IsType<YamlScalarNode>(mapping.Children[new YamlScalarNode(key)]).Value
        ?? throw new InvalidOperationException($"Scene node '{key}' has no value.");

    private static float ReadNumber(YamlMappingNode mapping, string key) =>
        float.Parse(ReadScalar(mapping, key), CultureInfo.InvariantCulture);

    private static string GetRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Arisen")))
            {
                return Path.Combine([directory.FullName, .. segments]);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private readonly record struct ShowcaseCamera(
        WorldPosition Position,
        Quaternion Rotation,
        float VerticalFieldOfViewDegrees,
        float NearPlane,
        float FarPlane);

    private static Vector3 ToVector3(WorldPosition position) => new(
        (float)position.X,
        (float)position.Y,
        (float)position.Z);
}

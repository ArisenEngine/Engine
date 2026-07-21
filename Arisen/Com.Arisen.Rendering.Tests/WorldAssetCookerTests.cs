using System.Buffers.Binary;
using ArisenEngine.Core.Assets;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class WorldAssetCookerTests
{
    [Fact]
    public void CellIdentityAndCookedBytes_AreIndependentOfEntryOrderAndSourcePath()
    {
        using var context = new WorldFixture();
        WorldDescriptorLoadResult forward = context.LoadSource(reverseCells: false);
        Assert.True(forward.Success, forward.Diagnostic);
        WorldCellId[] forwardIds = forward.Descriptor!.Cells.Select(cell => cell.Id).ToArray();

        CookedWorldArtifact first = context.Cook(reverseCells: false);
        byte[] firstBytes = File.ReadAllBytes(first.Path);
        CookedWorldArtifact reordered = context.Cook(reverseCells: true);
        byte[] reorderedBytes = File.ReadAllBytes(reordered.Path);

        string movedPath = Path.Combine(context.Root, "Moved", "Renamed.arisenworld");
        Directory.CreateDirectory(Path.GetDirectoryName(movedPath)!);
        File.Move(context.WorldPath, movedPath);
        context.Database.AddAsset(context.WorldGuid, "World", movedPath, WorldFixture.PackageId);
        File.WriteAllText(movedPath, context.CreateWorldSource(reverseCells: false));
        CookedWorldArtifact moved = WorldAssetCooker.Cook(context.Database, context.WorldRef);
        byte[] movedBytes = File.ReadAllBytes(moved.Path);

        Assert.Equal(firstBytes, reorderedBytes);
        Assert.Equal(firstBytes, movedBytes);
        Assert.Equal(forwardIds, context.LoadSource(reverseCells: false).Descriptor!.Cells.Select(cell => cell.Id));
        Assert.Equal(
            WorldCellIdentity.Create(context.WorldGuid, new WorldCellCoordinate(0, 0, 0), "surface"),
            WorldCellIdentity.Create(context.WorldGuid, new WorldCellCoordinate(0, 0, 0), "Surface"));
    }

    [Fact]
    public void CookedWorld_RoundTripsWithoutSourceAndClosesAllSceneDependencies()
    {
        using var context = new WorldFixture();
        CookedWorldArtifact cooked = context.Cook(reverseCells: true);

        WorldDescriptorLoadResult loaded = WorldAssetCooker.LoadCooked(context.Database, context.WorldRef);
        Assert.True(loaded.Success, loaded.Diagnostic);
        WorldDescriptor descriptor = loaded.Descriptor!;
        Assert.Equal(context.WorldGuid, descriptor.WorldGuid);
        Assert.Equal(2, descriptor.Cells.Count);
        Assert.Equal(2, descriptor.EntityReferences.Count);
        Assert.Equal(3, cooked.Dependencies.Count);
        Assert.All(descriptor.Cells, cell => Assert.Equal(32, cell.SceneContentHash.Length));
        Assert.All(descriptor.Cells, cell => Assert.True(cell.EstimatedCpuBytes >= cell.ScenePayloadBytes));
        Assert.All(descriptor.Cells, cell => Assert.Single(cell.Neighbors));
        WorldCellDescriptor dependentCell = Assert.Single(
            descriptor.Cells,
            cell => cell.Dependencies.Count == 1);
        Assert.Equal(
            WorldCellIdentity.Create(context.WorldGuid, new WorldCellCoordinate(0, 0, 0), "surface"),
            dependentCell.Dependencies[0]);

        var registry = new RuntimeAssetCookerRegistry();
        registry.RegisterCooker(new SceneRuntimeAssetCooker(context.Database));
        registry.RegisterCooker(new WorldRuntimeAssetCooker(context.Database));
        RuntimeAssetCookResult closure = RuntimeAssetCookCoordinator.Cook(
            new RuntimeAssetCookContext(
                context.Root,
                "Production",
                "Debug",
                "win-x64",
                Path.Combine(context.Root, "Staging"),
                ForceRebuild: false),
            [new RuntimeAssetCookRootRequest(
                "startupWorld",
                context.WorldGuid,
                WorldFixture.PackageId,
                "World")],
            registry);

        Assert.Equal(WorldAssetCooker.RuntimeVariant, Assert.Single(closure.Catalog.Roots).Variant);
        Assert.Equal(4, closure.Catalog.Artifacts.Count);
        RuntimeAssetCatalogArtifact worldArtifact = closure.Catalog.Artifacts.Single(
            artifact => artifact.AssetType == "World");
        Assert.Equal(3, worldArtifact.Dependencies.Count);
        Assert.All(worldArtifact.Dependencies, dependency =>
        {
            Assert.Equal("Scene", dependency.AssetType);
            Assert.Equal(SceneAssetCooker.RuntimeVariant, dependency.Variant);
            Assert.True(dependency.Required);
        });

        context.Database.UseReadOnlyRuntime();
        WorldDescriptorLoadResult cookedOnly = WorldAssetCooker.LoadCooked(context.Database, context.WorldRef);
        Assert.True(cookedOnly.Success, cookedOnly.Diagnostic);
        Assert.Equal(descriptor.Cells.Select(cell => cell.Id), cookedOnly.Descriptor!.Cells.Select(cell => cell.Id));
    }

    [Theory]
    [InlineData(WorldInvalidCase.OverlappingBounds, "overlapping")]
    [InlineData(WorldInvalidCase.DuplicateCell, "duplicate cell identity")]
    [InlineData(WorldInvalidCase.UndeclaredDependency, "undeclared dependency")]
    [InlineData(WorldInvalidCase.DependencyCycle, "cyclic cells")]
    [InlineData(WorldInvalidCase.UnresolvedEntity, "undeclared entity")]
    public void InvalidWorldData_FailsBeforeCooking(
        WorldInvalidCase invalidCase,
        string expectedDiagnostic)
    {
        using var context = new WorldFixture();
        File.WriteAllText(context.WorldPath, context.CreateWorldSource(invalidCase: invalidCase));

        WorldDescriptorLoadResult loaded = WorldDescriptorLoader.LoadSource(context.Database, context.WorldRef);

        Assert.False(loaded.Success);
        Assert.Null(loaded.Descriptor);
        Assert.Contains(expectedDiagnostic, loaded.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => WorldAssetCooker.Cook(context.Database, context.WorldRef));
        Assert.False(context.Database.TryGetCookedArtifact(
            context.WorldGuid,
            WorldAssetCooker.RuntimeVariant,
            out _));
    }

    [Fact]
    public void CorruptTruncatedAndWrongVersionPayloads_FailClosed()
    {
        using var context = new WorldFixture();
        byte[] valid = File.ReadAllBytes(context.Cook(reverseCells: false).Path);

        byte[] corrupt = (byte[])valid.Clone();
        corrupt[^1] ^= 0x40;
        WorldDescriptorLoadResult corruptResult = WorldAssetCooker.TryReadPayload(
            context.WorldGuid,
            corrupt,
            "corrupt.ariworld");
        Assert.False(corruptResult.Success);
        Assert.Contains("SHA-256", corruptResult.Diagnostic, StringComparison.OrdinalIgnoreCase);

        byte[] wrongVersion = (byte[])valid.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(wrongVersion.AsSpan(12, 4), 999);
        WorldDescriptorLoadResult versionResult = WorldAssetCooker.TryReadPayload(
            context.WorldGuid,
            wrongVersion,
            "wrong-version.ariworld");
        Assert.False(versionResult.Success);
        Assert.Contains("header", versionResult.Diagnostic, StringComparison.OrdinalIgnoreCase);

        WorldDescriptorLoadResult truncated = WorldAssetCooker.TryReadPayload(
            context.WorldGuid,
            valid.AsSpan(0, valid.Length - 7),
            "truncated.ariworld");
        Assert.False(truncated.Success);
        Assert.Null(truncated.Descriptor);
    }

    public enum WorldInvalidCase
    {
        None,
        OverlappingBounds,
        DuplicateCell,
        UndeclaredDependency,
        DependencyCycle,
        UnresolvedEntity
    }

    private sealed class WorldFixture : IDisposable
    {
        public const string PackageId = "com.arisen.test";

        private static readonly Guid s_PersistentSceneGuid = Guid.Parse("82000000-0000-0000-0000-000000000001");
        private static readonly Guid s_FirstSceneGuid = Guid.Parse("82000000-0000-0000-0000-000000000002");
        private static readonly Guid s_SecondSceneGuid = Guid.Parse("82000000-0000-0000-0000-000000000003");
        private static readonly Guid s_PersistentEntityGuid = Guid.Parse("82100000-0000-0000-0000-000000000001");
        private static readonly Guid s_FirstEntityGuid = Guid.Parse("82100000-0000-0000-0000-000000000002");
        private static readonly Guid s_SecondEntityGuid = Guid.Parse("82100000-0000-0000-0000-000000000003");

        public WorldFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "ArisenWorldAssetTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Database = new TestAssetDatabase(AssetSourceAccessMode.RuntimeAssetCook, Path.Combine(Root, "Cooked"));
            AddScene("Persistent", s_PersistentSceneGuid, s_PersistentEntityGuid);
            AddScene("First", s_FirstSceneGuid, s_FirstEntityGuid);
            AddScene("Second", s_SecondSceneGuid, s_SecondEntityGuid);
            WorldGuid = Guid.Parse("82000000-0000-0000-0000-000000000010");
            WorldPath = Path.Combine(Root, "Worlds", "TestWorld.arisenworld");
            Directory.CreateDirectory(Path.GetDirectoryName(WorldPath)!);
            File.WriteAllText(WorldPath, CreateWorldSource());
            Database.AddAsset(WorldGuid, "World", WorldPath, PackageId);
            WorldRef = new AssetRef<WorldSourceAsset>(WorldGuid, "World", PackageId);
        }

        public string Root { get; }
        public TestAssetDatabase Database { get; }
        public Guid WorldGuid { get; }
        public string WorldPath { get; private set; }
        public AssetRef<WorldSourceAsset> WorldRef { get; }

        public WorldDescriptorLoadResult LoadSource(bool reverseCells)
        {
            File.WriteAllText(WorldPath, CreateWorldSource(reverseCells));
            return WorldDescriptorLoader.LoadSource(Database, WorldRef);
        }

        public CookedWorldArtifact Cook(bool reverseCells)
        {
            File.WriteAllText(WorldPath, CreateWorldSource(reverseCells));
            return WorldAssetCooker.Cook(Database, WorldRef);
        }

        public string CreateWorldSource(
            bool reverseCells = false,
            WorldInvalidCase invalidCase = WorldInvalidCase.None)
        {
            string firstBoundsMax = invalidCase == WorldInvalidCase.OverlappingBounds ? "150" : "100";
            string secondCoordinate = invalidCase == WorldInvalidCase.DuplicateCell
                ? "{ X: 0, Y: 0, Z: 0 }"
                : "{ X: 1, Y: 0, Z: 0 }";
            string secondDependency = invalidCase switch
            {
                WorldInvalidCase.UndeclaredDependency => """
                  Dependencies:
                  - Coordinate: { X: 99, Y: 0, Z: 0 }
                    Layer: surface
                  """,
                WorldInvalidCase.DependencyCycle => """
                  Dependencies:
                  - Coordinate: { X: 0, Y: 0, Z: 0 }
                    Layer: surface
                  """,
                _ => string.Empty
            };
            string firstDependency = invalidCase == WorldInvalidCase.DependencyCycle
                ? """
                  Dependencies:
                  - Coordinate: { X: 1, Y: 0, Z: 0 }
                    Layer: surface
                  """
                : string.Empty;
            Guid targetEntity = invalidCase == WorldInvalidCase.UnresolvedEntity
                ? Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")
                : s_FirstEntityGuid;
            string first = $$"""
                - Coordinate: { X: 0, Y: 0, Z: 0 }
                  Layer: Surface
                  Scene:
                    Guid: {{s_FirstSceneGuid:D}}
                    PackageId: {{PackageId}}
                  Bounds:
                    Min: { X: 0, Y: 0, Z: 0 }
                    Max: { X: {{firstBoundsMax}}, Y: 100, Z: 100 }
                  EstimatedCpuBytes: 128
                  EstimatedGpuBytes: 256
                {{Indent(firstDependency, 2)}}  References:
                  - SourceEntityGuid: {{s_FirstEntityGuid:D}}
                    Target:
                      Scope: Persistent
                      EntityGuid: {{s_PersistentEntityGuid:D}}
                    Required: false
                """;
            string second = $$"""
                - Coordinate: {{secondCoordinate}}
                  Layer: surface
                  Scene:
                    Guid: {{s_SecondSceneGuid:D}}
                    PackageId: {{PackageId}}
                  Bounds:
                    Min: { X: 100, Y: 0, Z: 0 }
                    Max: { X: 200, Y: 100, Z: 100 }
                  EstimatedCpuBytes: 256
                  EstimatedGpuBytes: 512
                {{Indent(secondDependency, 2)}}  References:
                  - SourceEntityGuid: {{s_SecondEntityGuid:D}}
                    Target:
                      Scope: Cell
                      Coordinate: { X: 0, Y: 0, Z: 0 }
                      Layer: surface
                      EntityGuid: {{targetEntity:D}}
                    Required: true
                """;
            string cells = reverseCells ? second + Environment.NewLine + first : first + Environment.NewLine + second;
            return $$"""
                Version: 1
                WorldGuid: {{WorldGuid:D}}
                Name: Deterministic Test World
                PersistentScene:
                  Guid: {{s_PersistentSceneGuid:D}}
                  PackageId: {{PackageId}}
                Partition:
                  Origin: { X: 0, Y: 0, Z: 0 }
                  CellSize: { X: 100, Y: 100, Z: 100 }
                  LoadRadius: 2
                  UnloadHysteresis: 1
                  MaxActiveCells: 16
                Policy:
                  UnresolvedReferences: KeepUnresolved
                  UnloadedTargets: ClearAndLateResolve
                  DependencyCycles: Reject
                Layers:
                - Id: surface
                  Priority: 0
                Cells:
                {{Indent(cells, 0)}}
                """;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        private void AddScene(string name, Guid sceneGuid, Guid entityGuid)
        {
            string path = Path.Combine(Root, "Scenes", name + ".arisenscene");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $$"""
                Version: 2
                Name: {{name}}
                ComponentSchemas:
                - TypeId: 1
                  Name: Transform
                  Version: 1
                  Required: true
                Entities:
                - Guid: {{entityGuid:D}}
                  Name: {{name}} Entity
                  Transform:
                    Position: { X: 0, Y: 0, Z: 0 }
                    Rotation: { X: 0, Y: 0, Z: 0, W: 1 }
                    Scale: { X: 1, Y: 1, Z: 1 }
                """);
            Database.AddAsset(sceneGuid, "Scene", path, PackageId);
        }

        private static string Indent(string value, int spaces)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string prefix = new(' ', spaces);
            return string.Join(
                Environment.NewLine,
                value.Replace("\r\n", "\n", StringComparison.Ordinal)
                    .TrimEnd('\n')
                    .Split('\n')
                    .Select(line => prefix + line)) + Environment.NewLine;
        }
    }
}

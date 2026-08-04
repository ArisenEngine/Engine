using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

[Collection(SceneComponentExtensionRegistryCollection.Name)]
public sealed class VegetationSceneComponentTests : IDisposable
{
    private readonly VegetationClusterSceneComponentCodec m_Codec = new();

    public VegetationSceneComponentTests()
    {
        SceneComponentExtensionRegistry.Shared.Register(m_Codec);
    }

    public void Dispose()
    {
        SceneComponentExtensionRegistry.Shared.Unregister(m_Codec);
    }

    [Fact]
    public void VegetationClusterComponent_RoundTripsWithExactFlattenedClosure()
    {
        using var fixture = new VegetationSceneFixture();
        SceneFixture scene = fixture.AddScene("VegetationCell");
        SceneStagingData staging = fixture.BuildStaging(scene);
        object stagedValue = Assert.Single(
            Assert.Single(staging.Entities).ExtensionComponents!).Value;
        var readContext = new SceneComponentReadContext(
            fixture.Database,
            scene.Reference.Guid,
            scene.EntityGuid,
            scene.SourcePath);

        byte[] firstComponentBytes = m_Codec.WriteCooked(stagedValue);
        Assert.True(m_Codec.TryReadCooked(
            readContext,
            firstComponentBytes,
            out object cookedValue,
            out string componentDiagnostic), componentDiagnostic);
        Assert.Equal(firstComponentBytes, m_Codec.WriteCooked(cookedValue));

        var sourceWorld = new EntityManager();
        SceneLoadResult sourceLoad = SceneAssetLoader.LoadScene(
            fixture.Database,
            scene.Reference,
            sourceWorld);
        Assert.True(sourceLoad.Success, sourceLoad.Diagnostic);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<VegetationClusterComponent>());
        Assert.True(scene.TryGetComponent(
            sourceWorld,
            sourceLoad,
            out VegetationClusterComponent source));
        AssertComponent(source, fixture, scene);

        CookedSceneArtifact first = SceneAssetCooker.Cook(
            fixture.Database,
            scene.Reference);
        byte[] firstSceneBytes = File.ReadAllBytes(first.Path);
        CookedSceneArtifact second = SceneAssetCooker.Cook(
            fixture.Database,
            scene.Reference);
        Assert.Equal(firstSceneBytes, File.ReadAllBytes(second.Path));

        CookedSceneDependency[] expectedDependencies =
        [
            new(
                fixture.ClusterGuid,
                VegetationSceneFixture.PackageId,
                VegetationAssetTypes.Cluster,
                true,
                VegetationClusterAssetCooker.RuntimeVariant),
            new(
                fixture.BiomeGuid,
                VegetationSceneFixture.PackageId,
                VegetationAssetTypes.Biome,
                true,
                VegetationBiomeAssetCooker.RuntimeVariant),
            new(
                fixture.SpeciesGuid,
                VegetationSceneFixture.PackageId,
                VegetationAssetTypes.Species,
                true,
                VegetationSpeciesAssetCooker.RuntimeVariant),
            new(
                fixture.SecondSpeciesGuid,
                VegetationSceneFixture.PackageId,
                VegetationAssetTypes.Species,
                true,
                VegetationSpeciesAssetCooker.RuntimeVariant),
            new(
                fixture.PageGuid,
                VegetationSceneFixture.PackageId,
                VegetationAssetTypes.InstancePage,
                true,
                VegetationInstancePageAssetCooker.RuntimeVariant),
            new(
                fixture.MeshGuid,
                VegetationSceneFixture.PackageId,
                "Mesh",
                true,
                "staticmesh.uint32"),
            new(
                fixture.MaterialGuid,
                VegetationSceneFixture.PackageId,
                "Material",
                true,
                "material.runtime"),
            new(
                fixture.SecondMeshGuid,
                VegetationSceneFixture.PackageId,
                "Mesh",
                true,
                "staticmesh.uint32"),
            new(
                fixture.SecondMaterialGuid,
                VegetationSceneFixture.PackageId,
                "Material",
                true,
                "material.runtime")
        ];
        expectedDependencies = expectedDependencies
            .OrderBy(dependency => dependency.Guid)
            .ThenBy(dependency => dependency.PackageId, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.AssetType, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.Variant, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedDependencies, first.Dependencies);

        fixture.Database.UseReadOnlyRuntime();
        var cookedWorld = new EntityManager();
        SceneLoadResult cookedLoad = SceneAssetCooker.LoadCooked(
            fixture.Database,
            scene.Reference,
            cookedWorld);
        Assert.True(cookedLoad.Success, cookedLoad.Diagnostic);
        Assert.True(scene.TryGetComponent(
            cookedWorld,
            cookedLoad,
            out VegetationClusterComponent cooked));
        Assert.Equal(source, cooked);
    }

    [Fact]
    public void SceneRootCookCoordinator_CooksInitiallyUncookedBiomeSpeciesClosure()
    {
        using var fixture = new VegetationSceneFixture(
            cookPrimarySpecies: false,
            cookSecondarySpecies: false,
            cookBiome: false);
        SceneFixture scene = fixture.AddScene("CleanCookVegetationCell");
        Assert.False(fixture.Database.TryGetCookedArtifact(
            scene.Reference.Guid,
            SceneAssetCooker.RuntimeVariant,
            out _));
        Assert.False(fixture.Database.TryGetCookedArtifact(
            fixture.BiomeGuid,
            VegetationBiomeAssetCooker.RuntimeVariant,
            out _));
        Assert.False(fixture.Database.TryGetCookedArtifact(
            fixture.SpeciesGuid,
            VegetationSpeciesAssetCooker.RuntimeVariant,
            out _));
        Assert.False(fixture.Database.TryGetCookedArtifact(
            fixture.SecondSpeciesGuid,
            VegetationSpeciesAssetCooker.RuntimeVariant,
            out _));
        Assert.True(fixture.Database.TryGetCookedArtifact(
            fixture.ClusterGuid,
            VegetationClusterAssetCooker.RuntimeVariant,
            out _));
        Assert.True(fixture.Database.TryGetCookedArtifact(
            fixture.PageGuid,
            VegetationInstancePageAssetCooker.RuntimeVariant,
            out _));

        var renderingDependencies = new DeterministicRenderingDependencyCooker(fixture.Root);
        var registry = new RuntimeAssetCookerRegistry();
        registry.RegisterCooker(new SceneRuntimeAssetCooker(fixture.Database));
        registry.RegisterCooker(new VegetationRuntimeAssetCooker(fixture.Database));
        registry.RegisterCooker(renderingDependencies);
        var context = new RuntimeAssetCookContext(
            fixture.Root,
            "Production",
            "Release",
            "win-x64",
            Path.Combine(fixture.Root, "Staging"),
            ForceRebuild: false);

        RuntimeAssetCookResult result = RuntimeAssetCookCoordinator.Cook(
            context,
            [
                new RuntimeAssetCookRootRequest(
                    "vegetationScene",
                    scene.Reference.Guid,
                    VegetationSceneFixture.PackageId,
                    "Scene")
            ],
            registry);

        Assert.True(fixture.Database.TryGetCookedArtifact(
            scene.Reference.Guid,
            SceneAssetCooker.RuntimeVariant,
            out _));
        Assert.True(fixture.Database.TryGetCookedArtifact(
            fixture.BiomeGuid,
            VegetationBiomeAssetCooker.RuntimeVariant,
            out _));
        Assert.True(fixture.Database.TryGetCookedArtifact(
            fixture.SpeciesGuid,
            VegetationSpeciesAssetCooker.RuntimeVariant,
            out _));
        Assert.True(fixture.Database.TryGetCookedArtifact(
            fixture.SecondSpeciesGuid,
            VegetationSpeciesAssetCooker.RuntimeVariant,
            out _));
        Assert.Equal(10, result.Catalog.Artifacts.Count);
        Assert.Equal(10, result.Files.Count);

        RuntimeAssetCatalogArtifact sceneArtifact = FindArtifact(
            result.Catalog,
            scene.Reference.Guid,
            SceneAssetCooker.RuntimeVariant);
        RuntimeAssetCatalogArtifact clusterArtifact = FindArtifact(
            result.Catalog,
            fixture.ClusterGuid,
            VegetationClusterAssetCooker.RuntimeVariant);
        RuntimeAssetCatalogArtifact biomeArtifact = FindArtifact(
            result.Catalog,
            fixture.BiomeGuid,
            VegetationBiomeAssetCooker.RuntimeVariant);
        RuntimeAssetCatalogArtifact primarySpeciesArtifact = FindArtifact(
            result.Catalog,
            fixture.SpeciesGuid,
            VegetationSpeciesAssetCooker.RuntimeVariant);
        RuntimeAssetCatalogArtifact secondarySpeciesArtifact = FindArtifact(
            result.Catalog,
            fixture.SecondSpeciesGuid,
            VegetationSpeciesAssetCooker.RuntimeVariant);
        RuntimeAssetCatalogArtifact pageArtifact = FindArtifact(
            result.Catalog,
            fixture.PageGuid,
            VegetationInstancePageAssetCooker.RuntimeVariant);
        RuntimeAssetCatalogArtifact primaryMeshArtifact = FindArtifact(
            result.Catalog,
            fixture.MeshGuid,
            "staticmesh.uint32");
        RuntimeAssetCatalogArtifact secondaryMeshArtifact = FindArtifact(
            result.Catalog,
            fixture.SecondMeshGuid,
            "staticmesh.uint32");
        RuntimeAssetCatalogArtifact primaryMaterialArtifact = FindArtifact(
            result.Catalog,
            fixture.MaterialGuid,
            "material.runtime");
        RuntimeAssetCatalogArtifact secondaryMaterialArtifact = FindArtifact(
            result.Catalog,
            fixture.SecondMaterialGuid,
            "material.runtime");

        AssertDependencies(
            sceneArtifact,
            (fixture.ClusterGuid, VegetationAssetTypes.Cluster, VegetationClusterAssetCooker.RuntimeVariant),
            (fixture.BiomeGuid, VegetationAssetTypes.Biome, VegetationBiomeAssetCooker.RuntimeVariant),
            (fixture.SpeciesGuid, VegetationAssetTypes.Species, VegetationSpeciesAssetCooker.RuntimeVariant),
            (fixture.SecondSpeciesGuid, VegetationAssetTypes.Species, VegetationSpeciesAssetCooker.RuntimeVariant),
            (fixture.PageGuid, VegetationAssetTypes.InstancePage, VegetationInstancePageAssetCooker.RuntimeVariant),
            (fixture.MeshGuid, "Mesh", "staticmesh.uint32"),
            (fixture.SecondMeshGuid, "Mesh", "staticmesh.uint32"),
            (fixture.MaterialGuid, "Material", "material.runtime"),
            (fixture.SecondMaterialGuid, "Material", "material.runtime"));
        AssertDependencies(
            clusterArtifact,
            (fixture.BiomeGuid, VegetationAssetTypes.Biome, VegetationBiomeAssetCooker.RuntimeVariant),
            (fixture.SpeciesGuid, VegetationAssetTypes.Species, VegetationSpeciesAssetCooker.RuntimeVariant),
            (fixture.PageGuid, VegetationAssetTypes.InstancePage, VegetationInstancePageAssetCooker.RuntimeVariant));
        AssertDependencies(
            biomeArtifact,
            (fixture.SpeciesGuid, VegetationAssetTypes.Species, VegetationSpeciesAssetCooker.RuntimeVariant),
            (fixture.SecondSpeciesGuid, VegetationAssetTypes.Species, VegetationSpeciesAssetCooker.RuntimeVariant));
        AssertDependencies(
            pageArtifact,
            (fixture.SpeciesGuid, VegetationAssetTypes.Species, VegetationSpeciesAssetCooker.RuntimeVariant));
        AssertDependencies(
            primarySpeciesArtifact,
            (fixture.MeshGuid, "Mesh", "staticmesh.uint32"),
            (fixture.MaterialGuid, "Material", "material.runtime"));
        AssertDependencies(
            secondarySpeciesArtifact,
            (fixture.SecondMeshGuid, "Mesh", "staticmesh.uint32"),
            (fixture.SecondMaterialGuid, "Material", "material.runtime"));
        Assert.Empty(primaryMeshArtifact.Dependencies);
        Assert.Empty(secondaryMeshArtifact.Dependencies);
        Assert.Empty(primaryMaterialArtifact.Dependencies);
        Assert.Empty(secondaryMaterialArtifact.Dependencies);
        Assert.Contains(
            renderingDependencies.Requests,
            request => request.Guid == fixture.SecondMeshGuid);
        Assert.Contains(
            renderingDependencies.Requests,
            request => request.Guid == fixture.SecondMaterialGuid);
        Assert.Equal(4, renderingDependencies.Requests.Select(request => request.Guid).Distinct().Count());
    }

    [Fact]
    public void CookedVegetationComponent_RejectsMissingRequiredProviderCodec()
    {
        using var fixture = new VegetationSceneFixture();
        SceneFixture scene = fixture.AddScene("MissingVegetationCodec");
        SceneAssetCooker.Cook(fixture.Database, scene.Reference);
        fixture.Database.UseReadOnlyRuntime();
        Assert.True(SceneComponentExtensionRegistry.Shared.Unregister(m_Codec));

        try
        {
            var world = new EntityManager();
            SceneLoadResult result = SceneAssetCooker.LoadCooked(
                fixture.Database,
                scene.Reference,
                world);

            Assert.False(result.Success);
            Assert.Empty(world.GetAllEntities());
            Assert.Contains(
                $"required component TypeId '{VegetationClusterSceneComponentCodec.TypeId}' is unknown",
                result.Diagnostic,
                StringComparison.Ordinal);
        }
        finally
        {
            SceneComponentExtensionRegistry.Shared.Register(m_Codec);
        }
    }

    [Fact]
    public void VegetationClusterComponent_RejectsMalformedCookedPayloads()
    {
        using var fixture = new VegetationSceneFixture();
        SceneFixture scene = fixture.AddScene("MalformedVegetation");
        SceneStagingData staging = fixture.BuildStaging(scene);
        object value = Assert.Single(
            Assert.Single(staging.Entities).ExtensionComponents!).Value;
        byte[] valid = m_Codec.WriteCooked(value);
        var context = new SceneComponentReadContext(
            fixture.Database,
            scene.Reference.Guid,
            scene.EntityGuid,
            scene.SourcePath);

        var malformed = new List<byte[]>
        {
            valid[..^1],
            valid.Append((byte)0).ToArray(),
            Mutate(valid, bytes => bytes[92] = 1),
            Mutate(valid, bytes => bytes.AsSpan(196, sizeof(uint)).Clear()),
            Mutate(valid, bytes => BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(168),
                0x80000000U)),
            Mutate(valid, bytes => bytes[^1] = 0xff)
        };

        foreach (byte[] payload in malformed)
        {
            Assert.False(m_Codec.TryReadCooked(
                context,
                payload,
                out _,
                out string diagnostic));
            Assert.Contains("VegetationSceneComponent", diagnostic, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void VegetationClusterComponent_RejectsMismatchedAuthoredClosureAndCellIdentity()
    {
        using var fixture = new VegetationSceneFixture();
        SourceFault[] faults =
        [
            SourceFault.WrongCluster,
            SourceFault.WrongBiome,
            SourceFault.WrongSpecies,
            SourceFault.WrongClusterPackage,
            SourceFault.WrongPageCount,
            SourceFault.WrongInstanceCount,
            SourceFault.WrongOrigin,
            SourceFault.WrongBounds,
            SourceFault.WrongWorld,
            SourceFault.WrongCoordinate,
            SourceFault.WrongLayer,
            SourceFault.WrongOwningCell,
            SourceFault.OutOfRangeCoordinate
        ];

        foreach (SourceFault fault in faults)
        {
            SceneFixture scene = fixture.AddScene($"Fault{fault}", fault: fault);
            var world = new EntityManager();
            SceneLoadResult result = SceneAssetLoader.LoadScene(
                fixture.Database,
                scene.Reference,
                world);

            Assert.False(result.Success);
            Assert.Empty(world.GetAllEntities());
            Assert.Contains("VegetationSceneComponent", result.Diagnostic, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(ClosureShape.MultipleSpecies)]
    [InlineData(ClosureShape.MixedPageOrigins)]
    public void VegetationClusterComponent_RejectsUnsupportedCookedClusterShape(
        ClosureShape shape)
    {
        using var fixture = new VegetationSceneFixture(shape);
        SceneFixture scene = fixture.AddScene($"Closure{shape}");
        var world = new EntityManager();

        SceneLoadResult result = SceneAssetLoader.LoadScene(
            fixture.Database,
            scene.Reference,
            world);

        Assert.False(result.Success);
        Assert.Empty(world.GetAllEntities());
        Assert.Contains(
            shape == ClosureShape.MultipleSpecies
                ? "exactly one canonical species"
                : "share the component's exact canonical origin",
            result.Diagnostic,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VegetationClusterComponent_RejectsSpeciesOutsideAuthoredBiome()
    {
        using var fixture = new VegetationSceneFixture();
        fixture.ReplaceBiomeWithSecondarySpeciesOnly();
        SceneFixture scene = fixture.AddScene("SpeciesOutsideBiome");
        var world = new EntityManager();

        SceneLoadResult result = SceneAssetLoader.LoadScene(
            fixture.Database,
            scene.Reference,
            world);

        Assert.False(result.Success);
        Assert.Empty(world.GetAllEntities());
        Assert.Contains(
            "is not declared by authored biome",
            result.Diagnostic,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SourceStagingRejectsAuthoredBiomeDriftBeforeBiomeRecook()
    {
        using var fixture = new VegetationSceneFixture();
        fixture.ReplaceBiomeSourceWithSecondarySpeciesOnly();
        Assert.True(VegetationBiomeAssetCooker.TryLoadCooked(
            fixture.Database,
            new AssetRef<VegetationBiomeSourceAsset>(
                fixture.BiomeGuid,
                VegetationAssetTypes.Biome,
                VegetationSceneFixture.PackageId),
            out CookedVegetationBiome staleCookedBiome,
            out string biomeDiagnostic), biomeDiagnostic);
        Assert.Contains(
            staleCookedBiome.Entries,
            entry => entry.Species.Guid == fixture.SpeciesGuid);
        SceneFixture scene = fixture.AddScene("AuthoredBiomeDrift");

        SceneLoadResult result = SceneAssetLoader.LoadScene(
            fixture.Database,
            scene.Reference,
            new EntityManager());

        Assert.False(result.Success);
        Assert.Contains(
            "is not declared by authored biome",
            result.Diagnostic,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CookedComponentRequiresEveryCookedBiomeSpeciesWithoutSourceFallback()
    {
        using var fixture = new VegetationSceneFixture();
        SceneFixture scene = fixture.AddScene("CookedSpeciesClosure");
        SceneStagingData staging = fixture.BuildStaging(scene);
        object sourceValue = Assert.Single(
            Assert.Single(staging.Entities).ExtensionComponents!).Value;
        byte[] payload = m_Codec.WriteCooked(sourceValue);
        fixture.Database.InvalidateCookedAssets(
            fixture.SecondSpeciesGuid,
            VegetationSpeciesAssetCooker.RuntimeVariant);
        fixture.Database.UseReadOnlyRuntime();
        var context = new SceneComponentReadContext(
            fixture.Database,
            scene.Reference.Guid,
            scene.EntityGuid,
            scene.SourcePath);

        Assert.False(m_Codec.TryReadCooked(
            context,
            payload,
            out _,
            out string diagnostic));
        Assert.Contains(
            fixture.SecondSpeciesGuid.ToString("D"),
            diagnostic,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "is unavailable",
            diagnostic,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VegetationClusterOwnership_RejectsDuplicatesAndReleasesOnUnload()
    {
        using var fixture = new VegetationSceneFixture();
        SceneFixture duplicateScene = fixture.AddScene(
            "DuplicateInScene",
            entityCount: 2);
        var duplicateWorld = new EntityManager();
        SceneLoadResult duplicateLoad = SceneAssetLoader.LoadScene(
            fixture.Database,
            duplicateScene.Reference,
            duplicateWorld);
        Assert.False(duplicateLoad.Success);
        Assert.Empty(duplicateWorld.GetAllEntities());
        Assert.Contains("exclusive identity", duplicateLoad.Diagnostic, StringComparison.Ordinal);

        SceneFixture firstScene = fixture.AddScene("FirstVegetationCell");
        var secondCoordinate = new WorldCellCoordinate(
            fixture.CellCoordinate.X + 1,
            fixture.CellCoordinate.Y,
            fixture.CellCoordinate.Z);
        SceneFixture secondScene = fixture.AddScene(
            "SecondVegetationCell",
            coordinate: secondCoordinate);
        SceneStagingData firstStaging = fixture.BuildStaging(firstScene);
        SceneStagingData secondStaging = fixture.BuildStaging(secondScene);
        var world = new EntityManager();
        var service = new RuntimeSceneService(fixture.Database, world);
        WorldPosition renderOrigin = new(0.0, 0.0, 0.0);

        var first = service.ActivatePreparedAdditiveAtFrameBoundary(
            firstScene.Reference,
            SceneStagingPlacement.PlaceCell(firstStaging, fixture.Origin, renderOrigin),
            "test-source",
            new SceneComponentActivationContext(
                firstScene.CellId,
                fixture.Origin,
                fixture.CellBounds));
        Assert.True(first.Result.Success, first.Result.Diagnostic);
        Assert.Equal(1, world.EntityCount);

        var duplicate = service.ActivatePreparedAdditiveAtFrameBoundary(
            secondScene.Reference,
            SceneStagingPlacement.PlaceCell(secondStaging, fixture.Origin, renderOrigin),
            "test-source",
            new SceneComponentActivationContext(
                secondScene.CellId,
                fixture.Origin,
                fixture.CellBounds));
        Assert.False(duplicate.Result.Success);
        Assert.Equal(1, world.EntityCount);
        Assert.Contains("already active", duplicate.Result.Diagnostic, StringComparison.Ordinal);

        Assert.True(service.UnloadSceneAtFrameBoundary(
            first.InstanceId,
            out string unloadDiagnostic), unloadDiagnostic);
        Assert.Equal(0, world.EntityCount);

        var reloaded = service.ActivatePreparedAdditiveAtFrameBoundary(
            secondScene.Reference,
            SceneStagingPlacement.PlaceCell(secondStaging, fixture.Origin, renderOrigin),
            "test-source",
            new SceneComponentActivationContext(
                secondScene.CellId,
                fixture.Origin,
                fixture.CellBounds));
        Assert.True(reloaded.Result.Success, reloaded.Result.Diagnostic);
        Assert.Equal(1, world.EntityCount);
        Assert.True(service.TryResolveEntity(
            reloaded.InstanceId,
            secondScene.EntityGuid,
            out Entity entity));
        Assert.True(service.TryGetEntityWorldCellOwner(entity, out WorldCellId owner));
        Assert.Equal(secondScene.CellId, owner);
    }

    [Theory]
    [InlineData(ActivationFault.WrongCell)]
    [InlineData(ActivationFault.WrongOrigin)]
    [InlineData(ActivationFault.NonIntersectingBounds)]
    [InlineData(ActivationFault.PersistentScene)]
    public void VegetationClusterActivation_RejectsPlacementBeforeEcsMutation(
        ActivationFault fault)
    {
        using var fixture = new VegetationSceneFixture();
        SceneFixture scene = fixture.AddScene($"Activation{fault}");
        SceneStagingData staging = fixture.BuildStaging(scene);
        var world = new EntityManager();
        var service = new RuntimeSceneService(fixture.Database, world);
        if (fault == ActivationFault.PersistentScene)
        {
            SceneLoadResult persistent = service.LoadScene(scene.Reference);
            Assert.False(persistent.Success);
            Assert.Equal(0, world.EntityCount);
            Assert.Contains(
                "require a world-cell activation context",
                persistent.Diagnostic,
                StringComparison.OrdinalIgnoreCase);
            return;
        }

        WorldCellId cellId = fault == ActivationFault.WrongCell
            ? new WorldCellId(Guid.Parse("ffffffff-eeee-dddd-cccc-bbbbbbbbbbbb"))
            : scene.CellId;
        WorldPosition origin = fault == ActivationFault.WrongOrigin
            ? new WorldPosition(fixture.Origin.X + 1.0, fixture.Origin.Y, fixture.Origin.Z)
            : fixture.Origin;
        WorldBounds bounds = fault == ActivationFault.NonIntersectingBounds
            ? new WorldBounds(
                new WorldPosition(0.0, 0.0, 0.0),
                new WorldPosition(16.0, 16.0, 16.0))
            : fixture.CellBounds;

        var activation = service.ActivatePreparedAdditiveAtFrameBoundary(
            scene.Reference,
            SceneStagingPlacement.PlaceCell(
                staging,
                fixture.Origin,
                new WorldPosition(0.0, 0.0, 0.0)),
            "test-source",
            new SceneComponentActivationContext(cellId, origin, bounds));

        Assert.False(activation.Result.Success);
        Assert.Equal(0, world.EntityCount);
        Assert.Contains(
            fault switch
            {
                ActivationFault.WrongCell => "owning cell",
                ActivationFault.WrongOrigin => "origin",
                _ => "bounds"
            },
            activation.Result.Diagnostic,
            StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] Mutate(byte[] source, Action<byte[]> mutation)
    {
        byte[] copy = source.ToArray();
        mutation(copy);
        return copy;
    }

    private static void AssertComponent(
        VegetationClusterComponent component,
        VegetationSceneFixture fixture,
        SceneFixture scene)
    {
        Assert.Equal(fixture.ClusterGuid, component.ClusterGuid);
        Assert.Equal(fixture.BiomeGuid, component.BiomeGuid);
        Assert.Equal(fixture.SpeciesGuid, component.SpeciesGuid);
        Assert.Equal(fixture.WorldGuid, component.WorldGuid);
        Assert.Equal(scene.CellId.Value, component.OwningCellGuid);
        Assert.Equal(scene.Coordinate.X, component.CellX);
        Assert.Equal(scene.Coordinate.Y, component.CellY);
        Assert.Equal(scene.Coordinate.Z, component.CellZ);
        Assert.Equal(fixture.Origin.X, component.OriginX);
        Assert.Equal(fixture.Origin.Y, component.OriginY);
        Assert.Equal(fixture.Origin.Z, component.OriginZ);
        Assert.Equal(fixture.ClusterBounds.Min.X, component.BoundsMinX);
        Assert.Equal(fixture.ClusterBounds.Min.Y, component.BoundsMinY);
        Assert.Equal(fixture.ClusterBounds.Min.Z, component.BoundsMinZ);
        Assert.Equal(fixture.ClusterBounds.Max.X, component.BoundsMaxX);
        Assert.Equal(fixture.ClusterBounds.Max.Y, component.BoundsMaxY);
        Assert.Equal(fixture.ClusterBounds.Max.Z, component.BoundsMaxZ);
        Assert.Equal(
            VegetationClusterFlags.Visible |
            VegetationClusterFlags.CastShadows |
            VegetationClusterFlags.ReceiveShadows,
            component.Flags);
        Assert.Equal(2, component.QualityGroup);
        Assert.Equal(fixture.PageCount, component.PageCount);
        Assert.Equal(fixture.InstanceCount, component.InstanceCount);
    }

    public enum SourceFault
    {
        None,
        WrongCluster,
        WrongBiome,
        WrongSpecies,
        WrongClusterPackage,
        WrongPageCount,
        WrongInstanceCount,
        WrongOrigin,
        WrongBounds,
        WrongWorld,
        WrongCoordinate,
        WrongLayer,
        WrongOwningCell,
        OutOfRangeCoordinate
    }

    public enum ClosureShape
    {
        SingularCommonOrigin,
        MultipleSpecies,
        MixedPageOrigins
    }

    public enum ActivationFault
    {
        WrongCell,
        WrongOrigin,
        NonIntersectingBounds,
        PersistentScene
    }

    private static RuntimeAssetCatalogArtifact FindArtifact(
        RuntimeAssetCatalog catalog,
        Guid guid,
        string variant)
    {
        Assert.True(catalog.TryGetArtifact(
            guid,
            variant,
            out RuntimeAssetCatalogArtifact artifact));
        return artifact;
    }

    private static void AssertDependencies(
        RuntimeAssetCatalogArtifact artifact,
        params (Guid Guid, string AssetType, string Variant)[] expected)
    {
        Assert.Equal(expected.Length, artifact.Dependencies.Count);
        foreach ((Guid guid, string assetType, string variant) in expected)
        {
            Assert.Contains(
                artifact.Dependencies,
                dependency =>
                    dependency.Guid == guid &&
                    string.Equals(
                        dependency.PackageId,
                        VegetationSceneFixture.PackageId,
                        StringComparison.Ordinal) &&
                    string.Equals(dependency.AssetType, assetType, StringComparison.Ordinal) &&
                    string.Equals(dependency.Variant, variant, StringComparison.Ordinal) &&
                    dependency.Required);
        }
    }

    private sealed class DeterministicRenderingDependencyCooker : IRuntimeAssetCooker
    {
        private readonly string m_OutputRoot;

        public DeterministicRenderingDependencyCooker(string outputRoot)
        {
            m_OutputRoot = outputRoot;
        }

        public string ProviderId => "com.arisen.test.vegetation-scene-rendering-dependencies";

        public IReadOnlyCollection<string> AssetTypes { get; } = ["Mesh", "Material"];

        public List<RuntimeAssetCookRequest> Requests { get; } = new();

        public RuntimeAssetCookerOutput Cook(
            RuntimeAssetCookContext context,
            RuntimeAssetCookRequest request)
        {
            ArgumentNullException.ThrowIfNull(context);
            string expectedVariant = request.AssetType switch
            {
                "Mesh" => "staticmesh.uint32",
                "Material" => "material.runtime",
                _ => throw new InvalidOperationException(
                    $"Unsupported rendering dependency type '{request.AssetType}'.")
            };
            if (!string.Equals(
                    request.PackageId,
                    VegetationSceneFixture.PackageId,
                    StringComparison.Ordinal) ||
                !string.Equals(request.Variant, expectedVariant, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected rendering dependency request '{request.PackageId}:" +
                    $"{request.AssetType}:{request.Variant}'.");
            }

            Requests.Add(request);
            byte[] payload = Encoding.UTF8.GetBytes(
                $"{request.Guid:N}|{request.PackageId}|{request.AssetType}|{request.Variant}");
            string sourcePath = Path.Combine(
                m_OutputRoot,
                "RenderingDependencyCook",
                $"{request.Guid:N}.{request.AssetType}.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllBytes(sourcePath, payload);
            return RuntimeAssetCookerOutput.FromFile(
                request,
                expectedVariant,
                $"{request.PackageId}/{request.Guid:N}/{expectedVariant}.bin",
                sourcePath,
                formatVersion: 1);
        }
    }

    private sealed class VegetationSceneFixture : IDisposable
    {
        public const string PackageId = "com.arisen.vegetation.scene-test";
        private const string CellLayer = "vegetation";

        private readonly string m_Root;
        private readonly string m_BiomePath;
        private int m_SceneIndex;

        public VegetationSceneFixture(
            ClosureShape shape = ClosureShape.SingularCommonOrigin,
            bool cookPrimarySpecies = true,
            bool cookSecondarySpecies = true,
            bool cookBiome = true)
        {
            m_Root = Path.Combine(
                Path.GetTempPath(),
                "ArisenVegetationSceneComponentTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
            Database = new TestAssetDatabase(
                AssetSourceAccessMode.Diagnostic,
                Path.Combine(m_Root, "Cooked"));

            ClusterGuid = Guid.Parse("b1000000-0000-0000-0000-000000000001");
            BiomeGuid = Guid.Parse("b2000000-0000-0000-0000-000000000001");
            SpeciesGuid = Guid.Parse("b3000000-0000-0000-0000-000000000001");
            SecondSpeciesGuid = Guid.Parse("b3000000-0000-0000-0000-000000000002");
            PageGuid = Guid.Parse("b4000000-0000-0000-0000-000000000001");
            SecondPageGuid = Guid.Parse("b4000000-0000-0000-0000-000000000002");
            MeshGuid = Guid.Parse("b5000000-0000-0000-0000-000000000001");
            SecondMeshGuid = Guid.Parse("b5000000-0000-0000-0000-000000000002");
            MaterialGuid = Guid.Parse("b6000000-0000-0000-0000-000000000001");
            SecondMaterialGuid = Guid.Parse("b6000000-0000-0000-0000-000000000002");
            WorldGuid = Guid.Parse("b7000000-0000-0000-0000-000000000001");
            CellCoordinate = new WorldCellCoordinate(4, 0, -2);
            Origin = new WorldPosition(8192.0, 32.0, -4096.0);
            CellBounds = new WorldBounds(
                new WorldPosition(8000.0, -100.0, -4300.0),
                new WorldPosition(8400.0, 200.0, -3800.0));

            string speciesPath = WriteText(
                "Primary.arivegetationspecies",
                CreateSpeciesSource(
                    SpeciesGuid,
                    "Primary Scene Species",
                    MeshGuid,
                    MaterialGuid));
            string secondSpeciesPath = WriteText(
                "Secondary.arivegetationspecies",
                CreateSpeciesSource(
                    SecondSpeciesGuid,
                    "Secondary Scene Species",
                    SecondMeshGuid,
                    SecondMaterialGuid));
            m_BiomePath = WriteText("Scene.arivegetationbiome", CreateBiomeSource());
            string meshPath = WriteText("Scene.mesh", "fixture mesh");
            string secondMeshPath = WriteText("Scene2.mesh", "fixture mesh 2");
            string materialPath = WriteText("Scene.material", "fixture material");
            string secondMaterialPath = WriteText("Scene2.material", "fixture material 2");
            string clusterPath = WriteText("Scene.generated-cluster", "fixture cluster");
            string pagePath = WriteText("Scene.generated-page", "fixture page");
            string secondPagePath = WriteText("Scene.generated-page-2", "fixture page 2");

            Database.AddAsset(SpeciesGuid, VegetationAssetTypes.Species, speciesPath, PackageId);
            Database.AddAsset(
                SecondSpeciesGuid,
                VegetationAssetTypes.Species,
                secondSpeciesPath,
                PackageId);
            Database.AddAsset(BiomeGuid, VegetationAssetTypes.Biome, m_BiomePath, PackageId);
            Database.AddAsset(MeshGuid, "Mesh", meshPath, PackageId);
            Database.AddAsset(SecondMeshGuid, "Mesh", secondMeshPath, PackageId);
            Database.AddAsset(MaterialGuid, "Material", materialPath, PackageId);
            Database.AddAsset(SecondMaterialGuid, "Material", secondMaterialPath, PackageId);
            Database.AddAsset(ClusterGuid, VegetationAssetTypes.Cluster, clusterPath, PackageId);
            Database.AddAsset(PageGuid, VegetationAssetTypes.InstancePage, pagePath, PackageId);
            Database.AddAsset(
                SecondPageGuid,
                VegetationAssetTypes.InstancePage,
                secondPagePath,
                PackageId);

            if (cookPrimarySpecies)
            {
                VegetationSpeciesAssetCooker.Cook(
                    Database,
                    new AssetRef<VegetationSpeciesSourceAsset>(
                        SpeciesGuid,
                        VegetationAssetTypes.Species,
                        PackageId));
            }
            if (cookSecondarySpecies)
            {
                VegetationSpeciesAssetCooker.Cook(
                    Database,
                    new AssetRef<VegetationSpeciesSourceAsset>(
                        SecondSpeciesGuid,
                        VegetationAssetTypes.Species,
                        PackageId));
            }
            if (cookBiome)
            {
                VegetationBiomeAssetCooker.Cook(
                    Database,
                    new AssetRef<VegetationBiomeSourceAsset>(
                        BiomeGuid,
                        VegetationAssetTypes.Biome,
                        PackageId));
            }
            CookedVegetationClusterArtifact artifact = VegetationClusterAssetCooker.Cook(
                Database,
                CreateClusterDescriptor(shape));
            ClusterBounds = artifact.Bounds;
            PageCount = artifact.PageCount;
            InstanceCount = artifact.InstanceCount;
        }

        public TestAssetDatabase Database { get; }
        public string Root => m_Root;
        public Guid ClusterGuid { get; }
        public Guid BiomeGuid { get; }
        public Guid SpeciesGuid { get; }
        public Guid SecondSpeciesGuid { get; }
        public Guid PageGuid { get; }
        public Guid SecondPageGuid { get; }
        public Guid MeshGuid { get; }
        public Guid SecondMeshGuid { get; }
        public Guid MaterialGuid { get; }
        public Guid SecondMaterialGuid { get; }
        public Guid WorldGuid { get; }
        public WorldCellCoordinate CellCoordinate { get; }
        public WorldPosition Origin { get; }
        public WorldBounds CellBounds { get; }
        public WorldBounds ClusterBounds { get; }
        public int PageCount { get; }
        public int InstanceCount { get; }

        public SceneFixture AddScene(
            string name,
            int entityCount = 1,
            SourceFault fault = SourceFault.None,
            WorldCellCoordinate? coordinate = null)
        {
            WorldCellCoordinate sceneCoordinate = coordinate ?? CellCoordinate;
            Guid sceneGuid = Guid.NewGuid();
            Guid[] entityGuids = Enumerable.Range(0, entityCount)
                .Select(_ => Guid.NewGuid())
                .OrderBy(value => value)
                .ToArray();
            WorldCellId cellId = WorldCellIdentity.Create(
                WorldGuid,
                sceneCoordinate,
                CellLayer);
            string source = CreateSceneSource(
                name,
                entityGuids,
                sceneCoordinate,
                cellId,
                fault);
            string sourcePath = WriteText($"{m_SceneIndex++}-{name}.arisenscene", source);
            Database.AddAsset(sceneGuid, "Scene", sourcePath, PackageId);
            return new SceneFixture(
                new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", PackageId),
                entityGuids[0],
                sourcePath,
                source,
                sceneCoordinate,
                cellId);
        }

        public SceneStagingData BuildStaging(SceneFixture scene)
        {
            Assert.True(SceneAssetLoader.TryBuildSceneStaging(
                Database,
                scene.Reference.Guid,
                scene.SourcePath,
                scene.Source,
                out SceneStagingData staging,
                out string diagnostic), diagnostic);
            return staging;
        }

        public void ReplaceBiomeWithSecondarySpeciesOnly()
        {
            ReplaceBiomeSourceWithSecondarySpeciesOnly();
            VegetationBiomeAssetCooker.Cook(
                Database,
                new AssetRef<VegetationBiomeSourceAsset>(
                    BiomeGuid,
                    VegetationAssetTypes.Biome,
                    PackageId));
        }

        public void ReplaceBiomeSourceWithSecondarySpeciesOnly()
        {
            File.WriteAllText(m_BiomePath, CreateSecondarySpeciesOnlyBiomeSource());
        }

        public void Dispose()
        {
            Database.ReleaseAllLoadedCookedAssets();
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, recursive: true);
            }
        }

        private VegetationClusterCookDescriptor CreateClusterDescriptor(ClosureShape shape)
        {
            var primarySpecies = new CookedVegetationSpeciesReference(SpeciesGuid, PackageId);
            var secondarySpecies = new CookedVegetationSpeciesReference(
                SecondSpeciesGuid,
                PackageId);
            IReadOnlyList<CookedVegetationSpeciesReference> pageSpecies =
                shape == ClosureShape.MultipleSpecies
                    ? Array.AsReadOnly([primarySpecies, secondarySpecies])
                    : Array.AsReadOnly([primarySpecies]);
            IReadOnlyList<VegetationCookedInstanceInput> firstInstances =
                shape == ClosureShape.MultipleSpecies
                    ? Array.AsReadOnly<VegetationCookedInstanceInput>(
                    [
                        new(0x101UL, 0, new Vector3(-2.0f, 0.5f, 1.0f), Quaternion.Identity, 0.8f, 1.5f),
                        new(0x102UL, 1, new Vector3(3.0f, 1.0f, -4.0f), Quaternion.Identity, 1.2f, 2.0f)
                    ])
                    : Array.AsReadOnly<VegetationCookedInstanceInput>(
                    [
                        new(0x101UL, 0, new Vector3(-2.0f, 0.5f, 1.0f), Quaternion.Identity, 0.8f, 1.5f),
                        new(0x102UL, 0, new Vector3(3.0f, 1.0f, -4.0f), Quaternion.Identity, 1.2f, 2.0f)
                    ]);
            var firstPage = new VegetationInstancePageCookDescriptor(
                PageGuid,
                ClusterGuid,
                PackageId,
                VegetationInstancePageAssetCooker.CurrentGeneratedSchemaVersion,
                Origin,
                pageSpecies,
                firstInstances);
            VegetationInstancePageCookDescriptor[] pages = shape == ClosureShape.MixedPageOrigins
                ?
                [
                    firstPage,
                    new VegetationInstancePageCookDescriptor(
                        SecondPageGuid,
                        ClusterGuid,
                        PackageId,
                        VegetationInstancePageAssetCooker.CurrentGeneratedSchemaVersion,
                        new WorldPosition(Origin.X + 128.0, Origin.Y, Origin.Z),
                        Array.AsReadOnly([primarySpecies]),
                        Array.AsReadOnly<VegetationCookedInstanceInput>(
                        [
                            new(0x201UL, 0, Vector3.Zero, Quaternion.Identity, 1.0f, 1.0f)
                        ]))
                ]
                : [firstPage];
            return new VegetationClusterCookDescriptor(
                ClusterGuid,
                PackageId,
                VegetationClusterAssetCooker.CurrentGeneratedSchemaVersion,
                new CookedVegetationBiomeReference(BiomeGuid, PackageId),
                Array.AsReadOnly(pages));
        }

        private string CreateSceneSource(
            string name,
            IReadOnlyList<Guid> entityGuids,
            WorldCellCoordinate coordinate,
            WorldCellId cellId,
            SourceFault fault)
        {
            Guid clusterGuid = fault == SourceFault.WrongCluster
                ? Guid.Parse("c1000000-0000-0000-0000-000000000001")
                : ClusterGuid;
            Guid biomeGuid = fault == SourceFault.WrongBiome
                ? Guid.Parse("c2000000-0000-0000-0000-000000000001")
                : BiomeGuid;
            Guid speciesGuid = fault == SourceFault.WrongSpecies
                ? Guid.Parse("c3000000-0000-0000-0000-000000000001")
                : SpeciesGuid;
            string clusterPackage = fault == SourceFault.WrongClusterPackage
                ? "com.arisen.vegetation.foreign"
                : PackageId;
            Guid worldGuid = fault == SourceFault.WrongWorld
                ? Guid.Parse("c7000000-0000-0000-0000-000000000001")
                : WorldGuid;
            int cellX = fault switch
            {
                SourceFault.WrongCoordinate => coordinate.X + 1,
                SourceFault.OutOfRangeCoordinate => 1_000_001,
                _ => coordinate.X
            };
            string layer = fault == SourceFault.WrongLayer ? "terrain" : CellLayer;
            Guid owningCellGuid = fault == SourceFault.WrongOwningCell
                ? Guid.Parse("c8000000-0000-0000-0000-000000000001")
                : cellId.Value;
            double originX = fault == SourceFault.WrongOrigin ? Origin.X + 1.0 : Origin.X;
            double boundsMaxX = fault == SourceFault.WrongBounds
                ? ClusterBounds.Max.X + 1.0
                : ClusterBounds.Max.X;
            int pageCount = fault == SourceFault.WrongPageCount
                ? PageCount + 1
                : PageCount;
            int instanceCount = fault == SourceFault.WrongInstanceCount
                ? InstanceCount + 1
                : InstanceCount;

            var source = new StringBuilder();
            source.AppendLine("Version: 2");
            source.AppendLine($"Name: {name}");
            source.AppendLine("ComponentSchemas:");
            source.AppendLine("- TypeId: 1");
            source.AppendLine("  Name: Transform");
            source.AppendLine("  Version: 1");
            source.AppendLine("  Required: true");
            source.AppendLine($"- TypeId: {VegetationClusterSceneComponentCodec.TypeId}");
            source.AppendLine("  Name: VegetationCluster");
            source.AppendLine("  Version: 1");
            source.AppendLine("  Required: true");
            source.AppendLine("Entities:");
            for (int index = 0; index < entityGuids.Count; index++)
            {
                source.AppendLine($"- Guid: {entityGuids[index]:D}");
                source.AppendLine($"  Name: Vegetation Cluster {index}");
                source.AppendLine("  Transform:");
                source.AppendLine("    Position: { X: 0, Y: 0, Z: 0 }");
                source.AppendLine("    Rotation: { X: 0, Y: 0, Z: 0, W: 1 }");
                source.AppendLine("    Scale: { X: 1, Y: 1, Z: 1 }");
                source.AppendLine("  VegetationCluster:");
                source.AppendLine($"    Cluster: {{ Guid: {clusterGuid:D}, PackageId: {clusterPackage} }}");
                source.AppendLine($"    Biome: {{ Guid: {biomeGuid:D}, PackageId: {PackageId} }}");
                source.AppendLine($"    Species: {{ Guid: {speciesGuid:D}, PackageId: {PackageId} }}");
                source.AppendLine($"    WorldGuid: {worldGuid:D}");
                source.AppendLine($"    OwningCellGuid: {owningCellGuid:D}");
                source.AppendLine(
                    $"    Cell: {{ X: {cellX}, Y: {coordinate.Y}, Z: {coordinate.Z}, Layer: {layer} }}");
                source.AppendLine(
                    $"    Origin: {{ X: {Format(originX)}, Y: {Format(Origin.Y)}, Z: {Format(Origin.Z)} }}");
                source.AppendLine("    Bounds:");
                source.AppendLine(
                    $"      Min: {{ X: {Format(ClusterBounds.Min.X)}, Y: {Format(ClusterBounds.Min.Y)}, Z: {Format(ClusterBounds.Min.Z)} }}");
                source.AppendLine(
                    $"      Max: {{ X: {Format(boundsMaxX)}, Y: {Format(ClusterBounds.Max.Y)}, Z: {Format(ClusterBounds.Max.Z)} }}");
                source.AppendLine("    Visible: true");
                source.AppendLine("    CastShadows: true");
                source.AppendLine("    ReceiveShadows: true");
                source.AppendLine("    QualityGroup: 2");
                source.AppendLine($"    PageCount: {pageCount}");
                source.AppendLine($"    InstanceCount: {instanceCount}");
            }
            return source.ToString();
        }

        private string CreateSpeciesSource(
            Guid guid,
            string name,
            Guid meshGuid,
            Guid materialGuid) => $$"""
            Version: 1
            SpeciesGuid: {{guid:D}}
            Name: {{name}}
            Lods:
            - Mesh: { Guid: {{meshGuid:D}}, PackageId: {{PackageId}} }
              Material: { Guid: {{materialGuid:D}}, PackageId: {{PackageId}} }
              MaximumDistance: 120.0
              MaximumScreenError: 2.0
            ShadowPolicy: Cast
            ScaleRange: { Minimum: 0.8, Maximum: 1.2 }
            YawRangeDegrees: { Minimum: 0.0, Maximum: 360.0 }
            TiltRangeDegrees: { Minimum: -5.0, Maximum: 5.0 }
            CollisionPromotion:
              Mode: None
              CapsuleRadius: 0.0
              CapsuleHalfHeight: 0.0
              MaximumDistance: 0.0
            WindResponse: 0.4
            """;

        private string CreateBiomeSource() => $$"""
            Version: 1
            BiomeGuid: {{BiomeGuid:D}}
            Name: Scene Component Biome
            GlobalSeed: 1469598103934665603
            Entries:
            - EntryId: primary
              Species: { Guid: {{SpeciesGuid:D}}, PackageId: {{PackageId}} }
              Density: 0.125
              SeedSalt: 29
              AltitudeRange: { Minimum: -500.0, Maximum: 4000.0 }
              SlopeRangeDegrees: { Minimum: 0.0, Maximum: 80.0 }
              LayerWeightRules: []
              MinimumSpacing: 1.5
              ClusterSize: 64
              ExclusionPolicy: Respect
            - EntryId: secondary
              Species: { Guid: {{SecondSpeciesGuid:D}}, PackageId: {{PackageId}} }
              Density: 0.0625
              SeedSalt: 31
              AltitudeRange: { Minimum: -500.0, Maximum: 4000.0 }
              SlopeRangeDegrees: { Minimum: 0.0, Maximum: 80.0 }
              LayerWeightRules: []
              MinimumSpacing: 2.0
              ClusterSize: 64
              ExclusionPolicy: Respect
            """;

        private string CreateSecondarySpeciesOnlyBiomeSource() => $$"""
            Version: 1
            BiomeGuid: {{BiomeGuid:D}}
            Name: Scene Component Biome
            GlobalSeed: 1469598103934665603
            Entries:
            - EntryId: secondary
              Species: { Guid: {{SecondSpeciesGuid:D}}, PackageId: {{PackageId}} }
              Density: 0.0625
              SeedSalt: 31
              AltitudeRange: { Minimum: -500.0, Maximum: 4000.0 }
              SlopeRangeDegrees: { Minimum: 0.0, Maximum: 80.0 }
              LayerWeightRules: []
              MinimumSpacing: 2.0
              ClusterSize: 64
              ExclusionPolicy: Respect
            """;

        private string WriteText(string relativePath, string contents)
        {
            string path = Path.Combine(m_Root, relativePath);
            File.WriteAllText(path, contents);
            return path;
        }

        private static string Format(double value) =>
            value.ToString("R", CultureInfo.InvariantCulture);
    }

    private sealed record SceneFixture(
        AssetRef<SceneSourceAsset> Reference,
        Guid EntityGuid,
        string SourcePath,
        string Source,
        WorldCellCoordinate Coordinate,
        WorldCellId CellId)
    {
        public bool TryGetComponent(
            EntityManager world,
            SceneLoadResult load,
            out VegetationClusterComponent component)
        {
            if (load.AuthoringEntities != null &&
                load.AuthoringEntities.TryGetEntity(EntityGuid, out Entity entity) &&
                world.HasComponent<VegetationClusterComponent>(entity))
            {
                component = world.GetComponent<VegetationClusterComponent>(entity);
                return true;
            }

            component = default;
            return false;
        }
    }
}

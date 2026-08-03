using System.Text;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.Serialization;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationRuntimeAssetCookingTests
{
    private static readonly Guid s_SpeciesGuid =
        Guid.Parse("7b0f2e52-8b67-4e3d-bf0a-cbc42f622101");
    private static readonly Guid s_BiomeGuid =
        Guid.Parse("c0a92f10-0eb9-4d24-b729-7d0f38313101");
    private static readonly Guid s_MeshGuid =
        Guid.Parse("9f57d9cc-2db6-4c85-ae7b-544338806101");
    private static readonly Guid s_MaterialGuid =
        Guid.Parse("4ac21c64-e984-4ed0-9e21-93878de52601");
    private const string PackageId = "com.arisen.vegetation.test";
    private const string WrongPackageId = "com.arisen.vegetation.foreign";

    [Fact]
    public void SpeciesRuntimeCooker_IdenticalRecookPreservesArtifactPathAndTimestamp()
    {
        using var fixture = VegetationDiskFixture.Create();
        AssetDatabase database = fixture.CreateDatabase();
        var cooker = new VegetationRuntimeAssetCooker(database);
        var request = new RuntimeAssetCookRequest(
            s_SpeciesGuid,
            PackageId,
            VegetationAssetTypes.Species);

        RuntimeAssetCookerOutput first = cooker.Cook(fixture.CreateContext(), request);
        DateTime preservedTimestamp = new(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(first.SourcePath, preservedTimestamp);

        RuntimeAssetCookerOutput repeated = cooker.Cook(fixture.CreateContext(), request);

        Assert.Equal(first.SourcePath, repeated.SourcePath);
        Assert.Equal(first.Artifact.OutputRelativePath, repeated.Artifact.OutputRelativePath);
        Assert.Equal(first.Artifact.Sha256, repeated.Artifact.Sha256);
        Assert.Equal(preservedTimestamp, File.GetLastWriteTimeUtc(repeated.SourcePath));
        Assert.True(database.TryGetCookedArtifact(
            s_SpeciesGuid,
            VegetationSpeciesAssetCooker.RuntimeVariant,
            out CookedAssetRecord registered));
        Assert.Equal(first.SourcePath, registered.Path);
    }

    [Fact]
    public void BiomeRuntimeCooker_IdenticalRecookPreservesArtifactPathAndTimestamp()
    {
        using var fixture = VegetationDiskFixture.Create();
        AssetDatabase database = fixture.CreateDatabase();
        var cooker = new VegetationRuntimeAssetCooker(database);
        var request = new RuntimeAssetCookRequest(
            s_BiomeGuid,
            PackageId,
            VegetationAssetTypes.Biome);

        RuntimeAssetCookerOutput first = cooker.Cook(fixture.CreateContext(), request);
        DateTime preservedTimestamp = new(2024, 3, 4, 5, 6, 8, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(first.SourcePath, preservedTimestamp);

        RuntimeAssetCookerOutput repeated = cooker.Cook(fixture.CreateContext(), request);

        Assert.Equal(first.SourcePath, repeated.SourcePath);
        Assert.Equal(first.Artifact.OutputRelativePath, repeated.Artifact.OutputRelativePath);
        Assert.Equal(first.Artifact.Sha256, repeated.Artifact.Sha256);
        Assert.Equal(preservedTimestamp, File.GetLastWriteTimeUtc(repeated.SourcePath));
        Assert.True(database.TryGetCookedArtifact(
            s_BiomeGuid,
            VegetationBiomeAssetCooker.RuntimeVariant,
            out CookedAssetRecord registered));
        Assert.Equal(first.SourcePath, registered.Path);
    }

    [Theory]
    [InlineData(DependencyOwnershipFault.MissingMesh)]
    [InlineData(DependencyOwnershipFault.WrongMeshOwner)]
    [InlineData(DependencyOwnershipFault.MissingMaterial)]
    [InlineData(DependencyOwnershipFault.WrongMaterialOwner)]
    [InlineData(DependencyOwnershipFault.MissingSpecies)]
    [InlineData(DependencyOwnershipFault.WrongSpeciesOwner)]
    public void RuntimeCooker_InvalidDependencyOwnershipFailsBeforeOpeningArtifactWrite(
        DependencyOwnershipFault fault)
    {
        using var fixture = VegetationOwnershipFixture.Create(fault);
        var cooker = new VegetationRuntimeAssetCooker(fixture.Database);
        bool speciesDependency = fault is DependencyOwnershipFault.MissingSpecies or
            DependencyOwnershipFault.WrongSpeciesOwner;
        Guid rootGuid = speciesDependency ? s_BiomeGuid : s_SpeciesGuid;
        string rootType = speciesDependency
            ? VegetationAssetTypes.Biome
            : VegetationAssetTypes.Species;
        string rootVariant = speciesDependency
            ? VegetationBiomeAssetCooker.RuntimeVariant
            : VegetationSpeciesAssetCooker.RuntimeVariant;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            cooker.Cook(
                fixture.CreateContext(),
                new RuntimeAssetCookRequest(rootGuid, PackageId, rootType)));

        string dependencyRole = fault switch
        {
            DependencyOwnershipFault.MissingMesh or
            DependencyOwnershipFault.WrongMeshOwner => "mesh",
            DependencyOwnershipFault.MissingMaterial or
            DependencyOwnershipFault.WrongMaterialOwner => "material",
            _ => "species"
        };
        Assert.Contains(dependencyRole, error.Message, StringComparison.OrdinalIgnoreCase);
        if (fault is DependencyOwnershipFault.MissingMesh or
            DependencyOwnershipFault.MissingMaterial or
            DependencyOwnershipFault.MissingSpecies)
        {
            Assert.Contains("missing from the asset database", error.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("resolves to", error.Message, StringComparison.Ordinal);
            Assert.Contains(WrongPackageId, error.Message, StringComparison.Ordinal);
        }

        Assert.False(fixture.Database.TryGetCookedArtifact(rootGuid, rootVariant, out _));
        Assert.False(
            Directory.Exists(fixture.Database.CookedRoot),
            "Dependency ownership must be validated before opening an artifact write transaction.");
    }

    [Fact]
    public void CookCoordinator_ClosesBiomeSpeciesMeshAndMaterialDependencies()
    {
        using var fixture = VegetationDiskFixture.Create();
        AssetDatabase database = fixture.CreateDatabase();
        var renderingDependencies = new DeterministicRenderingDependencyCooker(fixture.Root);
        var registry = new RuntimeAssetCookerRegistry();
        registry.RegisterCooker(new VegetationRuntimeAssetCooker(database));
        registry.RegisterCooker(renderingDependencies);

        RuntimeAssetCookResult result = RuntimeAssetCookCoordinator.Cook(
            fixture.CreateContext(),
            [
                new RuntimeAssetCookRootRequest(
                    "vegetationBiome",
                    s_BiomeGuid,
                    PackageId,
                    VegetationAssetTypes.Biome)
            ],
            registry);

        RuntimeAssetCatalogRoot root = Assert.Single(result.Catalog.Roots);
        Assert.Equal(VegetationBiomeAssetCooker.RuntimeVariant, root.Variant);
        Assert.Equal(4, result.Catalog.Artifacts.Count);
        Assert.Equal(4, result.Files.Count);

        RuntimeAssetCatalogArtifact biome = FindArtifact(
            result.Catalog,
            s_BiomeGuid,
            VegetationBiomeAssetCooker.RuntimeVariant);
        RuntimeAssetCatalogArtifact species = FindArtifact(
            result.Catalog,
            s_SpeciesGuid,
            VegetationSpeciesAssetCooker.RuntimeVariant);
        RuntimeAssetCatalogArtifact mesh = FindArtifact(
            result.Catalog,
            s_MeshGuid,
            "staticmesh.uint32");
        RuntimeAssetCatalogArtifact material = FindArtifact(
            result.Catalog,
            s_MaterialGuid,
            "material.runtime");

        RuntimeAssetCatalogDependency biomeDependency = Assert.Single(biome.Dependencies);
        AssertDependency(
            biomeDependency,
            species.Guid,
            species.PackageId,
            species.AssetType,
            species.Variant);
        Assert.Equal(2, species.Dependencies.Count);
        Assert.Contains(
            species.Dependencies,
            dependency => dependency.Guid == mesh.Guid &&
                dependency.AssetType == mesh.AssetType &&
                dependency.Variant == mesh.Variant &&
                dependency.Required);
        Assert.Contains(
            species.Dependencies,
            dependency => dependency.Guid == material.Guid &&
                dependency.AssetType == material.AssetType &&
                dependency.Variant == material.Variant &&
                dependency.Required);
        Assert.Empty(mesh.Dependencies);
        Assert.Empty(material.Dependencies);
        Assert.Equal(2, renderingDependencies.Requests.Count);
        Assert.Contains(
            renderingDependencies.Requests,
            request => request.Guid == s_MeshGuid &&
                request.AssetType == "Mesh" &&
                request.Variant == "staticmesh.uint32");
        Assert.Contains(
            renderingDependencies.Requests,
            request => request.Guid == s_MaterialGuid &&
                request.AssetType == "Material" &&
                request.Variant == "material.runtime");
    }

    private static RuntimeAssetCatalogArtifact FindArtifact(
        RuntimeAssetCatalog catalog,
        Guid guid,
        string variant)
    {
        Assert.True(catalog.TryGetArtifact(guid, variant, out RuntimeAssetCatalogArtifact artifact));
        return artifact;
    }

    private static void AssertDependency(
        RuntimeAssetCatalogDependency dependency,
        Guid guid,
        string packageId,
        string assetType,
        string variant)
    {
        Assert.Equal(guid, dependency.Guid);
        Assert.Equal(packageId, dependency.PackageId);
        Assert.Equal(assetType, dependency.AssetType);
        Assert.Equal(variant, dependency.Variant);
        Assert.True(dependency.Required);
    }

    private static string CreateSpeciesSource() => $$"""
        Version: 1
        SpeciesGuid: {{s_SpeciesGuid:D}}
        Name: Runtime Cooking Species
        Lods:
        - Mesh: { Guid: {{s_MeshGuid:D}}, PackageId: {{PackageId}} }
          Material: { Guid: {{s_MaterialGuid:D}}, PackageId: {{PackageId}} }
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

    private static string CreateBiomeSource() => $$"""
        Version: 1
        BiomeGuid: {{s_BiomeGuid:D}}
        Name: Runtime Cooking Biome
        GlobalSeed: 1469598103934665603
        Entries:
        - EntryId: fixture
          Species: { Guid: {{s_SpeciesGuid:D}}, PackageId: {{PackageId}} }
          Density: 0.125
          SeedSalt: 29
          AltitudeRange: { Minimum: -500.0, Maximum: 4000.0 }
          SlopeRangeDegrees: { Minimum: 0.0, Maximum: 80.0 }
          LayerWeightRules:
          - LayerId: ground
            WeightRange: { Minimum: 0.25, Maximum: 1.0 }
          MinimumSpacing: 1.5
          ClusterSize: 64
          ExclusionPolicy: Respect
        """;

    public enum DependencyOwnershipFault
    {
        MissingMesh,
        WrongMeshOwner,
        MissingMaterial,
        WrongMaterialOwner,
        MissingSpecies,
        WrongSpeciesOwner
    }

    private sealed class DeterministicRenderingDependencyCooker : IRuntimeAssetCooker
    {
        private readonly string m_OutputRoot;

        public DeterministicRenderingDependencyCooker(string outputRoot)
        {
            m_OutputRoot = outputRoot;
        }

        public string ProviderId => "com.arisen.test.vegetation-rendering-dependencies";

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
            if (!string.Equals(request.PackageId, PackageId, StringComparison.Ordinal) ||
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

    private sealed class VegetationDiskFixture : IDisposable
    {
        private VegetationDiskFixture(string root)
        {
            Root = root;
            WorkspaceRoot = Path.Combine(root, "Workspace");
            PackageRoot = Path.Combine(root, "Package");
            Directory.CreateDirectory(WorkspaceRoot);
            WriteAsset(
                "RuntimeSpecies.arivegetationspecies",
                s_SpeciesGuid,
                VegetationAssetTypes.Species,
                CreateSpeciesSource());
            WriteAsset(
                "RuntimeBiome.arivegetationbiome",
                s_BiomeGuid,
                VegetationAssetTypes.Biome,
                CreateBiomeSource());
            WriteAsset("RuntimeMesh.mesh", s_MeshGuid, "Mesh", "fixture mesh");
            WriteAsset("RuntimeMaterial.material", s_MaterialGuid, "Material", "fixture material");
        }

        public string Root { get; }

        public string WorkspaceRoot { get; }

        public string PackageRoot { get; }

        public static VegetationDiskFixture Create()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "ArisenVegetationRuntimeCookingTests",
                Guid.NewGuid().ToString("N"));
            return new VegetationDiskFixture(root);
        }

        public AssetDatabase CreateDatabase()
        {
            var database = new AssetDatabase();
            database.InitializeWorkspace(
                WorkspaceRoot,
                [(PackageId, PackageRoot)],
                AssetSourceAccessMode.RuntimeAssetCook);
            return database;
        }

        public RuntimeAssetCookContext CreateContext()
        {
            return new RuntimeAssetCookContext(
                WorkspaceRoot,
                "Production",
                "Release",
                "win-x64",
                Path.Combine(WorkspaceRoot, "Staging"),
                ForceRebuild: false);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private void WriteAsset(
            string fileName,
            Guid guid,
            string assetType,
            string contents)
        {
            string path = Path.Combine(PackageRoot, "Assets", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            SerializationUtil.Serialize(
                new AssetMetadata
                {
                    Guid = guid,
                    AssetType = assetType,
                    Importer = "VegetationRuntimeCookingFixture"
                },
                path + ".meta");
        }
    }

    private sealed class VegetationOwnershipFixture : IDisposable
    {
        private VegetationOwnershipFixture(string root, DependencyOwnershipFault fault)
        {
            Root = root;
            Directory.CreateDirectory(Root);
            Database = new TestAssetDatabase(
                AssetSourceAccessMode.RuntimeAssetCook,
                Path.Combine(Root, "Cooked"));
            string speciesPath = WriteSource(
                "RuntimeSpecies.arivegetationspecies",
                CreateSpeciesSource());
            string biomePath = WriteSource(
                "RuntimeBiome.arivegetationbiome",
                CreateBiomeSource());
            string meshPath = WriteSource("RuntimeMesh.mesh", "fixture mesh");
            string materialPath = WriteSource("RuntimeMaterial.material", "fixture material");

            if (fault != DependencyOwnershipFault.MissingSpecies)
            {
                Database.AddAsset(
                    s_SpeciesGuid,
                    VegetationAssetTypes.Species,
                    speciesPath,
                    fault == DependencyOwnershipFault.WrongSpeciesOwner
                        ? WrongPackageId
                        : PackageId);
            }

            Database.AddAsset(s_BiomeGuid, VegetationAssetTypes.Biome, biomePath, PackageId);
            if (fault != DependencyOwnershipFault.MissingMesh)
            {
                Database.AddAsset(
                    s_MeshGuid,
                    "Mesh",
                    meshPath,
                    fault == DependencyOwnershipFault.WrongMeshOwner
                        ? WrongPackageId
                        : PackageId);
            }

            if (fault != DependencyOwnershipFault.MissingMaterial)
            {
                Database.AddAsset(
                    s_MaterialGuid,
                    "Material",
                    materialPath,
                    fault == DependencyOwnershipFault.WrongMaterialOwner
                        ? WrongPackageId
                        : PackageId);
            }
        }

        public string Root { get; }

        public TestAssetDatabase Database { get; }

        public static VegetationOwnershipFixture Create(DependencyOwnershipFault fault)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "ArisenVegetationOwnershipTests",
                Guid.NewGuid().ToString("N"));
            return new VegetationOwnershipFixture(root, fault);
        }

        public RuntimeAssetCookContext CreateContext()
        {
            return new RuntimeAssetCookContext(
                Root,
                "Production",
                "Release",
                "win-x64",
                Path.Combine(Root, "Staging"),
                ForceRebuild: false);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private string WriteSource(string fileName, string contents)
        {
            string path = Path.Combine(Root, "Sources", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            return path;
        }
    }
}

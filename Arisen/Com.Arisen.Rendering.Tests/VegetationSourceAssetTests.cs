using ArisenEngine.Core.Assets;
using ArisenEngine.Core.Serialization;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationSourceAssetTests
{
    private static readonly Guid s_SpeciesGuid =
        Guid.Parse("7b0f2e52-8b67-4e3d-bf0a-cbc42f622001");
    private static readonly Guid s_SecondSpeciesGuid =
        Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid s_BiomeGuid =
        Guid.Parse("c0a92f10-0eb9-4d24-b729-7d0f38313001");
    private const string PackageId = "com.arisen.packagegame";
    private const string RenderingPackageId = "com.arisen.generic-renderpipeline";

    [Fact]
    public void SpeciesV1_PreservesOrderedLodsAndBoundedPlacementPolicy()
    {
        VegetationSpeciesSourceDescriptor species =
            VegetationSpeciesSourceAssetLoader.LoadSourceText(
                s_SpeciesGuid,
                "com.arisen.test",
                "ValleyPine.arispecies",
                CreateSpeciesSource());

        Assert.Equal(s_SpeciesGuid, species.Guid);
        Assert.Equal("com.arisen.test", species.PackageId);
        Assert.Equal("Valley Pine", species.Name);
        Assert.Equal(VegetationShadowPolicy.Cast, species.ShadowPolicy);
        Assert.Equal(new VegetationValueRange(0.8f, 1.3f), species.ScaleRange);
        Assert.Equal(new VegetationValueRange(0.0f, 360.0f), species.YawRangeDegrees);
        Assert.Equal(new VegetationValueRange(-8.0f, 8.0f), species.TiltRangeDegrees);
        Assert.Equal(VegetationCollisionPromotionMode.Capsule, species.CollisionPromotion.Mode);
        Assert.Equal(0.75f, species.WindResponse);
        Assert.Equal([35.0f, 120.0f], species.Lods.Select(lod => lod.MaximumDistance));
        Assert.Equal([1.0f, 4.0f], species.Lods.Select(lod => lod.MaximumScreenError));
        Assert.Equal("com.arisen.meshes", species.Lods[0].Mesh.PackageId);
        Assert.Equal("Mesh", species.Lods[0].Mesh.AssetType);
        Assert.Equal("Material", species.Lods[0].Material.AssetType);
    }

    [Fact]
    public void SpeciesV1_RejectsShapeIdentityPackageAndOrderedRangeViolations()
    {
        string valid = CreateSpeciesSource();

        Assert.Contains(
            "unknown field",
            Assert.Throws<InvalidDataException>(() =>
                LoadSpecies(valid + Environment.NewLine + "Unexpected: true")).Message);
        Assert.Throws<InvalidDataException>(() =>
            LoadSpecies(valid.Replace("Lods:", "Lods: invalid\nIgnored:", StringComparison.Ordinal)));
        Assert.Contains(
            "expected asset GUID",
            Assert.Throws<InvalidDataException>(() =>
                VegetationSpeciesSourceAssetLoader.LoadSourceText(
                    Guid.NewGuid(),
                    "com.arisen.test",
                    "ValleyPine.arispecies",
                    valid)).Message);
        Assert.Throws<InvalidDataException>(() =>
            VegetationSpeciesSourceAssetLoader.LoadSourceText(
                s_SpeciesGuid,
                "Com.Arisen.Test",
                "ValleyPine.arispecies",
                valid));
        Assert.Contains(
            "strictly increasing",
            Assert.Throws<InvalidDataException>(() =>
                LoadSpecies(valid.Replace("MaximumDistance: 120.0", "MaximumDistance: 30.0", StringComparison.Ordinal))).Message);
        Assert.Contains(
            "WindResponse",
            Assert.Throws<InvalidDataException>(() =>
                LoadSpecies(valid.Replace("WindResponse: 0.75", "WindResponse: .nan", StringComparison.Ordinal))).Message);
        Assert.Contains(
            "None requires zero",
            Assert.Throws<InvalidDataException>(() =>
                LoadSpecies(valid.Replace("Mode: Capsule", "Mode: None", StringComparison.Ordinal))).Message);
    }

    [Fact]
    public void BiomeV1_PreservesOrderedSpeciesRulesAndStableEntryIds()
    {
        VegetationBiomeSourceDescriptor biome =
            VegetationBiomeSourceAssetLoader.LoadSourceText(
                s_BiomeGuid,
                "com.arisen.test",
                "Valley.ariweatheredbiome",
                CreateBiomeSource());

        Assert.Equal(s_BiomeGuid, biome.Guid);
        Assert.Equal("Open Valley", biome.Name);
        Assert.Equal((ulong)0x1020304050607080, biome.GlobalSeed);
        Assert.Equal(["pine", "rock"], biome.Entries.Select(entry => entry.EntryId));
        Assert.Equal([s_SpeciesGuid, s_SecondSpeciesGuid], biome.Entries.Select(entry => entry.Species.Guid));
        Assert.Equal(["grass", "soil"], biome.Entries[0].LayerWeightRules.Select(rule => rule.LayerId));
        Assert.Equal(new VegetationValueRange(0.45f, 1.0f), biome.Entries[0].LayerWeightRules[0].WeightRange);
        Assert.Equal(VegetationExclusionPolicy.Respect, biome.Entries[0].ExclusionPolicy);
        Assert.Equal(VegetationExclusionPolicy.IgnoreSoft, biome.Entries[1].ExclusionPolicy);
        Assert.Equal(96, biome.Entries[0].ClusterSize);
    }

    [Fact]
    public void BiomeV1_RejectsDuplicateIdsUnknownFieldsAndInvalidRules()
    {
        string valid = CreateBiomeSource();

        Assert.Contains(
            "duplicate EntryId",
            Assert.Throws<InvalidDataException>(() =>
                LoadBiome(valid.Replace("EntryId: rock", "EntryId: pine", StringComparison.Ordinal))).Message);
        Assert.Contains(
            "duplicate layer-weight rule",
            Assert.Throws<InvalidDataException>(() =>
                LoadBiome(valid.Replace("LayerId: soil", "LayerId: grass", StringComparison.Ordinal))).Message);
        Assert.Contains(
            "unknown field",
            Assert.Throws<InvalidDataException>(() =>
                LoadBiome(valid.Replace("Density: 0.08", "Density: 0.08\n  QualityMultiplier: 0.5", StringComparison.Ordinal))).Message);
        Assert.Contains(
            "WeightRange",
            Assert.Throws<InvalidDataException>(() =>
                LoadBiome(valid.Replace("Maximum: 1.0", "Maximum: 1.5", StringComparison.Ordinal))).Message);
        Assert.Contains(
            "SlopeRangeDegrees",
            Assert.Throws<InvalidDataException>(() =>
                LoadBiome(valid.Replace("Minimum: 0.0, Maximum: 38.0", "Minimum: 45.0, Maximum: 38.0", StringComparison.Ordinal))).Message);
        Assert.Throws<InvalidDataException>(() =>
            VegetationBiomeSourceAssetLoader.LoadSourceText(
                s_BiomeGuid,
                "COM.ARISEN.TEST",
                "Valley.ariweatheredbiome",
                valid));
    }

    [Fact]
    public void CanonicalValleyFixturesMatchMetadataAndRuntimeContracts()
    {
        string repositoryRoot = FindRepositoryRoot();
        string assetRoot = Path.Combine(
            repositoryRoot,
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.packagegame",
            "Assets",
            "Vegetation");
        string speciesPath = Path.Combine(assetRoot, "ValleyRock.arivegetationspecies");
        string biomePath = Path.Combine(assetRoot, "ShowcaseValley.arivegetationbiome");
        string renderingAssetRoot = Path.Combine(
            repositoryRoot,
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            RenderingPackageId,
            "Assets");
        string meshPath = Path.Combine(renderingAssetRoot, "Meshes", "FacetedCrystal.obj");
        string materialPath = Path.Combine(
            renderingAssetRoot,
            "Materials",
            "StandardLitMaterial.arismaterial");
        AssetMetadata speciesMetadata = SerializationUtil.Deserialize<AssetMetadata>(
            speciesPath + ".meta",
            serializeIfNotExist: false);
        AssetMetadata biomeMetadata = SerializationUtil.Deserialize<AssetMetadata>(
            biomePath + ".meta",
            serializeIfNotExist: false);
        AssetMetadata meshMetadata = SerializationUtil.Deserialize<AssetMetadata>(
            meshPath + ".meta",
            serializeIfNotExist: false);
        AssetMetadata materialMetadata = SerializationUtil.Deserialize<AssetMetadata>(
            materialPath + ".meta",
            serializeIfNotExist: false);

        VegetationSpeciesSourceDescriptor species =
            VegetationSpeciesSourceAssetLoader.LoadSource(new AssetRecord(
                speciesMetadata.Guid,
                speciesMetadata.AssetType,
                speciesPath,
                speciesPath + ".meta",
                PackageId));
        VegetationBiomeSourceDescriptor biome =
            VegetationBiomeSourceAssetLoader.LoadSource(new AssetRecord(
                biomeMetadata.Guid,
                biomeMetadata.AssetType,
                biomePath,
                biomePath + ".meta",
                PackageId));

        Assert.Equal(s_SpeciesGuid, species.Guid);
        Assert.Equal(s_BiomeGuid, biome.Guid);
        Assert.Equal(VegetationAssetTypes.Species, speciesMetadata.AssetType);
        Assert.Equal(VegetationAssetTypes.Biome, biomeMetadata.AssetType);
        Assert.Equal(species.Guid, Assert.Single(biome.Entries).Species.Guid);
        Assert.Equal("Rock", Assert.Single(biome.Entries[0].LayerWeightRules).LayerId);

        using var temp = new VegetationTempDirectory();
        var database = new TestAssetDatabase(
            AssetSourceAccessMode.RuntimeAssetCook,
            Path.Combine(temp.Path, "Cooked"));
        database.AddAsset(
            speciesMetadata.Guid,
            speciesMetadata.AssetType,
            speciesPath,
            PackageId);
        database.AddAsset(
            biomeMetadata.Guid,
            biomeMetadata.AssetType,
            biomePath,
            PackageId);
        database.AddAsset(
            meshMetadata.Guid,
            meshMetadata.AssetType,
            meshPath,
            RenderingPackageId);
        database.AddAsset(
            materialMetadata.Guid,
            materialMetadata.AssetType,
            materialPath,
            RenderingPackageId);

        CookedVegetationSpeciesArtifact cookedSpecies = VegetationSpeciesAssetCooker.Cook(
            database,
            new AssetRef<VegetationSpeciesSourceAsset>(
                speciesMetadata.Guid,
                VegetationAssetTypes.Species,
                PackageId));
        CookedVegetationBiomeArtifact cookedBiome = VegetationBiomeAssetCooker.Cook(
            database,
            new AssetRef<VegetationBiomeSourceAsset>(
                biomeMetadata.Guid,
                VegetationAssetTypes.Biome,
                PackageId));

        Assert.Contains(
            cookedSpecies.Dependencies,
            dependency => dependency.Guid == meshMetadata.Guid &&
                dependency.PackageId == RenderingPackageId &&
                dependency.AssetType == "Mesh" &&
                dependency.Variant == "staticmesh.uint32");
        Assert.Contains(
            cookedSpecies.Dependencies,
            dependency => dependency.Guid == materialMetadata.Guid &&
                dependency.PackageId == RenderingPackageId &&
                dependency.AssetType == "Material" &&
                dependency.Variant == "material.runtime");
        VegetationCookedAssetDependency biomeDependency = Assert.Single(cookedBiome.Dependencies);
        Assert.Equal(speciesMetadata.Guid, biomeDependency.Guid);
        Assert.Equal(PackageId, biomeDependency.PackageId);
        Assert.Equal(VegetationAssetTypes.Species, biomeDependency.AssetType);
        Assert.Equal(VegetationSpeciesAssetCooker.RuntimeVariant, biomeDependency.Variant);
        Assert.True(
            VegetationSpeciesAssetCooker.TryLoadCooked(
                database,
                new AssetRef<VegetationSpeciesSourceAsset>(
                    speciesMetadata.Guid,
                    VegetationAssetTypes.Species,
                    PackageId),
                out _,
                out string speciesDiagnostic),
            speciesDiagnostic);
        Assert.True(
            VegetationBiomeAssetCooker.TryLoadCooked(
                database,
                new AssetRef<VegetationBiomeSourceAsset>(
                    biomeMetadata.Guid,
                    VegetationAssetTypes.Biome,
                    PackageId),
                out _,
                out string biomeDiagnostic),
            biomeDiagnostic);
    }

    private static VegetationSpeciesSourceDescriptor LoadSpecies(string source) =>
        VegetationSpeciesSourceAssetLoader.LoadSourceText(
            s_SpeciesGuid,
            "com.arisen.test",
            "ValleyPine.arispecies",
            source);

    private static VegetationBiomeSourceDescriptor LoadBiome(string source) =>
        VegetationBiomeSourceAssetLoader.LoadSourceText(
            s_BiomeGuid,
            "com.arisen.test",
            "Valley.ariweatheredbiome",
            source);

    private static string CreateSpeciesSource() => $$"""
        Version: 1
        SpeciesGuid: {{s_SpeciesGuid:D}}
        Name: Valley Pine
        Lods:
        - Mesh: { Guid: 30000000-0000-0000-0000-000000000001, PackageId: com.arisen.meshes }
          Material: { Guid: 40000000-0000-0000-0000-000000000001, PackageId: com.arisen.materials }
          MaximumDistance: 35.0
          MaximumScreenError: 1.0
        - Mesh: { Guid: 30000000-0000-0000-0000-000000000002, PackageId: com.arisen.meshes }
          Material: { Guid: 40000000-0000-0000-0000-000000000002, PackageId: com.arisen.materials }
          MaximumDistance: 120.0
          MaximumScreenError: 4.0
        ShadowPolicy: Cast
        ScaleRange: { Minimum: 0.8, Maximum: 1.3 }
        YawRangeDegrees: { Minimum: 0.0, Maximum: 360.0 }
        TiltRangeDegrees: { Minimum: -8.0, Maximum: 8.0 }
        CollisionPromotion:
          Mode: Capsule
          CapsuleRadius: 0.45
          CapsuleHalfHeight: 2.2
          MaximumDistance: 24.0
        WindResponse: 0.75
        """;

    private static string CreateBiomeSource() => $$"""
        Version: 1
        BiomeGuid: {{s_BiomeGuid:D}}
        Name: Open Valley
        GlobalSeed: 1161981756646125696
        Entries:
        - EntryId: pine
          Species: { Guid: {{s_SpeciesGuid:D}}, PackageId: com.arisen.vegetation.content }
          Density: 0.08
          SeedSalt: 11
          AltitudeRange: { Minimum: -200.0, Maximum: 1800.0 }
          SlopeRangeDegrees: { Minimum: 0.0, Maximum: 38.0 }
          LayerWeightRules:
          - LayerId: grass
            WeightRange: { Minimum: 0.45, Maximum: 1.0 }
          - LayerId: soil
            WeightRange: { Minimum: 0.0, Maximum: 0.55 }
          MinimumSpacing: 2.5
          ClusterSize: 96
          ExclusionPolicy: Respect
        - EntryId: rock
          Species: { Guid: {{s_SecondSpeciesGuid:D}}, PackageId: com.arisen.vegetation.content }
          Density: 0.015
          SeedSalt: 29
          AltitudeRange: { Minimum: -500.0, Maximum: 4000.0 }
          SlopeRangeDegrees: { Minimum: 15.0, Maximum: 82.0 }
          LayerWeightRules: []
          MinimumSpacing: 4.0
          ClusterSize: 32
          ExclusionPolicy: IgnoreSoft
        """;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Arisen")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Arisen repository root.");
    }

    private sealed class VegetationTempDirectory : IDisposable
    {
        public VegetationTempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenVegetationSourceAssetTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

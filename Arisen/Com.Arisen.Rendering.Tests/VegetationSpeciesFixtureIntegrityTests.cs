using System.Globalization;
using ArisenEngine.Core.Assets;
using ArisenEngine.Rendering.Resources;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation.Assets;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

// Proves the canonical valley fixture is real, inspectable species content: authored sources,
// genuinely varied geometry and texture data, materials that satisfy the vegetation material
// contract against the real shader, and a biome plus recipes that bind the four species.
public sealed class VegetationSpeciesFixtureIntegrityTests : IDisposable
{
    private const string PackageId = VegetationCanonicalFixture.PackageId;

    private readonly string m_WorkspaceRoot = Path.Combine(
        Path.GetTempPath(),
        "ArisenVegetationSpeciesFixtureTests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (!Directory.Exists(m_WorkspaceRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(m_WorkspaceRoot, recursive: true);
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }

    [Fact]
    public void CanonicalSpeciesAreRealContractCompliantContent()
    {
        string repositoryRoot = FindRepositoryRoot();
        string packageRoot = Path.Combine(
            repositoryRoot,
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            PackageId);
        string pipelineRoot = Path.Combine(
            repositoryRoot,
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            VegetationCanonicalFixture.PipelinePackageId);
        Directory.CreateDirectory(m_WorkspaceRoot);
        var database = new AssetDatabase();
        database.InitializeWorkspace(
            m_WorkspaceRoot,
            [
                (PackageId, packageRoot),
                (VegetationCanonicalFixture.PipelinePackageId, pipelineRoot)
            ],
            AssetSourceAccessMode.RuntimeAssetCook);
        try
        {
            AssertCanonicalSpeciesAssets(database, packageRoot);
            AssertCanonicalBiome(database);
            AssertCanonicalRecipes(database);
        }
        finally
        {
            database.ReleaseAllLoadedCookedAssets();
        }
    }

    private static void AssertCanonicalSpeciesAssets(
        AssetDatabase database,
        string packageRoot)
    {
        foreach (VegetationCanonicalSpecies species in VegetationCanonicalFixture.Species)
        {
            string meshPath = $"Assets/Vegetation/Meshes/{species.MeshFileName}";
            string materialPath = $"Assets/Vegetation/Materials/{species.MaterialFileName}";
            AssertTrackedAsset(
                packageRoot,
                meshPath,
                species.MeshGuid,
                "Mesh",
                "ArisenTextMeshImporter");
            AssertTrackedAsset(
                packageRoot,
                materialPath,
                species.MaterialGuid,
                "Material",
                "ArisenMaterialImporter");
            AssertTrackedAsset(
                packageRoot,
                $"Assets/Vegetation/Valley{species.Name}.arivegetationspecies",
                species.SpeciesGuid,
                VegetationAssetTypes.Species,
                "ArisenVegetationSpeciesImporter");
            AssertTrackedAsset(
                packageRoot,
                $"Assets/Vegetation/Valley{species.Name}.arivegetationscatter",
                species.RecipeGuid,
                VegetationAssetTypes.ScatterRecipe,
                "ArisenVegetationScatterRecipeImporter");
            foreach (string channel in new[] { "Albedo", "Normal", "ORM" })
            {
                AssertRealTexture(
                    packageRoot,
                    $"Assets/Vegetation/Textures/{species.TexturePrefix}{channel}.ppm");
            }

            AssertRealMesh(Path.Combine(packageRoot, meshPath.Replace('/', Path.DirectorySeparatorChar)));

            VegetationSpeciesSourceDescriptor authored =
                VegetationSpeciesSourceAssetLoader.LoadSource(
                    database,
                    new AssetRef<VegetationSpeciesSourceAsset>(
                        species.SpeciesGuid,
                        VegetationAssetTypes.Species,
                        PackageId));
            Assert.Equal(species.SpeciesGuid, authored.Guid);
            Assert.Equal($"Valley {species.Name}", authored.Name);
            Assert.Equal(VegetationShadowPolicy.Cast, authored.ShadowPolicy);
            Assert.Equal(species.WindResponse, authored.WindResponse, precision: 5);
            Assert.Equal(
                species.CollisionMode,
                authored.CollisionPromotion.Mode);
            Assert.Equal(
                species.CapsuleRadius,
                authored.CollisionPromotion.CapsuleRadius,
                precision: 5);
            Assert.Equal(
                species.CapsuleHalfHeight,
                authored.CollisionPromotion.CapsuleHalfHeight,
                precision: 5);
            Assert.Equal(
                species.CapsuleMaximumDistance,
                authored.CollisionPromotion.MaximumDistance,
                precision: 5);
            Assert.True(authored.ScaleRange.Minimum < authored.ScaleRange.Maximum);
            Assert.True(authored.TiltRangeDegrees.Minimum < authored.TiltRangeDegrees.Maximum);

            VegetationSpeciesLodDescriptor lod = Assert.Single(authored.Lods);
            Assert.Equal(species.MeshGuid, lod.Mesh.Guid);
            Assert.Equal(PackageId, lod.Mesh.PackageId);
            Assert.Equal(species.MaterialGuid, lod.Material.Guid);
            Assert.Equal(PackageId, lod.Material.PackageId);
            Assert.Equal(species.LodMaximumDistance, lod.MaximumDistance, precision: 5);
            Assert.Equal(species.LodMaximumScreenError, lod.MaximumScreenError, precision: 5);

            MaterialAsset material = MaterialAssetLoader.LoadSource(database, species.MaterialGuid);
            VegetationMaterialSemantics semantics = VegetationMaterialContract.Resolve(
                material,
                species.MaterialGuid);
            Assert.Equal(species.RoughnessFactor, semantics.RoughnessFactor, precision: 5);
            Assert.Equal(species.TintVariation, semantics.TintVariation, precision: 5);
            Assert.True(semantics.MetallicFactor < 0.5f);
            Assert.Equal(0.0f, semantics.AlphaCutoff);
            Assert.Equal(
                VegetationMaterialFlags.TintVariation,
                semantics.Flags & VegetationMaterialFlags.TintVariation);
            Assert.Equal(Vector4.One, semantics.BaseColorFactor);
        }
    }

    private static void AssertCanonicalBiome(AssetDatabase database)
    {
        VegetationBiomeSourceDescriptor biome = VegetationBiomeSourceAssetLoader.LoadSource(
            database,
            new AssetRef<VegetationBiomeSourceAsset>(
                VegetationCanonicalFixture.BiomeGuid,
                VegetationAssetTypes.Biome,
                PackageId));

        Assert.Equal(VegetationCanonicalFixture.BiomeGuid, biome.Guid);
        Assert.Equal(VegetationCanonicalFixture.Species.Length, biome.Entries.Count);
        for (int index = 0; index < VegetationCanonicalFixture.BiomeEntries.Length; index++)
        {
            VegetationCanonicalSpecies expected = VegetationCanonicalFixture.BiomeEntries[index];
            VegetationBiomeEntryDescriptor entry = biome.Entries[index];
            Assert.Equal(expected.EntryId, entry.EntryId);
            Assert.Equal(expected.SpeciesGuid, entry.Species.Guid);
            Assert.Equal(PackageId, entry.Species.PackageId);
            Assert.True(entry.Density > 0.0f);
            Assert.True(entry.MinimumSpacing >= 0.001f);
            Assert.InRange(entry.ClusterSize, 1, 4096);
            Assert.NotEmpty(entry.LayerWeightRules);
            Assert.True(entry.AltitudeRange.Minimum < entry.AltitudeRange.Maximum);
            Assert.True(entry.SlopeRangeDegrees.Minimum < entry.SlopeRangeDegrees.Maximum);
        }
    }

    private static void AssertCanonicalRecipes(AssetDatabase database)
    {
        foreach (VegetationCanonicalSpecies species in VegetationCanonicalFixture.Species)
        {
            VegetationScatterRecipeDescriptor recipe =
                VegetationScatterRecipeSourceAssetLoader.LoadSource(
                    database,
                    new AssetRef<VegetationScatterRecipeSourceAsset>(
                        species.RecipeGuid,
                        VegetationAssetTypes.ScatterRecipe,
                        PackageId));
            Assert.Equal(species.RecipeGuid, recipe.Guid);
            Assert.Equal(species.EntryId, recipe.EntryId);
            Assert.Equal(VegetationCanonicalFixture.WorldGuid, recipe.World.Guid);
            Assert.Equal(VegetationCanonicalFixture.BiomeGuid, recipe.Biome.Guid);
            Assert.Equal(VegetationCanonicalFixture.TerrainRootGuid, recipe.TerrainRoot.Guid);
            Assert.Equal(0, recipe.Cell.Coordinate.X);
            Assert.Equal(0, recipe.Cell.Coordinate.Y);
            Assert.Equal(0, recipe.Cell.Coordinate.Z);
            Assert.Equal("surface", recipe.Cell.Layer);
            Assert.True(recipe.UnscaledConservativeRadius > 0.0f);
            Assert.Empty(recipe.Exclusions);
        }
    }

    private static void AssertTrackedAsset(
        string packageRoot,
        string relativePath,
        Guid expectedGuid,
        string expectedAssetType,
        string expectedImporter)
    {
        string path = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Fixture asset '{relativePath}' is missing.");
        AssetMetadata metadata =
            ArisenEngine.Core.Serialization.SerializationUtil.Deserialize<AssetMetadata>(
                path + ".meta",
                serializeIfNotExist: false);
        Assert.Equal(expectedGuid, metadata.Guid);
        Assert.Equal(expectedAssetType, metadata.AssetType);
        Assert.Equal(expectedImporter, metadata.Importer);
    }

    private static void AssertRealMesh(string meshPath)
    {
        string[] lines = File.ReadAllLines(meshPath);
        int vertexCount = 0;
        int triangleCount = 0;
        var vertexColors = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                vertexCount++;
                string[] fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Assert.Equal(16, fields.Length);
                vertexColors.Add(string.Join(',', fields[13..16]));
            }
            else if (line.StartsWith("i ", StringComparison.Ordinal))
            {
                triangleCount++;
            }
        }

        Assert.True(vertexCount >= 12, $"Mesh '{meshPath}' has only {vertexCount} vertices.");
        Assert.True(triangleCount >= 8, $"Mesh '{meshPath}' has only {triangleCount} triangles.");
        Assert.True(
            vertexColors.Count >= 3,
            $"Mesh '{meshPath}' does not carry varied vertex colours.");
    }

    private static void AssertRealTexture(string packageRoot, string relativePath)
    {
        string path = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Fixture texture '{relativePath}' is missing.");
        string[] tokens = File.ReadAllText(path).Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("P3", tokens[0]);
        int width = int.Parse(tokens[1], CultureInfo.InvariantCulture);
        int height = int.Parse(tokens[2], CultureInfo.InvariantCulture);
        Assert.Equal(32, width);
        Assert.Equal(32, height);
        Assert.Equal(255, int.Parse(tokens[3], CultureInfo.InvariantCulture));
        int[] components = tokens[4..].Select(
            token => int.Parse(token, CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(width * height * 3, components.Length);
        var colours = new HashSet<int>();
        int minimumComponent = 255;
        int maximumComponent = 0;
        for (int index = 0; index < components.Length; index += 3)
        {
            Assert.InRange(components[index], 0, 255);
            Assert.InRange(components[index + 1], 0, 255);
            Assert.InRange(components[index + 2], 0, 255);
            minimumComponent = Math.Min(
                minimumComponent,
                Math.Min(components[index], Math.Min(components[index + 1], components[index + 2])));
            maximumComponent = Math.Max(
                maximumComponent,
                Math.Max(components[index], Math.Max(components[index + 1], components[index + 2])));
            colours.Add(
                (components[index] << 16) |
                (components[index + 1] << 8) |
                components[index + 2]);
        }

        Assert.True(
            colours.Count >= 8,
            $"Texture '{relativePath}' only contains {colours.Count} distinct colours.");
        Assert.True(
            maximumComponent - minimumComponent >= 3,
            $"Texture '{relativePath}' only spans {maximumComponent - minimumComponent} value steps.");
    }

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

        throw new InvalidOperationException("Could not locate repository root.");
    }
}

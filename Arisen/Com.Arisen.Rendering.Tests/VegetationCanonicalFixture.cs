using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation.Assets;

namespace Com.Arisen.Rendering.Tests;

// Canonical outdoor species fixture shared by the vegetation cooking, integrity, and validation
// contract tests. Entries are ordered by recipe Guid because VegetationScatterRecipeGenerator
// orders recipes by package then Guid.
internal sealed record VegetationCanonicalSpecies
(
    string Name,
    string EntryId,
    Guid RecipeGuid,
    Guid SpeciesGuid,
    Guid ClusterGuid,
    Guid PageGuid,
    int PageCount,
    Guid MeshGuid,
    Guid MaterialGuid,
    string MeshFileName,
    string MaterialFileName,
    string TexturePrefix,
    int InstanceCount,
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ,
    double WindResponse,
    float LodMaximumDistance,
    float LodMaximumScreenError,
    float RoughnessFactor,
    float TintVariation,
    VegetationCollisionPromotionMode CollisionMode,
    float CapsuleRadius,
    float CapsuleHalfHeight,
    float CapsuleMaximumDistance)
{
    public WorldBounds Bounds => new(
        new WorldPosition(MinX, MinY, MinZ),
        new WorldPosition(MaxX, MaxY, MaxZ));
}

internal static class VegetationCanonicalFixture
{
    public const string PackageId = "com.arisen.packagegame";
    public const string PipelinePackageId = "com.arisen.vegetation.generic-renderpipeline";
    public const string FilterPackageId = "com.arisen.generic-renderpipeline";

    public static readonly Guid WorldGuid =
        Guid.Parse("9a9b4db5-c0a8-4f2e-8929-89464bea9d51");
    public static readonly Guid BiomeGuid =
        Guid.Parse("c0a92f10-0eb9-4d24-b729-7d0f38313001");
    public static readonly Guid CenterSceneGuid =
        Guid.Parse("506af06e-b16d-4573-b6c9-98548c370e90");
    public static readonly Guid CenterCellGuid =
        Guid.Parse("5d13eda6-606a-57a0-bae4-cd559ddad464");
    public static readonly Guid PersistentSceneGuid =
        Guid.Parse("bfdbfc32-8a32-4b02-b8a9-65a172859a5c");
    public static readonly Guid TerrainRootGuid =
        Guid.Parse("6f4d0a1c-0e85-4a42-93fe-34058ef48511");
    public static readonly Guid VegetationShaderGuid =
        Guid.Parse("2a536b1f-81cf-4d91-a84f-39bc6f7e15a2");
    public static readonly WorldPosition CellOrigin = new(-256.0, -64.0, -256.0);

    // Ordered by recipe Guid: tree, rock, shrub, grass.
    public static readonly VegetationCanonicalSpecies[] Species =
    [
        new(
            "Tree",
            "valley-tree",
            Guid.Parse("33817180-f63a-4f57-a3d4-cc0a7496707c"),
            Guid.Parse("83212e38-5d3e-4494-8929-fe27639b5a29"),
            Guid.Parse("6cea50e6-732f-6658-8ea0-48f1682b62e0"),
            Guid.Parse("83c82df2-9fef-e19e-9bda-8d9421c48045"),
            1,
            Guid.Parse("499d50d7-10af-4923-b97a-c1444895d327"),
            Guid.Parse("6d411706-c507-4c87-a392-aea7998889bd"),
            "ValleyTree.armesh",
            "ValleyTreeMaterial.arismaterial",
            "ValleyTree_",
            312,
            -215.45700645446777,
            -7.20877742767334,
            -262.6460111141205,
            -11.507012844085693,
            45.82605314254761,
            7.714510917663574,
            0.35,
            500.0f,
            6.0f,
            0.8f,
            0.45f,
            VegetationCollisionPromotionMode.Capsule,
            0.35f,
            1.6f,
            32.0f),
        new(
            "Rock",
            "valley-rock",
            Guid.Parse("aa347d75-087c-4fb9-998f-2cc6130ceac1"),
            Guid.Parse("7b0f2e52-8b67-4e3d-bf0a-cbc42f622001"),
            Guid.Parse("e90ae5ab-24fb-2617-9983-3ed656bd652c"),
            Guid.Parse("df936767-8c79-a601-af91-73cae122c63e"),
            1,
            Guid.Parse("89ae1524-c1c0-47c3-85a5-6a16838035f1"),
            Guid.Parse("33fb5b2f-c310-478c-8523-8eeefa4ea747"),
            "ValleyBoulder.armesh",
            "ValleyRockMaterial.arismaterial",
            "ValleyRock_",
            764,
            -259.39336678385735,
            6.416667222976685,
            -258.59615260362625,
            3.4432592391967773,
            55.813838958740234,
            2.249734401702881,
            0.05,
            180.0f,
            4.0f,
            0.95f,
            0.3f,
            VegetationCollisionPromotionMode.Capsule,
            0.75f,
            1.5f,
            24.0f),
        new(
            "Shrub",
            "valley-shrub",
            Guid.Parse("b325337c-2610-40e3-9186-723914b1dbae"),
            Guid.Parse("e5605813-00b6-4129-85b2-05b754ccde74"),
            Guid.Parse("dd618cfa-c607-cf82-bbb8-545ff1cb1a4d"),
            Guid.Parse("5653ee6a-8b63-e743-914a-033422b4b75a"),
            5,
            Guid.Parse("db1a5934-3fb7-49db-aa41-f318c8068a38"),
            Guid.Parse("7f730a67-ea48-45dd-87ec-f473dbde9b5b"),
            "ValleyShrub.armesh",
            "ValleyShrubMaterial.arismaterial",
            "ValleyShrub_",
            4375,
            -258.5532653555274,
            -2.4312198162078857,
            -258.72216442227364,
            2.5102195739746094,
            55.250099420547485,
            2.5753307342529297,
            0.55,
            250.0f,
            2.0f,
            0.85f,
            0.55f,
            VegetationCollisionPromotionMode.None,
            0.0f,
            0.0f,
            0.0f),
        new(
            "Grass",
            "valley-grass",
            Guid.Parse("ca8e2582-2225-4da3-bea7-c05da1604403"),
            Guid.Parse("3eb515ce-3dea-4994-81a1-a14fc2fe0eb5"),
            Guid.Parse("324397e1-7ddb-a3fd-8d4e-1c077443f814"),
            Guid.Parse("61274bea-6b1d-5b0e-7771-f21290c708cb"),
            11,
            Guid.Parse("00135f74-4ecf-45c4-b9f2-72a68151a1f3"),
            Guid.Parse("77bd836b-eacd-4aec-a349-3c271f7a1407"),
            "ValleyGrassTuft.armesh",
            "ValleyGrassMaterial.arismaterial",
            "ValleyGrass_",
            44397,
            -256.77769664116204,
            -0.38987600803375244,
            -256.7855395078659,
            0.7617918848991394,
            51.8774294257164,
            0.7533199191093445,
            0.85,
            150.0f,
            1.0f,
            0.9f,
            0.85f,
            VegetationCollisionPromotionMode.None,
            0.0f,
            0.0f,
            0.0f)
    ];

    // Authored biome entry order in ShowcaseValley.arivegetationbiome: rock, grass, shrub, tree.
    public static readonly VegetationCanonicalSpecies[] BiomeEntries =
    [
        Require("Rock"),
        Require("Grass"),
        Require("Shrub"),
        Require("Tree")
    ];

    public static int TotalInstanceCount =>
        Species.Sum(static species => species.InstanceCount);

    public static VegetationCanonicalSpecies Require(string name) =>
        Species.First(species =>
            string.Equals(species.Name, name, StringComparison.Ordinal));

    public static string GetGeneratedDirectoryName(Guid recipeGuid) =>
        recipeGuid.ToString("N");
}

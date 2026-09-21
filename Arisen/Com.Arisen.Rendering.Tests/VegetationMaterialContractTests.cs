using System.Globalization;
using System.Numerics;
using Arisen.Native.RHI;
using ArisenEngine.Core.Assets;
using ArisenEngine.Rendering;
using ArisenEngine.Rendering.Resources;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationMaterialContractTests : IDisposable
{
    private const string PackageId = "com.arisen.test.vegetation";
    private static readonly Guid s_ShaderGuid = Guid.Parse("c1000000-0000-4000-8000-000000000001");
    private static readonly Guid s_BaseColorGuid = Guid.Parse("c1000000-0000-4000-8000-000000000002");
    private static readonly Guid s_NormalGuid = Guid.Parse("c1000000-0000-4000-8000-000000000003");
    private static readonly Guid s_OrmGuid = Guid.Parse("c1000000-0000-4000-8000-000000000004");
    private static readonly Guid s_MaterialGuid = Guid.Parse("c1000000-0000-4000-8000-000000000005");

    private readonly string m_Root;
    private readonly string m_PackageRoot;

    public VegetationMaterialContractTests()
    {
        m_Root = Path.Combine(Path.GetTempPath(), "ArisenVegetationMaterialTests", Guid.NewGuid().ToString("N"));
        m_PackageRoot = Path.Combine(m_Root, "Package");
        Directory.CreateDirectory(m_PackageRoot);
        Write("Assets/Shaders/Vegetation.hlsl", s_ShaderGuid, "ShaderSource", "HlslShader", s_ShaderSource);
        WritePpm("Assets/Textures/BaseColor.ppm", s_BaseColorGuid);
        WritePpm("Assets/Textures/Normal.ppm", s_NormalGuid);
        WritePpm("Assets/Textures/Orm.ppm", s_OrmGuid);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void ResolveReturnsOpaqueDefaultsForAContractCompliantMaterial()
    {
        WriteMaterial();
        AssetDatabase database = CreateDatabase();

        VegetationMaterialSemantics semantics = VegetationMaterialContract.Resolve(
            MaterialAssetLoader.LoadSource(database, s_MaterialGuid),
            s_MaterialGuid);

        Assert.Equal(new Vector4(0.9f, 0.95f, 0.8f, 1.0f), semantics.BaseColorFactor);
        Assert.Equal(0.0f, semantics.AlphaCutoff);
        Assert.Equal(0.0f, semantics.MetallicFactor);
        Assert.Equal(1.0f, semantics.RoughnessFactor);
        Assert.Equal(MaterialPbrDefaults.OcclusionStrength, semantics.OcclusionStrength);
        Assert.Equal(0.0f, semantics.TintVariation);
        Assert.Equal(VegetationMaterialFlags.None, semantics.Flags);
    }

    [Fact]
    public void ResolveDerivesAlphaTestAndTintVariationFlags()
    {
        WriteMaterial(
            scalarProperties: """
                  - Name: AlphaCutoff
                    Value: 0.45
                  - Name: TintVariation
                    Value: 0.35
                """,
            baseColorAlpha: 0.8f);
        AssetDatabase database = CreateDatabase();

        VegetationMaterialSemantics semantics = VegetationMaterialContract.Resolve(
            MaterialAssetLoader.LoadSource(database, s_MaterialGuid),
            s_MaterialGuid);

        Assert.Equal(0.45f, semantics.AlphaCutoff);
        Assert.Equal(0.35f, semantics.TintVariation);
        Assert.Equal(0.8f, semantics.BaseColorFactor.W);
        Assert.Equal(
            VegetationMaterialFlags.AlphaTest | VegetationMaterialFlags.TintVariation,
            semantics.Flags);
    }

    [Fact]
    public void PreparedMaterialPreservesTheAuthoredAlphaFactor()
    {
        VegetationPreparedMaterialData prepared = VegetationPreparedMaterialData.Create(
            new VegetationMaterialSemantics(
                new Vector4(0.4f, 0.6f, 0.3f, 0.75f),
                AlphaCutoff: 0.5f,
                MetallicFactor: 0.0f,
                RoughnessFactor: 0.9f,
                OcclusionStrength: 1.0f,
                TintVariation: 0.25f,
                VegetationMaterialFlags.AlphaTest | VegetationMaterialFlags.TintVariation),
            baseColorImageIndex: 3,
            baseColorSamplerIndex: 4,
            normalImageIndex: 5,
            normalSamplerIndex: 6,
            ormImageIndex: 7,
            ormSamplerIndex: 8);

        Assert.Equal(new Vector4(0.4f, 0.6f, 0.3f, 0.75f), prepared.BaseColorFactor);
        Assert.Equal(0.5f, prepared.AlphaCutoff);
        Assert.Equal(0.25f, prepared.TintVariation);
        Assert.Equal(
            (uint)(VegetationMaterialFlags.AlphaTest | VegetationMaterialFlags.TintVariation),
            prepared.Flags);
        Assert.Equal(5u, prepared.NormalImageIndex);
        Assert.Equal(8u, prepared.OrmSamplerIndex);
    }

    [Fact]
    public void PreparedMaterialRejectsMissingBindlessDescriptors()
    {
        VegetationMaterialSemantics semantics = new(
            Vector4.One,
            0.0f,
            0.0f,
            1.0f,
            1.0f,
            0.0f,
            VegetationMaterialFlags.None);

        Assert.Throws<InvalidDataException>(() => VegetationPreparedMaterialData.Create(
            semantics,
            baseColorImageIndex: 1,
            baseColorSamplerIndex: 2,
            normalImageIndex: VegetationPreparedMaterialData.InvalidImageIndex,
            normalSamplerIndex: 4,
            ormImageIndex: 5,
            ormSamplerIndex: 6));
    }

    [Theory]
    [InlineData("missing-normal")]
    [InlineData("missing-orm")]
    [InlineData("base-color-without-mips")]
    [InlineData("normal-without-normal-filter")]
    [InlineData("orm-with-srgb-colorspace")]
    public void MaterialLoadingRejectsBindingsThatViolateTheShaderVariantContract(string defect)
    {
        WriteMaterial(defect: defect);
        AssetDatabase database = CreateDatabase();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            MaterialAssetLoader.LoadSource(database, s_MaterialGuid));

        string expectedBinding = defect switch
        {
            "missing-orm" or "orm-with-srgb-colorspace" => "MetallicRoughness",
            "missing-normal" or "normal-without-normal-filter" => "Normal",
            _ => "BaseColor"
        };
        Assert.Contains(expectedBinding, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRejectsAnOcclusionTextureOutsideTheVegetationContract()
    {
        WriteMaterial(defect: "occlusion-slot");
        AssetDatabase database = CreateDatabase();

        MaterialAsset material = MaterialAssetLoader.LoadSource(database, s_MaterialGuid);
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            VegetationMaterialContract.Resolve(material, s_MaterialGuid));

        Assert.Contains("Occlusion slot", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing-normal")]
    [InlineData("missing-orm")]
    [InlineData("base-color-without-mips")]
    [InlineData("normal-without-normal-filter")]
    [InlineData("orm-with-srgb-colorspace")]
    public void ResolveRejectsTextureBindingsOutsideTheVegetationContract(string defect)
    {
        var textures = new List<MaterialTexture2DRef>
        {
            Texture("BaseColor", 0, Texture2DVariantKey.MipmappedSRgb),
            Texture("Normal", 1, Texture2DVariantKey.MipmappedNormal),
            Texture("MetallicRoughness", 2, Texture2DVariantKey.MipmappedLinear)
        };
        switch (defect)
        {
            case "missing-normal":
                textures.RemoveAt(1);
                break;
            case "missing-orm":
                textures.RemoveAt(2);
                break;
            case "base-color-without-mips":
                textures[0] = Texture("BaseColor", 0, Texture2DVariantKey.DefaultSRgb);
                break;
            case "normal-without-normal-filter":
                textures[1] = Texture("Normal", 1, Texture2DVariantKey.MipmappedLinear);
                break;
            case "orm-with-srgb-colorspace":
                textures[2] = Texture("MetallicRoughness", 2, Texture2DVariantKey.MipmappedSRgb);
                break;
        }

        MaterialAsset material = CreateMaterial(textures);

        Assert.Throws<NotSupportedException>(() =>
            VegetationMaterialContract.Resolve(material, s_MaterialGuid));
    }

    [Theory]
    [InlineData("blend")]
    [InlineData("cull-back")]
    [InlineData("front-face-clockwise")]
    public void ResolveRejectsMaterialsThatBreakTheOpaqueTwoSidedRenderState(string defect)
    {
        WriteMaterial(renderState: defect);
        AssetDatabase database = CreateDatabase();

        MaterialAsset material = MaterialAssetLoader.LoadSource(database, s_MaterialGuid);
        Assert.Throws<NotSupportedException>(() =>
            VegetationMaterialContract.Resolve(material, s_MaterialGuid));
    }

    [Theory]
    [InlineData("alpha-cutoff-too-large")]
    [InlineData("roughness-below-floor")]
    [InlineData("tint-variation-negative")]
    [InlineData("base-color-out-of-gamut")]
    public void ResolveRejectsOutOfRangeMaterialFactors(string defect)
    {
        WriteMaterial(defect: defect);
        AssetDatabase database = CreateDatabase();

        MaterialAsset material = MaterialAssetLoader.LoadSource(database, s_MaterialGuid);
        Assert.Throws<InvalidDataException>(() =>
            VegetationMaterialContract.Resolve(material, s_MaterialGuid));
    }

    [Fact]
    public void MaterialBindingTheWrongCookedVariantIsRejectedDuringSourceLoad()
    {
        WriteMaterial(defect: "base-color-without-mips");
        AssetDatabase database = CreateDatabase();

        MaterialAssetInspection inspection = MaterialAssetLoader.InspectSource(database, s_MaterialGuid);

        Assert.False(inspection.IsShaderContractValid);
        Assert.Contains(
            inspection.ShaderContractDiagnostics,
            diagnostic =>
                diagnostic.BindingKind == MaterialShaderContractBindingKind.Texture2DVariant &&
                diagnostic.BindingName == "BaseColor");
        Assert.Contains(
            "r8g8b8a8unorm.srgb.mips",
            inspection.ShaderContractDiagnostics[0].Message,
            StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            MaterialAssetLoader.LoadSource(database, s_MaterialGuid));
    }

    [Fact]
    public void AnnotationParserCollectsCanonicalTextureVariants()
    {
        MaterialShaderContract contract = ShaderMaterialContractAnnotations.Parse(
            s_ShaderSource,
            "Vegetation.hlsl");

        Assert.Equal(
            ["BaseColor", "Normal", "MetallicRoughness"],
            contract.RequiredTexture2DRefs);
        Assert.Equal(
            [
                new MaterialShaderTextureVariantRequirement(
                    "BaseColor",
                    "r8g8b8a8unorm.srgb.mips"),
                new MaterialShaderTextureVariantRequirement(
                    "Normal",
                    "r8g8b8a8unorm.linear.mips.normalmap"),
                new MaterialShaderTextureVariantRequirement(
                    "MetallicRoughness",
                    "r8g8b8a8unorm.linear.mips")
            ],
            contract.RequiredTexture2DVariants);
    }

    [Theory]
    [InlineData("// @arisen.material.texture2dvariant BaseColor r8g8b8a8unorm.srgb", "invalid")]
    [InlineData("// @arisen.material.texture2dvariant BaseColor srgb.mips", "invalid")]
    [InlineData("// @arisen.material.texture2dvariant BaseColor r8g8b8a8unorm.srgb.nomips.normalmap", "invalid")]
    [InlineData("// @arisen.material.texture2dvariant BaseColor", "exactly one binding name")]
    public void AnnotationParserRejectsInvalidTextureVariantDeclarations(
        string annotation,
        string expectedMessage)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            ShaderMaterialContractAnnotations.Parse(annotation, "Test.hlsl"));

        Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnnotationParserRejectsConflictingVariantsForOneBinding()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            ShaderMaterialContractAnnotations.Parse(
                "// @arisen.material.texture2dvariant BaseColor r8g8b8a8unorm.srgb.mips\n" +
                "// @arisen.material.texture2dvariant BaseColor r8g8b8a8unorm.linear.mips\n",
                "Test.hlsl"));

        Assert.Contains("conflicting variants", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VariantKeyParserRoundTripsEveryCanonicalVegetationVariant()
    {
        foreach (Texture2DVariantKey key in new[]
                 {
                     Texture2DVariantKey.MipmappedSRgb,
                     Texture2DVariantKey.MipmappedLinear,
                     Texture2DVariantKey.MipmappedNormal
                 })
        {
            string variant = key.GetCookedVariant();
            Assert.True(Texture2DVariantKey.TryParseCookedVariant(variant, out Texture2DVariantKey parsed));
            Assert.Equal(key, parsed);
        }

        Assert.False(Texture2DVariantKey.TryParseCookedVariant("srgb.mips", out _));
        Assert.False(Texture2DVariantKey.TryParseCookedVariant("r8g8b8a8unorm.srgb.mips.normalmap", out _));
        Assert.False(Texture2DVariantKey.TryParseCookedVariant(string.Empty, out _));
    }

    private AssetDatabase CreateDatabase()
    {
        var database = new AssetDatabase();
        database.InitializeWorkspace(
            m_Root,
            [(PackageId, m_PackageRoot)],
            AssetSourceAccessMode.RuntimeAssetCook);
        return database;
    }

    private static MaterialTexture2DRef Texture(
        string name,
        uint slot,
        Texture2DVariantKey variant) => new(
        name,
        new Texture2DAsset(
            Guid.NewGuid(),
            name,
            variant,
            Texture2DSourceFormat.PpmP3),
        slot);

    private static MaterialAsset CreateMaterial(
        IReadOnlyList<MaterialTexture2DRef> textures,
        MaterialRenderState? renderState = null,
        IReadOnlyList<MaterialScalarProperty>? scalars = null,
        IReadOnlyList<MaterialVector4Property>? vectors = null) => new(
        s_MaterialGuid,
        "Test/Vegetation",
        new ShaderAsset(
            s_ShaderGuid,
            "Test/Vegetation",
            [new ShaderStageAsset("Vertex", EProgramStage.Vertex, "VSMain")],
            ShaderVariantKey.VulkanDebug),
        textures,
        scalars ?? [],
        vectors ?? [],
        renderState ?? MaterialRenderState.Default);

    private void WritePpm(string relativePath, Guid guid)
    {
        Write(
            relativePath,
            guid,
            "Texture2D",
            "PpmTextureImporter",
            "P3\n1 1\n255\n255 255 255\n");
    }

    private void Write(
        string relativePath,
        Guid guid,
        string assetType,
        string importer,
        string contents)
    {
        string path = Path.Combine(m_PackageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        File.WriteAllText(
            path + ".meta",
            $"Guid: {guid:D}\nAssetType: {assetType}\nImporter: {importer}\n");
    }

    private string WriteMaterial(
        string? scalarProperties = null,
        string? defect = null,
        string? renderState = null,
        float baseColorAlpha = 1.0f)
    {
        bool missingNormal = defect == "missing-normal";
        bool missingOrm = defect == "missing-orm";
        bool occlusionSlot = defect == "occlusion-slot";
        bool baseColorNoMips = defect == "base-color-without-mips";
        bool normalWithoutFilter = defect == "normal-without-normal-filter";
        bool ormSrgb = defect == "orm-with-srgb-colorspace";
        bool alphaTooLarge = defect == "alpha-cutoff-too-large";
        bool roughnessBelowFloor = defect == "roughness-below-floor";
        bool tintNegative = defect == "tint-variation-negative";
        bool baseColorOutOfGamut = defect == "base-color-out-of-gamut";

        string activeScalars = scalarProperties ?? "  - Name: MetallicFactor\n    Value: 0.0";
        if (scalarProperties == null && !roughnessBelowFloor)
        {
            activeScalars += "\n  - Name: RoughnessFactor\n    Value: 1.0";
        }
        if (alphaTooLarge)
        {
            activeScalars += "\n  - Name: AlphaCutoff\n    Value: 1.25";
        }
        if (roughnessBelowFloor)
        {
            activeScalars += "\n  - Name: RoughnessFactor\n    Value: 0.01";
        }
        if (tintNegative)
        {
            activeScalars += "\n  - Name: TintVariation\n    Value: -0.2";
        }

        string activeRenderState = renderState switch
        {
            "blend" => "  CullMode: None\n  FrontFace: CounterClockwise\n  Blend:\n    Enabled: true\n",
            "cull-back" => "  CullMode: Back\n  FrontFace: CounterClockwise\n",
            "front-face-clockwise" => "  CullMode: None\n  FrontFace: Clockwise\n",
            _ => "  CullMode: None\n  FrontFace: CounterClockwise\n"
        };

        string normalSection = missingNormal
            ? string.Empty
            : $"""
                - Name: Normal
                  Slot: 1
                  Texture:
                    Guid: {s_NormalGuid:D}
                    Variant:
                      Format: R8G8B8A8UNorm
                      ColorSpace: Linear
                      GenerateMipMaps: true
                      {(normalWithoutFilter ? "MipFilter: Color" : "MipFilter: NormalMap")}
                    SourceFormat: PpmP3

                """;
        string ormSection = missingOrm
            ? string.Empty
            : $"""
                - Name: MetallicRoughness
                  Slot: 2
                  Texture:
                    Guid: {s_OrmGuid:D}
                    Variant:
                      Format: R8G8B8A8UNorm
                      ColorSpace: {(ormSrgb ? "SRgb" : "Linear")}
                      GenerateMipMaps: true
                    SourceFormat: PpmP3

                """;
        string occlusionSection = occlusionSlot
            ? $"""
                - Name: Occlusion
                  Slot: 3
                  Texture:
                    Guid: {s_OrmGuid:D}
                    Variant:
                      Format: R8G8B8A8UNorm
                      ColorSpace: Linear
                      GenerateMipMaps: true
                    SourceFormat: PpmP3

                """
            : string.Empty;
        string redChannel = (baseColorOutOfGamut ? 1.4f : 0.9f).ToString(CultureInfo.InvariantCulture);

        string source = $"""
            Name: Test/Vegetation
            Shader:
              Guid: {s_ShaderGuid:D}
              Name: VegetationGenericRP/Vegetation
              Stages:
              - Name: Vertex
                ProgramStage: Vertex
                EntryPoint: VSMain
              - Name: Fragment
                ProgramStage: Fragment
                EntryPoint: PSMain
            Texture2DRefs:
            - Name: BaseColor
              Slot: 0
              Texture:
                Guid: {s_BaseColorGuid:D}
                Variant:
                  Format: R8G8B8A8UNorm
                  ColorSpace: SRgb
                  GenerateMipMaps: {(baseColorNoMips ? "false" : "true")}
                SourceFormat: PpmP3
            {normalSection}{ormSection}{occlusionSection}ScalarProperties:
            {activeScalars}
            Vector4Properties:
            - Name: BaseColorFactor
              Value:
                X: {redChannel}
                Y: 0.95
                Z: 0.8
                W: {baseColorAlpha.ToString(CultureInfo.InvariantCulture)}
            RenderState:
            {activeRenderState}
            """;

        string path = Path.Combine(
            m_PackageRoot,
            "Assets",
            "Materials",
            "TestVegetation.arismaterial");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, source);
        File.WriteAllText(
            path + ".meta",
            $"Guid: {s_MaterialGuid:D}\nAssetType: Material\nImporter: ArisenMaterialImporter\n");
        return path;
    }

    private const string s_ShaderSource = """
        // @arisen.material.texture2d BaseColor
        // @arisen.material.texture2dvariant BaseColor r8g8b8a8unorm.srgb.mips
        // @arisen.material.texture2d Normal
        // @arisen.material.texture2dvariant Normal r8g8b8a8unorm.linear.mips.normalmap
        // @arisen.material.texture2d MetallicRoughness
        // @arisen.material.texture2dvariant MetallicRoughness r8g8b8a8unorm.linear.mips
        """;
}

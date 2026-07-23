using System.Buffers.Binary;
using ArisenEditor.Core.Commands;
using Arisen.Native.RHI;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.RHI;
using ArisenEngine.Core.Serialization;
using ArisenEngine.Rendering;
using ArisenEngine.Rendering.Resources;
using ArisenEngine.Resources.Serialization;
using Xunit;
using EditorAssetDatabase = ArisenEditor.Core.Assets.AssetDatabase;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderingAssetPipelineTests
{
    [Fact]
    public void GeneratedAssetIdentity_DerivesStablePackageAwareChildMetadata()
    {
        var sourceGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var materialGuid = GeneratedAssetIdentity.CreateChildGuid(
            sourceGuid,
            "Com.Arisen.Test",
            "Material",
            "materials/0");
        var sameMaterialGuid = GeneratedAssetIdentity.CreateChildGuid(
            sourceGuid,
            "com.arisen.test",
            "material",
            "materials/0");
        var otherPackageGuid = GeneratedAssetIdentity.CreateChildGuid(
            sourceGuid,
            "com.arisen.other",
            "material",
            "materials/0");
        var otherChildGuid = GeneratedAssetIdentity.CreateChildGuid(
            sourceGuid,
            "com.arisen.test",
            "material",
            "materials/1");

        Assert.Equal(materialGuid, sameMaterialGuid);
        Assert.NotEqual(materialGuid, otherPackageGuid);
        Assert.NotEqual(materialGuid, otherChildGuid);

        var metadata = GeneratedAssetIdentity.CreateChildMetadata(
            sourceGuid,
            "Com.Arisen.Test",
            "Material",
            "materials/0",
            "Material",
            "GltfMaterialImporter");

        Assert.Equal(materialGuid, metadata.Guid);
        Assert.Equal("Material", metadata.AssetType);
        Assert.Equal("GltfMaterialImporter", metadata.Importer);
        Assert.NotNull(metadata.Generated);
        Assert.Equal(sourceGuid, metadata.Generated.SourceGuid);
        Assert.Equal("com.arisen.test", metadata.Generated.SourcePackageId);
        Assert.Equal("material", metadata.Generated.ChildKind);
        Assert.Equal("materials/0", metadata.Generated.ChildKey);
        Assert.Equal("GltfMaterialImporter", metadata.Generated.GeneratedByImporter);
    }

    [Fact]
    public void GltfModelImportPlanner_PlansStableChildrenAndMaterialBasics()
    {
        using var workspace = TestWorkspace.Create();
        var sourceGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var gltfPath = workspace.Write("Assets/Robot.gltf", """
            {
              "asset": { "version": "2.0" },
              "scene": 0,
              "scenes": [
                { "nodes": [0] }
              ],
              "nodes": [
                { "mesh": 0 }
              ],
              "meshes": [
                {
                  "primitives": [
                    {
                      "attributes": { "POSITION": 0 },
                      "material": 0,
                      "targets": [ { "POSITION": 1 } ]
                    }
                  ]
                }
              ],
              "materials": [
                {
                  "name": "PaintedMetal",
                  "alphaMode": "MASK",
                  "alphaCutoff": 0.42,
                  "pbrMetallicRoughness": {
                    "baseColorFactor": [0.25, 0.5, 0.75, 1.0],
                    "baseColorTexture": { "index": 0 },
                    "metallicRoughnessTexture": {
                      "index": 3,
                      "texCoord": 1,
                      "extensions": {
                        "KHR_texture_transform": {
                          "offset": [0.1, 0.2],
                          "scale": [2.0, 0.5],
                          "rotation": 0.25,
                          "texCoord": 2
                        }
                      }
                    },
                    "metallicFactor": 0.8,
                    "roughnessFactor": 0.35
                  },
                  "emissiveFactor": [0.05, 0.06, 0.07],
                  "emissiveTexture": { "index": 2 },
                  "extensions": {
                    "KHR_materials_emissive_strength": {
                      "emissiveStrength": 2.0
                    }
                  },
                  "normalTexture": { "index": 1 },
                  "occlusionTexture": { "index": 4, "strength": 0.65 }
                }
              ],
              "textures": [
                { "source": 0 },
                { "source": 1 },
                { "source": 2 },
                { "source": 3, "sampler": 0 },
                { "source": 3, "sampler": 1 }
              ],
              "images": [
                { "uri": "BaseColor.png" },
                { "uri": "Normal.png" },
                { "uri": "Emissive.png" },
                { "uri": "Packed.png" }
              ],
              "samplers": [
                {
                  "magFilter": 9728,
                  "minFilter": 9987,
                  "wrapS": 33648,
                  "wrapT": 33071
                },
                {
                  "magFilter": 9729,
                  "minFilter": 9984,
                  "wrapS": 10497,
                  "wrapT": 10497
                }
              ],
              "animations": [
                { "channels": [], "samplers": [] }
              ]
            }
            """);

        var plan = GltfModelImportPlanner.CreatePlan(gltfPath, sourceGuid, "Com.Arisen.Test");
        var materialGuid = GeneratedAssetIdentity.CreateChildGuid(sourceGuid, "com.arisen.test", "material", "materials/0");

        Assert.Equal(sourceGuid, plan.SourceGuid);
        Assert.Equal("com.arisen.test", plan.PackageId);
        Assert.Contains(plan.GeneratedChildren, child => child.Kind == "scene" && child.Key == "scenes/0");
        Assert.Contains(plan.GeneratedChildren, child => child.Kind == "mesh" && child.Key == "meshes/0");
        Assert.Contains(plan.GeneratedChildren, child => child.Kind == "texture2d" && child.Key == "images/0");
        Assert.Contains(plan.GeneratedChildren, child => child.Kind == "texture2d" && child.Key == "images/1");
        Assert.Contains(plan.GeneratedChildren, child => child.Kind == "texture2d" && child.Key == "images/2");
        Assert.Contains(plan.GeneratedChildren, child => child.Kind == "texture2d" && child.Key == "images/3");
        Assert.Equal(4, plan.GeneratedChildren.Count(child => child.Kind == "texture2d"));

        var materialChild = Assert.Single(plan.GeneratedChildren, child => child.Kind == "material");
        Assert.Equal(materialGuid, materialChild.Metadata.Guid);
        Assert.Equal("Material", materialChild.Metadata.AssetType);
        Assert.Equal("GltfMaterialImporter", materialChild.Metadata.Importer);
        Assert.NotNull(materialChild.Metadata.Generated);
        Assert.Equal(sourceGuid, materialChild.Metadata.Generated.SourceGuid);
        Assert.Equal("com.arisen.test", materialChild.Metadata.Generated.SourcePackageId);
        Assert.Equal("materials/0", materialChild.Metadata.Generated.ChildKey);

        var material = Assert.Single(plan.Materials);
        Assert.Equal(materialGuid, material.Guid);
        Assert.Equal("PaintedMetal", material.Name);
        Assert.Equal(new Vector4(0.25f, 0.5f, 0.75f, 1.0f), material.BaseColorFactor);
        Assert.Equal(new Vector4(0.05f, 0.06f, 0.07f, 2.0f), material.EmissiveFactor);
        Assert.Equal(0.8f, material.MetallicFactor);
        Assert.Equal(0.35f, material.RoughnessFactor);
        Assert.NotNull(material.BaseColorTexture);
        Assert.Equal(0, material.BaseColorTexture.TextureIndex);
        Assert.Equal(0, material.BaseColorTexture.ImageIndex);
        Assert.Equal("BaseColor.png", material.BaseColorTexture.Uri);
        Assert.True(material.BaseColorTexture.GenerateMipMaps);
        Assert.NotNull(material.NormalTexture);
        Assert.Equal(1, material.NormalTexture.TextureIndex);
        Assert.Equal(1, material.NormalTexture.ImageIndex);
        Assert.True(material.NormalTexture.GenerateMipMaps);
        Assert.NotNull(material.EmissiveTexture);
        Assert.Equal(2, material.EmissiveTexture.TextureIndex);
        Assert.Equal(2, material.EmissiveTexture.ImageIndex);
        Assert.Equal("Emissive.png", material.EmissiveTexture.Uri);
        Assert.NotNull(material.MetallicRoughnessTexture);
        Assert.Equal(3, material.MetallicRoughnessTexture.TextureIndex);
        Assert.Equal(3, material.MetallicRoughnessTexture.ImageIndex);
        Assert.True(material.MetallicRoughnessTexture.GenerateMipMaps);
        Assert.Equal(
            new MaterialTextureSamplerSettings(
                MaterialTextureFilter.Linear,
                MaterialTextureFilter.Nearest,
                MaterialTextureMipmapMode.Linear,
                MaterialTextureWrapMode.MirroredRepeat,
                MaterialTextureWrapMode.ClampToEdge),
            material.MetallicRoughnessTexture.Sampler);
        Assert.Equal(
            new MaterialTextureTransform(
                new Vector2(0.1f, 0.2f),
                new Vector2(2.0f, 0.5f),
                0.25f,
                2),
            material.MetallicRoughnessTexture.Transform);
        Assert.NotNull(material.OcclusionTexture);
        Assert.Equal(4, material.OcclusionTexture.TextureIndex);
        Assert.Equal(3, material.OcclusionTexture.ImageIndex);
        Assert.True(material.OcclusionTexture.GenerateMipMaps);
        Assert.Equal(0.65f, material.OcclusionStrength);
        Assert.Equal(GltfMaterialAlphaMode.Mask, material.AlphaMode);
        Assert.Equal(0.42f, material.AlphaCutoff);
        Assert.DoesNotContain(plan.Warnings, warning => warning.Contains("alphaMode", StringComparison.Ordinal));
        Assert.Contains(plan.Warnings, warning => warning.Contains("TEXCOORD_2", StringComparison.Ordinal));
        Assert.Contains(plan.Warnings, warning => warning.Contains("animations", StringComparison.Ordinal));
        Assert.Contains(plan.Warnings, warning => warning.Contains("morph targets", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(9728, false)]
    [InlineData(9729, false)]
    [InlineData(9984, true)]
    [InlineData(9985, true)]
    [InlineData(9986, true)]
    [InlineData(9987, true)]
    public void GltfModelImportPlanner_MapsSamplerMipRequirements(
        int minFilter,
        bool generateMipMaps)
    {
        using var workspace = TestWorkspace.Create();
        string gltfPath = workspace.Write("Assets/Sampled.gltf", $$"""
            {
              "asset": { "version": "2.0" },
              "materials": [
                {
                  "pbrMetallicRoughness": {
                    "baseColorTexture": { "index": 0 }
                  }
                }
              ],
              "textures": [
                { "source": 0, "sampler": 0 }
              ],
              "images": [
                { "uri": "BaseColor.png" }
              ],
              "samplers": [
                { "minFilter": {{minFilter}} }
              ]
            }
            """);

        GltfModelImportPlan plan = GltfModelImportPlanner.CreatePlan(
            gltfPath,
            Guid.Parse("47474747-5858-6969-7a7a-8b8b8b8b8b8b"),
            "com.arisen.test");

        GltfImportedTextureRef texture = Assert.Single(plan.Materials).BaseColorTexture!;
        Assert.Equal(generateMipMaps, texture.GenerateMipMaps);
    }

    [Fact]
    public void GltfModelImportPlanner_AcceptsBlendForTransparentRendering()
    {
        using var workspace = TestWorkspace.Create();
        var gltfPath = workspace.Write("Assets/Transparent.gltf", """
            {
              "asset": { "version": "2.0" },
              "materials": [
                {
                  "name": "Transparent",
                  "alphaMode": "BLEND"
                }
              ]
            }
            """);

        var plan = GltfModelImportPlanner.CreatePlan(
            gltfPath,
            Guid.Parse("abababab-cdcd-efef-0101-232323232323"),
            "com.arisen.test");
        var material = Assert.Single(plan.Materials);

        Assert.Equal(GltfMaterialAlphaMode.Blend, material.AlphaMode);
        Assert.Equal(MaterialPbrDefaults.AlphaCutoff, material.AlphaCutoff);
        Assert.DoesNotContain(
            plan.Warnings,
            warning => warning.Contains("alphaMode", StringComparison.Ordinal));
    }

    [Fact]
    public void GltfModelImportEmitter_MapsBlendToTransparentRenderState()
    {
        using var workspace = TestWorkspace.Create();
        var sourceGuid = Guid.Parse("cdcdcdcd-abab-4545-8989-010101010101");
        var shaderGuid = Guid.Parse("34343434-5656-7878-9090-abababababab");
        var gltfPath = workspace.Write("Assets/Transparent.gltf", """
            {
              "asset": { "version": "2.0" },
              "materials": [
                {
                  "name": "WindowGlass",
                  "alphaMode": "BLEND",
                  "pbrMetallicRoughness": {
                    "baseColorFactor": [0.6, 0.8, 1.0, 0.35]
                  }
                }
              ]
            }
            """);
        var shaderPath = workspace.Write("Assets/StandardLit.shader", """
            Shader "Tests/StandardLit"
            {
                SubShader
                {
                    Pass
                    {
                        Cull Back
                        HLSLPROGRAM
                        #pragma vertex VSMain
                        #pragma fragment PSMain
                        float4 VSMain() : SV_Position { return 0; }
                        float4 PSMain() : SV_Target0 { return 1; }
                        ENDHLSL
                    }
                }
            }
            """);

        var plan = GltfModelImportPlanner.CreatePlan(gltfPath, sourceGuid, "com.arisen.test");
        var result = GltfModelImportEmitter.Emit(
            plan,
            gltfPath,
            Path.Combine(workspace.Root, "Assets", "Generated"),
            new GltfModelImportEmissionSettings(shaderGuid, "Tests/StandardLit", "Transparent"));

        Assert.Empty(result.Warnings);
        var materialPath = Assert.Single(result.MaterialPaths);
        var materialMetadata = SerializationUtil.Deserialize<AssetMetadata>(
            materialPath + ".meta",
            serializeIfNotExist: false);
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        db.AddAsset(shaderGuid, ShaderAssetCooker.ShaderSourceAssetType, shaderPath);
        db.AddAsset(materialMetadata.Guid, "Material", materialPath);

        var material = MaterialAssetLoader.LoadSource(db, materialMetadata.Guid);
        var renderQueue = RenderQueuePolicy.Resolve(
            material.RenderState,
            material.Shader.VariantKeywords);

        Assert.True(material.RenderState.BlendEnabled);
        Assert.Equal(EBlendFactor.BLEND_FACTOR_SRC_ALPHA, material.RenderState.SrcColorBlendFactor);
        Assert.Equal(EBlendFactor.BLEND_FACTOR_ONE_MINUS_SRC_ALPHA, material.RenderState.DstColorBlendFactor);
        Assert.Equal(EBlendOp.BLEND_OP_ADD, material.RenderState.ColorBlendOp);
        Assert.Equal(RenderQueueClass.Transparent, renderQueue.Class);
        Assert.Equal(new Vector4(0.6f, 0.8f, 1.0f, 0.35f), material.Vector4Properties.Single(
            property => property.Name == MaterialPropertySlots.BaseColorFactor).Value);
    }

    [Fact]
    public void GltfModelImportEmitter_WritesGeneratedMaterialsAndMetadata()
    {
        using var workspace = TestWorkspace.Create();
        var sourceGuid = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        var shaderGuid = Guid.Parse("22222222-3333-4444-5555-666666666666");
        var baseColorPath = workspace.WriteBinary("Assets/BaseColor.png", CreateTinyPng());
        var normalPath = workspace.Write("Assets/Normal.ppm", "P3\n1 1\n255\n128 128 255\n");
        var emissivePath = workspace.WriteBinary("Assets/Emissive.png", CreateTinyPng());
        var packedPath = workspace.Write("Assets/Packed.ppm", "P3\n1 1\n255\n128 64 192\n");
        var gltfPath = workspace.Write("Assets/Robot.gltf", $$"""
            {
              "asset": { "version": "2.0" },
              "materials": [
                {
                  "name": "PaintedMetal",
                  "alphaMode": "MASK",
                  "alphaCutoff": 0.42,
                  "pbrMetallicRoughness": {
                    "baseColorFactor": [0.25, 0.5, 0.75, 1.0],
                    "baseColorTexture": { "index": 0 },
                    "metallicRoughnessTexture": {
                      "index": 3,
                      "extensions": {
                        "KHR_texture_transform": {
                          "offset": [0.25, 0.5],
                          "scale": [2.0, 0.75],
                          "rotation": 0.3
                        }
                      }
                    },
                    "metallicFactor": 0.8,
                    "roughnessFactor": 0.35
                  },
                  "emissiveFactor": [0.05, 0.06, 0.07],
                  "emissiveTexture": { "index": 2 },
                  "normalTexture": { "index": 1 },
                  "occlusionTexture": { "index": 4, "strength": 0.65 }
                }
              ],
              "textures": [
                { "source": 0 },
                { "source": 1 },
                { "source": 2 },
                { "source": 3, "sampler": 0 },
                { "source": 3, "sampler": 1 }
              ],
              "images": [
                { "uri": "{{Path.GetFileName(baseColorPath)}}"},
                { "uri": "{{Path.GetFileName(normalPath)}}"},
                { "uri": "{{Path.GetFileName(emissivePath)}}"},
                { "uri": "{{Path.GetFileName(packedPath)}}"}
              ],
              "samplers": [
                {
                  "magFilter": 9728,
                  "minFilter": 9987,
                  "wrapS": 33648,
                  "wrapT": 33071
                },
                {
                  "magFilter": 9729,
                  "minFilter": 9984,
                  "wrapS": 10497,
                  "wrapT": 10497
                }
              ]
            }
            """);
        var shaderPath = workspace.Write("Assets/StandardLit.shader", """
            Shader "Tests/StandardLit"
            {
                MaterialContract
                {
                    Texture2D BaseColor
                    Texture2D MetallicRoughness
                    Texture2D Occlusion
                    Scalar MetallicFactor
                    Scalar RoughnessFactor
                    Scalar OcclusionStrength
                    Scalar AlphaCutoff
                    Vector4 BaseColorFactor
                }

                SubShader
                {
                    Pass
                    {
                        HLSLPROGRAM
                        #pragma vertex VSMain
                        #pragma fragment PSMain
                        #pragma shader_feature USE_NORMAL_MAP
                        #pragma shader_feature ALPHA_TEST
                        float4 VSMain() : SV_Position { return 0; }
                        float4 PSMain() : SV_Target0 { return 1; }
                        ENDHLSL
                    }
                }
            }
            """);

        var plan = GltfModelImportPlanner.CreatePlan(gltfPath, sourceGuid, "com.arisen.test");
        var result = GltfModelImportEmitter.Emit(
            plan,
            gltfPath,
            Path.Combine(workspace.Root, "Assets", "Generated"),
            new GltfModelImportEmissionSettings(shaderGuid, "Tests/StandardLit", "Robot"));

        var materialPath = Assert.Single(result.MaterialPaths);
        Assert.Equal(4, result.TexturePaths.Count);
        Assert.Empty(result.Warnings);
        Assert.True(File.Exists(materialPath));
        Assert.True(File.Exists(materialPath + ".meta"));

        var materialMetadata = SerializationUtil.Deserialize<AssetMetadata>(materialPath + ".meta", serializeIfNotExist: false);
        Assert.Equal(GeneratedAssetIdentity.CreateChildGuid(sourceGuid, "com.arisen.test", "material", "materials/0"), materialMetadata.Guid);
        Assert.Equal("Material", materialMetadata.AssetType);
        Assert.Equal("GltfMaterialImporter", materialMetadata.Importer);
        Assert.NotNull(materialMetadata.Generated);
        Assert.Equal(sourceGuid, materialMetadata.Generated.SourceGuid);
        Assert.Equal("materials/0", materialMetadata.Generated.ChildKey);

        foreach (var texturePath in result.TexturePaths)
        {
            Assert.True(File.Exists(texturePath));
            var textureMetadata = SerializationUtil.Deserialize<AssetMetadata>(texturePath + ".meta", serializeIfNotExist: false);
            Assert.Equal("Texture2D", textureMetadata.AssetType);
            Assert.Equal("GltfTextureImporter", textureMetadata.Importer);
            Assert.NotNull(textureMetadata.Generated);
            Assert.Equal(sourceGuid, textureMetadata.Generated.SourceGuid);
        }

        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        db.AddAsset(shaderGuid, ShaderAssetCooker.ShaderSourceAssetType, shaderPath);
        db.AddAsset(materialMetadata.Guid, "Material", materialPath);
        foreach (var texturePath in result.TexturePaths)
        {
            var textureMetadata = SerializationUtil.Deserialize<AssetMetadata>(texturePath + ".meta", serializeIfNotExist: false);
            db.AddAsset(textureMetadata.Guid, "Texture2D", texturePath);
        }

        var loadedMaterial = MaterialAssetLoader.LoadSource(db, materialMetadata.Guid);
        Assert.Equal("PaintedMetal", loadedMaterial.Name);
        Assert.Equal(shaderGuid, loadedMaterial.Shader.Guid);
        Assert.Equal(new[] { "ALPHA_TEST", "USE_NORMAL_MAP" }, loadedMaterial.Shader.VariantKeywords);
        Assert.Contains(
            ".kw-ALPHA_TEST-USE_NORMAL_MAP.PSMain",
            loadedMaterial.Shader.Variant.GetCookedVariant(
                "PSMain",
                loadedMaterial.Shader.VariantKeywords),
            StringComparison.Ordinal);
        Assert.Equal(5, loadedMaterial.Texture2DRefs.Count);
        Assert.Contains(loadedMaterial.Texture2DRefs, texture => texture.Name == MaterialTextureSlots.BaseColor);
        Assert.Contains(loadedMaterial.Texture2DRefs, texture => texture.Name == MaterialTextureSlots.Normal);
        Assert.Contains(loadedMaterial.Texture2DRefs, texture => texture.Name == MaterialTextureSlots.Emissive);
        Assert.Contains(loadedMaterial.Texture2DRefs, texture => texture.Name == MaterialTextureSlots.MetallicRoughness);
        Assert.Contains(loadedMaterial.Texture2DRefs, texture => texture.Name == MaterialTextureSlots.Occlusion);
        Assert.Equal(Texture2DSourceFormat.ImageFile, loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.BaseColor).Texture.SourceFormat);
        Assert.Equal(Texture2DSourceFormat.PpmP3, loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.Normal).Texture.SourceFormat);
        Assert.Equal(Texture2DSourceFormat.ImageFile, loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.Emissive).Texture.SourceFormat);
        Assert.Equal(Texture2DSourceFormat.PpmP3, loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.MetallicRoughness).Texture.SourceFormat);
        Assert.Equal(Texture2DSourceFormat.PpmP3, loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.Occlusion).Texture.SourceFormat);
        Assert.Equal(Texture2DColorSpace.SRgb, loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.BaseColor).Texture.Variant.ColorSpace);
        Assert.Equal(Texture2DColorSpace.Linear, loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.Normal).Texture.Variant.ColorSpace);
        Assert.Equal(Texture2DColorSpace.SRgb, loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.Emissive).Texture.Variant.ColorSpace);
        var metallicRoughness = loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.MetallicRoughness);
        var occlusion = loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.Occlusion);
        Assert.Equal(Texture2DColorSpace.Linear, metallicRoughness.Texture.Variant.ColorSpace);
        Assert.Equal(Texture2DColorSpace.Linear, occlusion.Texture.Variant.ColorSpace);
        Assert.All(loadedMaterial.Texture2DRefs, texture =>
            Assert.True(texture.Texture.Variant.GenerateMipMaps));
        Assert.Equal(metallicRoughness.Texture.Guid, occlusion.Texture.Guid);
        Assert.Equal(
            new MaterialTextureSamplerSettings(
                MaterialTextureFilter.Linear,
                MaterialTextureFilter.Nearest,
                MaterialTextureMipmapMode.Linear,
                MaterialTextureWrapMode.MirroredRepeat,
                MaterialTextureWrapMode.ClampToEdge),
            metallicRoughness.ResolvedSampler);
        Assert.Equal(
            new MaterialTextureSamplerSettings(
                MaterialTextureFilter.Nearest,
                MaterialTextureFilter.Linear,
                MaterialTextureMipmapMode.Nearest,
                MaterialTextureWrapMode.Repeat,
                MaterialTextureWrapMode.Repeat),
            occlusion.ResolvedSampler);
        Assert.Equal(
            new MaterialTextureTransform(
                new Vector2(0.25f, 0.5f),
                new Vector2(2.0f, 0.75f),
                0.3f,
                0),
            metallicRoughness.ResolvedTransform);
        Assert.Equal(MaterialTextureTransform.Identity, occlusion.ResolvedTransform);
        Assert.Equal(0.8f, loadedMaterial.ScalarProperties.Single(property => property.Name == MaterialPropertySlots.MetallicFactor).Value);
        Assert.Equal(0.35f, loadedMaterial.ScalarProperties.Single(property => property.Name == MaterialPropertySlots.RoughnessFactor).Value);
        Assert.Equal(0.65f, loadedMaterial.ScalarProperties.Single(property => property.Name == MaterialPropertySlots.OcclusionStrength).Value);
        Assert.Equal(0.42f, loadedMaterial.ScalarProperties.Single(property => property.Name == MaterialPropertySlots.AlphaCutoff).Value);
        Assert.Equal(
            new Vector4(0.25f, 0.5f, 0.75f, 1.0f),
            loadedMaterial.Vector4Properties.Single(property => property.Name == MaterialPropertySlots.BaseColorFactor).Value);
        Assert.Equal(
            new Vector4(0.05f, 0.06f, 0.07f, 1.0f),
            loadedMaterial.Vector4Properties.Single(property => property.Name == MaterialPropertySlots.EmissiveFactor).Value);

        var cookedMaterial = MaterialAssetCooker.LoadOrCook(db, materialMetadata.Guid);
        Assert.True(cookedMaterial.IsValid);
        Assert.Equal(
            new[] { "ALPHA_TEST", "USE_NORMAL_MAP" },
            cookedMaterial.Asset.Shader.VariantKeywords);
        Assert.Equal(
            loadedMaterial.Shader.GetVariantIdentity(),
            cookedMaterial.Asset.Shader.GetVariantIdentity());
    }

    [Fact]
    public void GltfModelImportEmitter_WritesGeneratedSceneAndMeshChildren()
    {
        using var workspace = TestWorkspace.Create();
        var sourceGuid = Guid.Parse("eeeeeeee-ffff-0000-1111-222222222222");
        var shaderGuid = Guid.Parse("55555555-6666-7777-8888-999999999999");
        var glbPath = workspace.WriteBinary("Assets/SceneTriangle.glb", CreateGltfSceneTriangleGlb(1.25f));

        var plan = GltfModelImportPlanner.CreatePlan(glbPath, sourceGuid, "com.arisen.test");
        var result = GltfModelImportEmitter.Emit(
            plan,
            glbPath,
            Path.Combine(workspace.Root, "Assets", "Generated"),
            new GltfModelImportEmissionSettings(shaderGuid, "Tests/StandardLit", "SceneTriangle"));

        var scenePath = Assert.Single(result.ScenePaths);
        var meshPath = Assert.Single(result.MeshPaths);
        var materialPath = Assert.Single(result.MaterialPaths);
        Assert.Empty(result.TexturePaths);
        Assert.Empty(result.Warnings);
        Assert.True(File.Exists(scenePath));
        Assert.True(File.Exists(meshPath));

        var sceneMetadata = SerializationUtil.Deserialize<AssetMetadata>(scenePath + ".meta", serializeIfNotExist: false);
        var meshMetadata = SerializationUtil.Deserialize<AssetMetadata>(meshPath + ".meta", serializeIfNotExist: false);
        var materialMetadata = SerializationUtil.Deserialize<AssetMetadata>(materialPath + ".meta", serializeIfNotExist: false);

        Assert.Equal("Scene", sceneMetadata.AssetType);
        Assert.Equal("GltfSceneImporter", sceneMetadata.Importer);
        Assert.NotNull(sceneMetadata.Generated);
        Assert.Equal(sourceGuid, sceneMetadata.Generated.SourceGuid);
        Assert.Equal("scenes/0", sceneMetadata.Generated.ChildKey);

        Assert.Equal("Mesh", meshMetadata.AssetType);
        Assert.Equal("GltfMeshImporter", meshMetadata.Importer);
        Assert.NotNull(meshMetadata.Generated);
        Assert.Equal(sourceGuid, meshMetadata.Generated.SourceGuid);
        Assert.Equal("meshes/0", meshMetadata.Generated.ChildKey);

        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        db.AddAsset(sceneMetadata.Guid, "Scene", scenePath);
        db.AddAsset(meshMetadata.Guid, "Mesh", meshPath);
        db.AddAsset(materialMetadata.Guid, "Material", materialPath);

        var inspection = SceneAssetLoader.InspectScene(
            db,
            new AssetRef<SceneSourceAsset>(sceneMetadata.Guid, "Scene", "com.arisen.test"));
        Assert.True(inspection.Success, inspection.Diagnostic);
        Assert.Equal("GeneratedShowcase", inspection.SceneName);
        Assert.Equal(1, inspection.EntityCount);
        Assert.Equal(1, inspection.MeshRendererCount);

        var entity = Assert.Single(inspection.Entities);
        Guid expectedEntityGuid = GeneratedAssetIdentity.CreateChildGuid(
            sourceGuid,
            "com.arisen.test",
            "scene-entity",
            "scenes/0/nodes/0/primitives/0");
        Assert.Equal(expectedEntityGuid, entity.AuthoringGuid);
        Assert.Equal("TriangleNode", entity.Name);
        Assert.Equal(new Vector3(2.0f, 3.0f, 4.0f), entity.Transform.Position);
        Assert.Equal(new Vector3(2.0f, 2.0f, 2.0f), entity.Transform.Scale);
        Assert.NotNull(entity.MeshRenderer);
        Assert.Equal(meshMetadata.Guid, entity.MeshRenderer.Mesh.Guid);
        Assert.True(entity.MeshRenderer.Mesh.IsResolved, entity.MeshRenderer.Mesh.Diagnostic);
        Assert.Equal(materialMetadata.Guid, entity.MeshRenderer.Material.Guid);
        Assert.True(entity.MeshRenderer.Material.IsResolved, entity.MeshRenderer.Material.Diagnostic);
        Assert.Equal(0, entity.MeshRenderer.FirstSubmeshIndex);
        Assert.Equal(1, entity.MeshRenderer.SubmeshCount);

        string firstGeneratedSource = File.ReadAllText(scenePath);
        GltfModelImportEmissionResult repeated = GltfModelImportEmitter.Emit(
            plan,
            glbPath,
            Path.Combine(workspace.Root, "Assets", "Generated"),
            new GltfModelImportEmissionSettings(
                shaderGuid,
                "Tests/StandardLit",
                "SceneTriangle"));
        Assert.Equal(scenePath, Assert.Single(repeated.ScenePaths));
        Assert.Equal(firstGeneratedSource, File.ReadAllText(scenePath));

        var mesh = new MeshAsset(
            meshMetadata.Guid,
            "Tests/GeneratedSceneTriangle",
            MeshVariantKey.Default,
            MeshSourceFormat.GltfBinary);
        var cooked = MeshAssetCooker.LoadOrCook(db, mesh);

        Assert.True(cooked.IsValid);
        Assert.Equal(new Vector3(0, 0, 0), cooked.Bounds.Min);
        Assert.Equal(new Vector3(1.25f, 1.25f, 0), cooked.Bounds.Max);
        Assert.Equal(1u, cooked.SubmeshCount);
    }

    [Fact]
    public void ModelSourceReimporter_ReimportsToResolvedOutputRootAndReportsOrphans()
    {
        using var workspace = TestWorkspace.Create();
        var sourceGuid = Guid.Parse("12121212-3434-5656-7878-909090909090");
        var shaderGuid = Guid.Parse("23232323-4545-6767-8989-a0a0a0a0a0a0");
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var sourceAsset = CreateModelSourceAsset(
            workspace,
            db,
            sourceGuid,
            shaderGuid,
            "Assets/Generated/Robot");

        var result = ModelSourceReimporter.Reimport(sourceAsset);

        Assert.Equal(Path.Combine(workspace.Root, "Assets", "Generated", "Robot"), result.OutputRoot);
        Assert.Single(result.Emission.ScenePaths);
        Assert.Single(result.Emission.MeshPaths);
        Assert.Single(result.Emission.MaterialPaths);
        Assert.Empty(result.Emission.TexturePaths);
        Assert.Empty(result.OrphanedGeneratedChildren);
        Assert.Empty(result.ForeignGeneratedChildren);

        foreach (var emittedPath in result.Emission.ScenePaths
                     .Concat(result.Emission.MeshPaths)
                     .Concat(result.Emission.MaterialPaths))
        {
            Assert.True(File.Exists(emittedPath));
            var metadata = SerializationUtil.Deserialize<AssetMetadata>(emittedPath + ".meta", serializeIfNotExist: false);
            Assert.NotNull(metadata.Generated);
            Assert.Equal(sourceGuid, metadata.Generated.SourceGuid);
            Assert.Contains(result.GeneratedChildGuids, guid => guid == metadata.Guid);
        }

        var orphanMetaPath = Path.Combine(result.OutputRoot, "Materials", "OldMaterial.arismaterial.meta");
        SerializationUtil.Serialize(
            new AssetMetadata
            {
                Guid = Guid.Parse("34343434-5656-7878-9090-b1b1b1b1b1b1"),
                AssetType = "Material",
                Importer = "GltfMaterialImporter",
                Generated = new GeneratedAssetMetadata
                {
                    SourceGuid = sourceGuid,
                    SourcePackageId = "com.arisen.test",
                    ChildKind = "material",
                    ChildKey = "materials/99",
                    GeneratedByImporter = "GltfMaterialImporter"
                }
            },
            orphanMetaPath);

        var inspection = ModelSourceReimporter.InspectGeneratedOutput(sourceAsset, result.Model, result.Plan);
        var orphan = Assert.Single(inspection.OrphanedGeneratedChildren);
        Assert.Equal("materials/99", orphan.ChildKey);

        var secondResult = ModelSourceReimporter.Reimport(sourceAsset);
        Assert.Single(secondResult.OrphanedGeneratedChildren);
        Assert.True(File.Exists(orphanMetaPath));
    }

    [Fact]
    public void ModelReimportValidationFixture_ReimportsIndexesAndLoadsGeneratedScene()
    {
        using var workspace = TestWorkspace.Create();
        const string packageId = "com.arisen.test";
        var sourceGuid = Guid.Parse("13572468-2468-1357-8642-abcdefabcdef");
        var shaderGuid = Guid.Parse("24681357-1357-2468-9753-bcdefabcdefa");
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var sourceAsset = CreateModelReimportValidationSourceAsset(
            workspace,
            db,
            sourceGuid,
            shaderGuid,
            packageId);

        var firstResult = ModelSourceReimporter.Reimport(sourceAsset);

        Assert.Empty(firstResult.Emission.Warnings);
        Assert.Empty(firstResult.OrphanedGeneratedChildren);
        Assert.Empty(firstResult.ForeignGeneratedChildren);
        Assert.Single(firstResult.Emission.ScenePaths);
        Assert.Single(firstResult.Emission.MeshPaths);
        Assert.Single(firstResult.Emission.MaterialPaths);
        Assert.Equal(3, firstResult.Emission.TexturePaths.Count);

        var emittedPaths = GetEmittedSourcePaths(firstResult);
        Assert.Equal(6, emittedPaths.Length);
        Assert.Equal(firstResult.Plan.GeneratedChildren.Count, emittedPaths.Length);
        var indexedMetadata = IndexGeneratedAssets(db, firstResult, emittedPaths);
        var outputPrefix = Path.GetFullPath(firstResult.OutputRoot) + Path.DirectorySeparatorChar;

        for (int i = 0; i < emittedPaths.Length; i++)
        {
            var emittedPath = Path.GetFullPath(emittedPaths[i]);
            Assert.True(
                emittedPath.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase),
                $"Generated source escaped its output root: {emittedPath}");
            Assert.False(
                emittedPath.Split(
                        new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Any(segment => string.Equals(segment, ".arisen", StringComparison.OrdinalIgnoreCase)),
                $"Generated source was written beneath .arisen: {emittedPath}");

            var metadata = indexedMetadata[i];
            var plannedChild = Assert.Single(
                firstResult.Plan.GeneratedChildren,
                child => child.Metadata.Guid == metadata.Guid);
            Assert.Equal(plannedChild.Metadata.AssetType, metadata.AssetType);
            Assert.Equal(plannedChild.Metadata.Importer, metadata.Importer);
            Assert.NotNull(metadata.Generated);
            Assert.Equal(sourceGuid, metadata.Generated.SourceGuid);
            Assert.Equal(packageId, metadata.Generated.SourcePackageId);
            Assert.Equal(plannedChild.Kind, metadata.Generated.ChildKind);
            Assert.Equal(plannedChild.Key, metadata.Generated.ChildKey);
            Assert.Equal(
                plannedChild.Metadata.Generated!.GeneratedByImporter,
                metadata.Generated.GeneratedByImporter);
        }

        foreach (var child in firstResult.Plan.GeneratedChildren)
        {
            Assert.True(db.TryGetAsset(child.Metadata.Guid, out var indexedChild));
            Assert.Equal(child.Metadata.AssetType, indexedChild.AssetType);
            Assert.Equal(packageId, indexedChild.PackageId);
        }

        var materialMetadata = indexedMetadata.Single(
            metadata => string.Equals(metadata.AssetType, "Material", StringComparison.OrdinalIgnoreCase));
        var material = MaterialAssetLoader.LoadSource(db, materialMetadata.Guid);
        Assert.Equal(new[] { "USE_NORMAL_MAP" }, material.Shader.VariantKeywords);
        Assert.Equal(4, material.Texture2DRefs.Count);
        Assert.Equal(3, material.Texture2DRefs.Select(binding => binding.Texture.Guid).Distinct().Count());
        Assert.Equal(
            material.Texture2DRefs.Single(binding => binding.Name == MaterialTextureSlots.MetallicRoughness).Texture.Guid,
            material.Texture2DRefs.Single(binding => binding.Name == MaterialTextureSlots.Occlusion).Texture.Guid);
        foreach (var binding in material.Texture2DRefs)
        {
            var textureRef = new AssetRef<Texture2DSourceAsset>(
                binding.Texture.Guid,
                "Texture2D",
                packageId);
            Assert.True(
                db.TryGetAsset(textureRef, out var textureAsset),
                $"Generated material texture '{binding.Name}' did not resolve: {binding.Texture.Guid:D}");
            Assert.Equal("Texture2D", textureAsset.AssetType);
            Assert.True(File.Exists(textureAsset.SourcePath));
        }

        var sceneMetadata = indexedMetadata.Single(
            metadata => string.Equals(metadata.AssetType, "Scene", StringComparison.OrdinalIgnoreCase));
        var meshMetadata = indexedMetadata.Single(
            metadata => string.Equals(metadata.AssetType, "Mesh", StringComparison.OrdinalIgnoreCase));
        var sceneRef = new AssetRef<SceneSourceAsset>(sceneMetadata.Guid, "Scene", packageId);
        var inspection = SceneAssetLoader.InspectScene(db, sceneRef);
        Assert.True(inspection.Success, inspection.Diagnostic);
        Assert.Equal("ReimportValidationScene", inspection.SceneName);
        var inspectedEntity = Assert.Single(inspection.Entities);
        Assert.NotNull(inspectedEntity.MeshRenderer);
        Assert.Equal(meshMetadata.Guid, inspectedEntity.MeshRenderer.Mesh.Guid);
        Assert.Equal(materialMetadata.Guid, inspectedEntity.MeshRenderer.Material.Guid);
        Assert.True(inspectedEntity.MeshRenderer.Mesh.IsResolved, inspectedEntity.MeshRenderer.Mesh.Diagnostic);
        Assert.True(inspectedEntity.MeshRenderer.Material.IsResolved, inspectedEntity.MeshRenderer.Material.Diagnostic);

        var entityManager = new EntityManager();
        var loadResult = SceneAssetLoader.LoadScene(db, sceneRef, entityManager);
        Assert.True(loadResult.Success, loadResult.Diagnostic);
        Assert.Equal(1, loadResult.EntityCount);
        Assert.Equal(1, loadResult.MeshRendererCount);
        var renderer = entityManager.GetPool<MeshRendererComponent>().GetRawComponentArray()[0];
        Assert.Equal(meshMetadata.Guid, renderer.MeshGuid);
        Assert.Equal(materialMetadata.Guid, renderer.MaterialGuid);

        var firstSidecars = emittedPaths.ToDictionary(
            path => Path.GetRelativePath(firstResult.OutputRoot, path),
            path => File.ReadAllText(path + ".meta"),
            StringComparer.OrdinalIgnoreCase);
        var secondResult = ModelSourceReimporter.Reimport(sourceAsset);
        var secondPaths = GetEmittedSourcePaths(secondResult);

        Assert.Equal(firstResult.GeneratedChildGuids, secondResult.GeneratedChildGuids);
        Assert.Empty(secondResult.OrphanedGeneratedChildren);
        Assert.Empty(secondResult.ForeignGeneratedChildren);
        Assert.Equal(
            firstSidecars.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
            secondPaths
                .Select(path => Path.GetRelativePath(secondResult.OutputRoot, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        foreach (var secondPath in secondPaths)
        {
            var relativePath = Path.GetRelativePath(secondResult.OutputRoot, secondPath);
            Assert.Equal(firstSidecars[relativePath], File.ReadAllText(secondPath + ".meta"));
        }

        var foreignMetaPath = Path.Combine(
            secondResult.OutputRoot,
            "Materials",
            "Foreign.arismaterial.meta");
        SerializationUtil.Serialize(
            new AssetMetadata
            {
                Guid = Guid.Parse("35792468-4682-5791-a246-cdefabcdefab"),
                AssetType = "Material",
                Importer = "GltfMaterialImporter",
                Generated = new GeneratedAssetMetadata
                {
                    SourceGuid = Guid.Parse("46823579-5791-6824-b357-defabcdefabc"),
                    SourcePackageId = "com.arisen.foreign",
                    ChildKind = "material",
                    ChildKey = "materials/0",
                    GeneratedByImporter = "GltfMaterialImporter"
                }
            },
            foreignMetaPath);

        var foreignInspection = ModelSourceReimporter.InspectGeneratedOutput(
            secondResult.OutputRoot,
            sourceGuid,
            secondResult.Plan);
        Assert.Single(foreignInspection.ForeignGeneratedChildren);
        var exception = Assert.Throws<InvalidOperationException>(
            () => ModelSourceReimporter.Reimport(sourceAsset));
        Assert.Contains("another source GUID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelSourceReimporter_MaterialAndTextureChangesInvalidateCookedOutputs()
    {
        using var workspace = TestWorkspace.Create();
        var sourceGuid = Guid.Parse("9a9a9a9a-b1b1-c2c2-d3d3-e4e4e4e4e4e4");
        var shaderGuid = Guid.Parse("abababab-c2c2-d3d3-e4e4-f5f5f5f5f5f5");
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var sourceAsset = CreateTexturedModelSourceAsset(workspace, db, sourceGuid, shaderGuid, 0.25f);

        var firstResult = ModelSourceReimporter.Reimport(sourceAsset);
        var materialPath = Assert.Single(firstResult.Emission.MaterialPaths);
        var generatedTexturePath = Assert.Single(firstResult.Emission.TexturePaths);
        var materialMetadata = SerializationUtil.Deserialize<AssetMetadata>(
            materialPath + ".meta",
            serializeIfNotExist: false);
        var textureMetadata = SerializationUtil.Deserialize<AssetMetadata>(
            generatedTexturePath + ".meta",
            serializeIfNotExist: false);
        db.AddAsset(materialMetadata.Guid, materialMetadata.AssetType, materialPath);
        db.AddAsset(textureMetadata.Guid, textureMetadata.AssetType, generatedTexturePath);

        var firstMaterial = MaterialAssetLoader.LoadSource(db, materialMetadata.Guid);
        var firstCookedMaterial = MaterialAssetCooker.LoadOrCook(db, materialMetadata.Guid);
        var firstTexture = Assert.Single(firstMaterial.Texture2DRefs).Texture;
        var firstCookedTexture = Texture2DAssetCooker.LoadOrCook(db, firstTexture);
        var firstPixels = Texture2DAssetCooker.GetPixelData(
            db.GetCookedAssetBytes(firstCookedTexture.Handle)).ToArray();
        Assert.Equal(0.25f, firstCookedMaterial.Asset.ScalarProperties.Single(
            property => property.Name == MaterialPropertySlots.RoughnessFactor).Value);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, firstPixels);

        var newestDependencyTime = DateTime.UtcNow.AddMinutes(10);
        File.SetLastWriteTimeUtc(materialPath, newestDependencyTime);
        File.SetLastWriteTimeUtc(materialPath + ".meta", newestDependencyTime);
        File.SetLastWriteTimeUtc(generatedTexturePath, newestDependencyTime.AddMinutes(-4));
        var firstDependencyStamp = AssetDependencyTracker.GetMaterialStamp(db, firstMaterial);
        File.SetLastWriteTimeUtc(generatedTexturePath, newestDependencyTime.AddMinutes(-3));
        var changedTextureDependencyStamp = AssetDependencyTracker.GetMaterialStamp(db, firstMaterial);
        Assert.NotEqual(firstDependencyStamp, changedTextureDependencyStamp);

        Assert.True(db.TryGetCookedArtifact(
            materialMetadata.Guid,
            firstCookedMaterial.Variant,
            out var materialArtifact));
        Assert.True(db.TryGetCookedArtifact(
            textureMetadata.Guid,
            firstCookedTexture.Variant,
            out var textureArtifact));
        File.SetLastWriteTimeUtc(materialArtifact.Path, newestDependencyTime.AddMinutes(10));
        File.SetLastWriteTimeUtc(textureArtifact.Path, newestDependencyTime.AddMinutes(10));

        db.Release(firstCookedMaterial.Handle);
        db.Release(firstCookedTexture.Handle);
        WriteTexturedModelGltf(workspace, 0.75f);
        workspace.Write("Assets/Models/TexturedRobot/Source/BaseColor.ppm", "P3\n1 1\n255\n0 0 255\n");

        var secondResult = ModelSourceReimporter.Reimport(sourceAsset);
        Assert.Equal(firstResult.GeneratedChildGuids, secondResult.GeneratedChildGuids);

        var reloadQueue = new RenderResourceReloadQueue();
        db.AssetChanged += reloadQueue.MarkDirty;
        var invalidatedGuids = ModelSourceReimporter.InvalidateCookedOutputs(
            db,
            sourceAsset,
            secondResult);

        Assert.Contains(sourceGuid, invalidatedGuids);
        Assert.Contains(materialMetadata.Guid, invalidatedGuids);
        Assert.Contains(textureMetadata.Guid, invalidatedGuids);
        Assert.False(db.TryGetCookedArtifact(materialMetadata.Guid, firstCookedMaterial.Variant, out _));
        Assert.False(db.TryGetCookedArtifact(textureMetadata.Guid, firstCookedTexture.Variant, out _));

        var dirtyGuids = reloadQueue.Drain();
        Assert.Contains(sourceGuid, dirtyGuids);
        Assert.Contains(materialMetadata.Guid, dirtyGuids);
        Assert.Contains(textureMetadata.Guid, dirtyGuids);

        var secondCookedMaterial = MaterialAssetCooker.LoadOrCook(db, materialMetadata.Guid);
        var secondTexture = Assert.Single(secondCookedMaterial.Asset.Texture2DRefs).Texture;
        var secondCookedTexture = Texture2DAssetCooker.LoadOrCook(db, secondTexture);
        var secondPixels = Texture2DAssetCooker.GetPixelData(
            db.GetCookedAssetBytes(secondCookedTexture.Handle)).ToArray();

        Assert.Equal(0.75f, secondCookedMaterial.Asset.ScalarProperties.Single(
            property => property.Name == MaterialPropertySlots.RoughnessFactor).Value);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, secondPixels);

        db.Release(secondCookedMaterial.Handle);
        db.Release(secondCookedTexture.Handle);
    }

    [Fact]
    public void AssetImporter_RenameMovePreservesModelRootAndGeneratedChildIdentity()
    {
        using var workspace = TestWorkspace.Create();
        var sourceGuid = Guid.Parse("bcbcbcbc-d3d3-e4e4-f5f5-161616161616");
        var shaderGuid = Guid.Parse("cdcdcdcd-e4e4-f5f5-0606-272727272727");
        const string packageId = "com.arisen.test";
        var assetsRoot = Path.Combine(workspace.Root, "Assets");
        workspace.WriteBinary(
            "Assets/Models/Shared/Robot.glb",
            CreateGltfSceneTriangleGlb(1.0f));
        var oldModelPath = workspace.Write("Assets/Models/Original/Robot.arismodel", $$"""
            Name: RenameableRobot
            Source:
              Path: ../Shared/Robot.glb
              Format: GltfBinary
            Import:
              OutputRoot: Assets/Generated/RenameableRobot
              SceneIndex: 0
              UnitScale: 1.0
              EmitTextures: false
            Shader:
              Guid: {{shaderGuid:D}}
              Name: Tests/StandardLit
            """);
        var oldMetaPath = oldModelPath + ".meta";
        SerializationUtil.Serialize(
            new AssetMetadata
            {
                Guid = sourceGuid,
                AssetType = ModelSourceAssetLoader.ModelAssetType,
                Importer = "ArisenModelImporter"
            },
            oldMetaPath);

        var oldAsset = new AssetRecord(
            sourceGuid,
            ModelSourceAssetLoader.ModelAssetType,
            oldModelPath,
            oldMetaPath,
            packageId);
        var oldModel = ModelSourceAssetLoader.LoadSource(oldAsset);
        var oldPlan = ModelSourceAssetLoader.CreateGltfPlan(oldAsset, oldModel);
        Assert.NotEmpty(oldPlan.GeneratedChildren);

        var databasePath = Path.Combine(workspace.Root, ".arisen", "Editor", "AssetDatabase.db");
        EditorAssetDatabase.Initialize(databasePath);
        try
        {
            var oldRelativePath = Path.GetRelativePath(workspace.Root, oldModelPath).Replace('\\', '/');
            EditorAssetDatabase.Instance.RegisterAsset(
                sourceGuid,
                oldRelativePath,
                ModelSourceAssetLoader.ModelAssetType,
                "ArisenModelImporter",
                packageId,
                0);

            var newModelPath = Path.Combine(
                assetsRoot,
                "Models",
                "Moved",
                "RobotRenamed.arismodel");
            Directory.CreateDirectory(Path.GetDirectoryName(newModelPath)!);
            File.Move(oldModelPath, newModelPath);

            // Simulate a watcher-created destination sidecar racing the rename event.
            SerializationUtil.Serialize(
                new AssetMetadata
                {
                    Guid = Guid.Parse("dededede-f5f5-0606-1717-383838383838"),
                    AssetType = ModelSourceAssetLoader.ModelAssetType,
                    Importer = "ArisenModelImporter"
                },
                newModelPath + ".meta");

            var changes = new List<AssetChangeEvent>();
            using var importer = new ArisenEditor.Core.Assets.AssetImporter(
                assetsRoot,
                workspace.Root,
                packageId);
            importer.AssetChanged += changes.Add;
            importer.ProcessRenamedFile(oldModelPath, newModelPath);

            var newRelativePath = Path.GetRelativePath(workspace.Root, newModelPath).Replace('\\', '/');
            Assert.False(EditorAssetDatabase.Instance.TryGetGuid(oldRelativePath, out _));
            Assert.True(EditorAssetDatabase.Instance.TryGetGuid(newRelativePath, out var registeredGuid));
            Assert.Equal(sourceGuid, registeredGuid);
            Assert.Equal(newRelativePath, EditorAssetDatabase.Instance.GetPath(sourceGuid));
            Assert.False(File.Exists(oldMetaPath));
            Assert.True(File.Exists(newModelPath + ".meta"));

            var movedMetadata = SerializationUtil.Deserialize<AssetMetadata>(
                newModelPath + ".meta",
                serializeIfNotExist: false);
            Assert.Equal(sourceGuid, movedMetadata.Guid);
            Assert.Equal(ModelSourceAssetLoader.ModelAssetType, movedMetadata.AssetType);
            Assert.Equal("ArisenModelImporter", movedMetadata.Importer);

            var change = Assert.Single(changes);
            Assert.Equal(AssetChangeKind.Renamed, change.Kind);
            Assert.Equal(sourceGuid, change.Guid);
            Assert.Equal(oldModelPath, change.PreviousSourcePath);
            Assert.Equal(newModelPath, change.SourcePath);

            var movedAsset = new AssetRecord(
                sourceGuid,
                ModelSourceAssetLoader.ModelAssetType,
                newModelPath,
                newModelPath + ".meta",
                packageId);
            var movedModel = ModelSourceAssetLoader.LoadSource(movedAsset);
            var movedPlan = ModelSourceAssetLoader.CreateGltfPlan(movedAsset, movedModel);

            Assert.Equal(oldModel.Guid, movedModel.Guid);
            Assert.Equal(oldModel.ResolvedSourcePath, movedModel.ResolvedSourcePath);
            Assert.Equal(
                oldPlan.GeneratedChildren.Select(child => child.Metadata.Guid),
                movedPlan.GeneratedChildren.Select(child => child.Metadata.Guid));

            var reimport = ModelSourceReimporter.Reimport(movedAsset);
            Assert.Equal(
                movedPlan.GeneratedChildren.Select(child => child.Metadata.Guid),
                reimport.GeneratedChildGuids);
            foreach (var emittedPath in reimport.Emission.ScenePaths
                         .Concat(reimport.Emission.MeshPaths)
                         .Concat(reimport.Emission.MaterialPaths)
                         .Concat(reimport.Emission.TexturePaths))
            {
                var generatedMetadata = SerializationUtil.Deserialize<AssetMetadata>(
                    emittedPath + ".meta",
                    serializeIfNotExist: false);
                Assert.NotNull(generatedMetadata.Generated);
                Assert.Equal(sourceGuid, generatedMetadata.Generated.SourceGuid);
            }
        }
        finally
        {
            EditorAssetDatabase.Instance.Dispose();
        }
    }

    [Fact]
    public void AssetImporter_QueuedDeleteForAtomicReplacementPublishesChange()
    {
        using var workspace = TestWorkspace.Create();
        var assetGuid = Guid.Parse("abababab-cdcd-efef-0101-232323232323");
        const string packageId = "com.arisen.test";
        var assetsRoot = Path.Combine(workspace.Root, "Assets");
        var sourcePath = workspace.Write("Assets/Atomic.arisenscene", "Version: 2\nName: Atomic\nEntities: []\n");
        SerializationUtil.Serialize(
            new AssetMetadata
            {
                Guid = assetGuid,
                AssetType = "Scene",
                Importer = "ArisenSceneImporter"
            },
            sourcePath + ".meta");
        File.AppendAllText(sourcePath + ".meta", "ImporterType: \n");

        var databasePath = Path.Combine(workspace.Root, ".arisen", "Editor", "AssetDatabase.db");
        EditorAssetDatabase.Initialize(databasePath);
        try
        {
            var relativePath = Path.GetRelativePath(workspace.Root, sourcePath).Replace('\\', '/');
            EditorAssetDatabase.Instance.RegisterAsset(
                assetGuid,
                relativePath,
                "Scene",
                "ArisenSceneImporter",
                packageId,
                0);

            var changes = new List<AssetChangeEvent>();
            using var importer = new ArisenEditor.Core.Assets.AssetImporter(
                assetsRoot,
                workspace.Root,
                packageId);
            importer.AssetChanged += changes.Add;
            importer.ProcessDeletedFile(sourcePath);

            var change = Assert.Single(changes);
            Assert.Equal(AssetChangeKind.Changed, change.Kind);
            Assert.Equal(assetGuid, change.Guid);
            Assert.True(File.Exists(sourcePath));
            Assert.True(File.Exists(sourcePath + ".meta"));
            Assert.DoesNotContain("ImporterType", File.ReadAllText(sourcePath + ".meta"), StringComparison.Ordinal);
            var runtimeMetadata = SerializationUtil.Deserialize<ArisenEngine.Core.Assets.AssetMetadata>(
                sourcePath + ".meta",
                serializeIfNotExist: false);
            Assert.Equal(assetGuid, runtimeMetadata.Guid);
            Assert.True(EditorAssetDatabase.Instance.TryGetGuid(relativePath, out var registeredGuid));
            Assert.Equal(assetGuid, registeredGuid);
        }
        finally
        {
            EditorAssetDatabase.Instance.Dispose();
        }
    }

    [Fact]
    public void AssetDatabase_PrunesMissingRenameSourceBeforeImporterScan()
    {
        using var workspace = TestWorkspace.Create();
        var assetGuid = Guid.Parse("bcbcbcbc-dede-f0f0-1212-343434343434");
        const string packageId = "com.arisen.test";
        var assetsRoot = Path.Combine(workspace.Root, "Assets");
        var currentPath = workspace.Write("Assets/Current.ppm", "P3\n1 1\n255\n255 255 255\n");
        SerializationUtil.Serialize(
            new AssetMetadata
            {
                Guid = assetGuid,
                AssetType = "Texture2D",
                Importer = "ImageTextureImporter"
            },
            currentPath + ".meta");

        var databasePath = Path.Combine(workspace.Root, ".arisen", "Editor", "AssetDatabase.db");
        EditorAssetDatabase.Initialize(databasePath);
        try
        {
            const string stalePath = "Assets/Previous.ppm";
            EditorAssetDatabase.Instance.RegisterAsset(
                assetGuid,
                stalePath,
                "Texture2D",
                "ImageTextureImporter",
                packageId,
                0);

            Assert.Equal(1, EditorAssetDatabase.Instance.PruneMissingAssets(workspace.Root));

            using var importer = new ArisenEditor.Core.Assets.AssetImporter(
                assetsRoot,
                workspace.Root,
                packageId);
            importer.Start();

            var currentRelativePath = Path.GetRelativePath(workspace.Root, currentPath).Replace('\\', '/');
            Assert.Equal(currentRelativePath, EditorAssetDatabase.Instance.GetPath(assetGuid));
            Assert.False(EditorAssetDatabase.Instance.TryGetGuid(stalePath, out _));
        }
        finally
        {
            EditorAssetDatabase.Instance.Dispose();
        }
    }

    [Fact]
    public void ModelSourceReimporter_RejectsUnsafeOrForeignGeneratedOutput()
    {
        using var workspace = TestWorkspace.Create();
        var sourceGuid = Guid.Parse("45454545-6767-8989-a0a0-c2c2c2c2c2c2");
        var shaderGuid = Guid.Parse("56565656-7878-9090-b1b1-d3d3d3d3d3d3");
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var sourceAsset = CreateModelSourceAsset(
            workspace,
            db,
            sourceGuid,
            shaderGuid,
            "Assets/Generated/Robot");
        var model = ModelSourceAssetLoader.LoadSource(sourceAsset);
        var plan = ModelSourceAssetLoader.CreateGltfPlan(sourceAsset, model);
        var outputRoot = ModelSourceReimporter.ValidateOutputRoot(sourceAsset, model);
        var foreignMetaPath = Path.Combine(outputRoot, "Materials", "Foreign.arismaterial.meta");

        SerializationUtil.Serialize(
            new AssetMetadata
            {
                Guid = Guid.Parse("67676767-8989-a0a0-b1b1-e4e4e4e4e4e4"),
                AssetType = "Material",
                Importer = "GltfMaterialImporter",
                Generated = new GeneratedAssetMetadata
                {
                    SourceGuid = Guid.Parse("78787878-9090-a1a1-b2b2-f5f5f5f5f5f5"),
                    SourcePackageId = "com.arisen.other",
                    ChildKind = "material",
                    ChildKey = "materials/0",
                    GeneratedByImporter = "GltfMaterialImporter"
                }
            },
            foreignMetaPath);

        var inspection = ModelSourceReimporter.InspectGeneratedOutput(outputRoot, sourceGuid, plan);
        Assert.Single(inspection.ForeignGeneratedChildren);
        var ex = Assert.Throws<InvalidOperationException>(() => ModelSourceReimporter.Reimport(sourceAsset));
        Assert.Contains("another source GUID", ex.Message, StringComparison.Ordinal);

        var unsafeSourceAsset = CreateModelSourceAsset(
            workspace,
            db,
            Guid.Parse("89898989-a0a0-b1b1-c2c2-060606060606"),
            shaderGuid,
            "Assets");
        var unsafeModel = ModelSourceAssetLoader.LoadSource(unsafeSourceAsset);
        var unsafeEx = Assert.Throws<InvalidOperationException>(
            () => ModelSourceReimporter.ValidateOutputRoot(unsafeSourceAsset, unsafeModel));
        Assert.Contains("child directory", unsafeEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GltfModelImportEmitter_WritesPerPrimitiveMaterialSceneEntities()
    {
        using var workspace = TestWorkspace.Create();
        var sourceGuid = Guid.Parse("abababab-cdcd-efef-0101-232323232323");
        var shaderGuid = Guid.Parse("67676767-8989-abab-cdcd-efefefefefef");
        var glbPath = workspace.WriteBinary("Assets/MultiPrimitive.glb", CreateGltfMultiPrimitiveSceneGlb(1.0f));

        var plan = GltfModelImportPlanner.CreatePlan(glbPath, sourceGuid, "com.arisen.test");
        var result = GltfModelImportEmitter.Emit(
            plan,
            glbPath,
            Path.Combine(workspace.Root, "Assets", "Generated"),
            new GltfModelImportEmissionSettings(shaderGuid, "Tests/StandardLit", "MultiPrimitive"));

        var scenePath = Assert.Single(result.ScenePaths);
        var meshPath = Assert.Single(result.MeshPaths);
        Assert.Equal(2, result.MaterialPaths.Count);
        Assert.Empty(result.TexturePaths);
        Assert.Empty(result.Warnings);

        var sceneMetadata = SerializationUtil.Deserialize<AssetMetadata>(scenePath + ".meta", serializeIfNotExist: false);
        var meshMetadata = SerializationUtil.Deserialize<AssetMetadata>(meshPath + ".meta", serializeIfNotExist: false);
        var materialMetadata0 = SerializationUtil.Deserialize<AssetMetadata>(result.MaterialPaths[0] + ".meta", serializeIfNotExist: false);
        var materialMetadata1 = SerializationUtil.Deserialize<AssetMetadata>(result.MaterialPaths[1] + ".meta", serializeIfNotExist: false);

        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        db.AddAsset(sceneMetadata.Guid, "Scene", scenePath);
        db.AddAsset(meshMetadata.Guid, "Mesh", meshPath);
        db.AddAsset(materialMetadata0.Guid, "Material", result.MaterialPaths[0]);
        db.AddAsset(materialMetadata1.Guid, "Material", result.MaterialPaths[1]);

        var inspection = SceneAssetLoader.InspectScene(
            db,
            new AssetRef<SceneSourceAsset>(sceneMetadata.Guid, "Scene", "com.arisen.test"));
        Assert.True(inspection.Success, inspection.Diagnostic);
        Assert.Equal("GeneratedMultiPrimitive", inspection.SceneName);
        Assert.Equal(2, inspection.EntityCount);
        Assert.Equal(2, inspection.MeshRendererCount);

        var primitive0 = Assert.Single(inspection.Entities, entity => entity.Name == "MultiMaterialNode_Primitive_0");
        var primitive1 = Assert.Single(inspection.Entities, entity => entity.Name == "MultiMaterialNode_Primitive_1");
        Assert.Equal(
            GeneratedAssetIdentity.CreateChildGuid(
                sourceGuid,
                "com.arisen.test",
                "scene-entity",
                "scenes/0/nodes/0/primitives/0"),
            primitive0.AuthoringGuid);
        Assert.Equal(
            GeneratedAssetIdentity.CreateChildGuid(
                sourceGuid,
                "com.arisen.test",
                "scene-entity",
                "scenes/0/nodes/0/primitives/1"),
            primitive1.AuthoringGuid);
        Assert.NotNull(primitive0.MeshRenderer);
        Assert.NotNull(primitive1.MeshRenderer);
        Assert.Equal(meshMetadata.Guid, primitive0.MeshRenderer.Mesh.Guid);
        Assert.Equal(meshMetadata.Guid, primitive1.MeshRenderer.Mesh.Guid);
        Assert.Equal(materialMetadata0.Guid, primitive0.MeshRenderer.Material.Guid);
        Assert.Equal(materialMetadata1.Guid, primitive1.MeshRenderer.Material.Guid);
        Assert.Equal(0, primitive0.MeshRenderer.FirstSubmeshIndex);
        Assert.Equal(1, primitive0.MeshRenderer.SubmeshCount);
        Assert.Equal(1, primitive1.MeshRenderer.FirstSubmeshIndex);
        Assert.Equal(1, primitive1.MeshRenderer.SubmeshCount);

        var mesh = new MeshAsset(
            meshMetadata.Guid,
            "Tests/GeneratedMultiPrimitive",
            MeshVariantKey.Default,
            MeshSourceFormat.GltfBinary);
        var cooked = MeshAssetCooker.LoadOrCook(db, mesh);
        var bytes = db.GetCookedAssetBytes(cooked.Handle);
        Span<MeshSubmesh> submeshes = stackalloc MeshSubmesh[checked((int)cooked.SubmeshCount)];
        MeshAssetCooker.ReadSubmeshes(bytes.Span, cooked, submeshes);

        Assert.True(cooked.IsValid);
        Assert.Equal(2u, cooked.SubmeshCount);
        Assert.Equal(0u, submeshes[0].FirstIndex);
        Assert.Equal(3u, submeshes[0].IndexCount);
        Assert.Equal(0u, submeshes[0].MaterialSlot);
        Assert.Equal(3u, submeshes[1].FirstIndex);
        Assert.Equal(3u, submeshes[1].IndexCount);
        Assert.Equal(1u, submeshes[1].MaterialSlot);
    }

    [Fact]
    public void Texture2DAssetCooker_DecodesImageFileSources()
    {
        using var workspace = TestWorkspace.Create();
        var textureGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var texturePath = workspace.WriteBinary("Assets/BaseColor.png", CreateTinyPng());
        db.AddAsset(textureGuid, "Texture2D", texturePath);

        var texture = new Texture2DAsset(
            textureGuid,
            "Tests/BaseColor",
            Texture2DVariantKey.DefaultSRgb,
            Texture2DSourceFormat.ImageFile);

        var cooked = Texture2DAssetCooker.LoadOrCook(db, texture);
        var bytes = db.GetCookedAssetBytes(cooked.Handle);
        var pixels = Texture2DAssetCooker.GetPixelData(bytes);

        Assert.True(cooked.IsValid);
        Assert.Equal(1u, cooked.Width);
        Assert.Equal(1u, cooked.Height);
        Assert.Equal(Texture2DCookedFormat.R8G8B8A8UNorm, cooked.Format);
        Assert.Equal(Texture2DColorSpace.SRgb, cooked.ColorSpace);
        Assert.Equal(4, pixels.Length);
        Assert.Contains(pixels.ToArray(), value => value > 0);
    }

    [Fact]
    public void Texture2DAssetCooker_GeneratesPackedMipChain()
    {
        using var workspace = TestWorkspace.Create();
        var textureGuid = Guid.Parse("48484848-5959-6a6a-7b7b-8c8c8c8c8c8c");
        var db = new TestAssetDatabase(
            AssetSourceAccessMode.Diagnostic,
            Path.Combine(workspace.Root, "Cooked"));
        string texturePath = workspace.Write(
            "Assets/SolidRed.ppm",
            "P3\n4 2\n255\n255 0 0  255 0 0  255 0 0  255 0 0\n255 0 0  255 0 0  255 0 0  255 0 0\n");
        db.AddAsset(textureGuid, "Texture2D", texturePath);

        var texture = new Texture2DAsset(
            textureGuid,
            "Tests/SolidRed",
            new Texture2DVariantKey(
                Texture2DCookedFormat.R8G8B8A8UNorm,
                Texture2DColorSpace.SRgb,
                GenerateMipMaps: true));

        CookedTexture2D cooked = Texture2DAssetCooker.LoadOrCook(db, texture);
        ReadOnlySpan<byte> pixels = Texture2DAssetCooker.GetPixelData(
            db.GetCookedAssetBytes(cooked.Handle));

        Assert.Equal(3, cooked.MipCount);
        Assert.Equal(44, pixels.Length);
        for (int index = 0; index < pixels.Length; index += 4)
        {
            Assert.Equal(255, pixels[index]);
            Assert.Equal(0, pixels[index + 1]);
            Assert.Equal(0, pixels[index + 2]);
            Assert.Equal(255, pixels[index + 3]);
        }

        Assert.True(db.TryGetCookedArtifact(textureGuid, cooked.Variant, out var mippedArtifact));
        db.Release(cooked.Handle);

        var noMipTexture = texture with
        {
            Variant = texture.Variant with { GenerateMipMaps = false }
        };
        CookedTexture2D noMipCooked = Texture2DAssetCooker.LoadOrCook(db, noMipTexture);
        Assert.True(db.TryGetCookedArtifact(textureGuid, noMipCooked.Variant, out var noMipArtifact));
        db.Release(noMipCooked.Handle);

        File.Copy(noMipArtifact.Path, mippedArtifact.Path, overwrite: true);
        File.SetLastWriteTimeUtc(mippedArtifact.Path, DateTime.UtcNow.AddMinutes(1));

        CookedTexture2D repaired = Texture2DAssetCooker.LoadOrCook(db, texture);
        Assert.Equal(3, repaired.MipCount);
        Assert.Equal(44, repaired.PixelDataSize);
    }

    [Fact]
    public void EnvironmentTextureAssetCooker_ParsesAndRecooksLinearLatLongPayload()
    {
        using var workspace = TestWorkspace.Create();
        var environmentGuid = Guid.Parse("10101010-2020-3030-4040-505050505050");
        var sourceTextureGuid = Guid.Parse("11111111-2121-3131-4141-515151515151");
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var texturePath = workspace.Write(
            "Assets/Environment.ppm",
            "P3\n4 2\n255\n255 128 0  64 128 255  0 32 128  255 255 255\n16 16 32  32 32 64  64 64 96  128 128 160\n");
        var environmentPath = workspace.Write("Assets/Studio.arienvironment", $$"""
            Version: 1
            Name: Test Studio
            SourceTexture:
              Guid: {{sourceTextureGuid:D}}
              PackageId: com.arisen.test
            Layout: LatLong
            SourceColorSpace: SRgb
            RuntimeFormat: R16G16B16A16SFloat
            RotationDegrees: 27.5
            Intensity: 1.4
            """);
        db.AddAsset(sourceTextureGuid, "Texture2D", texturePath);
        db.AddAsset(environmentGuid, "EnvironmentTexture", environmentPath);

        var asset = EnvironmentTextureAssetLoader.LoadSource(db, environmentGuid);
        var firstStamp = AssetDependencyTracker.GetEnvironmentTextureStamp(db, asset);
        var firstCooked = EnvironmentTextureAssetCooker.LoadOrCook(db, asset);
        var firstPixels = EnvironmentTextureAssetCooker.GetPixelData(
            db.GetCookedAssetBytes(firstCooked.Handle));

        Assert.Equal("Test Studio", asset.Name);
        Assert.Equal(sourceTextureGuid, asset.SourceTexture.Guid);
        Assert.Equal(EnvironmentTextureLayout.LatLong, asset.Variant.Layout);
        Assert.Equal(EnvironmentTextureCookedFormat.R16G16B16A16SFloat, asset.Variant.Format);
        Assert.Equal(Texture2DColorSpace.SRgb, asset.SourceColorSpace);
        Assert.Equal("latlong.r16g16b16a16sfloat.nomips", firstCooked.Variant);
        Assert.Equal(4u, firstCooked.Width);
        Assert.Equal(2u, firstCooked.Height);
        Assert.Equal(64, firstPixels.Length);
        Assert.Equal(27.5f, firstCooked.RotationDegrees);
        Assert.Equal(1.4f, firstCooked.Intensity);

        var firstRed = (float)BitConverter.UInt16BitsToHalf(
            BinaryPrimitives.ReadUInt16LittleEndian(firstPixels.Slice(0, 2)));
        var firstGreen = (float)BitConverter.UInt16BitsToHalf(
            BinaryPrimitives.ReadUInt16LittleEndian(firstPixels.Slice(2, 2)));
        Assert.InRange(firstRed, 0.999f, 1.001f);
        Assert.InRange(firstGreen, 0.214f, 0.217f);
        db.Release(firstCooked.Handle);

        File.WriteAllText(
            texturePath,
            "P3\n4 2\n255\n0 0 255  0 0 128  0 0 64  0 0 32\n0 16 64  0 32 96  0 64 128  0 128 255\n");
        File.SetLastWriteTimeUtc(texturePath, DateTime.UtcNow.AddMinutes(2));

        var secondStamp = AssetDependencyTracker.GetEnvironmentTextureStamp(db, asset);
        var secondCooked = EnvironmentTextureAssetCooker.LoadOrCook(db, asset);
        var secondPixels = EnvironmentTextureAssetCooker.GetPixelData(
            db.GetCookedAssetBytes(secondCooked.Handle));
        var secondRed = (float)BitConverter.UInt16BitsToHalf(
            BinaryPrimitives.ReadUInt16LittleEndian(secondPixels.Slice(0, 2)));
        var secondBlue = (float)BitConverter.UInt16BitsToHalf(
            BinaryPrimitives.ReadUInt16LittleEndian(secondPixels.Slice(4, 2)));

        Assert.NotEqual(firstStamp, secondStamp);
        Assert.Equal(0.0f, secondRed);
        Assert.InRange(secondBlue, 0.999f, 1.001f);
        db.Release(secondCooked.Handle);
    }

    [Fact]
    public void EnvironmentTextureAssetCooker_RejectsNonLatLongDimensions()
    {
        using var workspace = TestWorkspace.Create();
        var environmentGuid = Guid.NewGuid();
        var sourceTextureGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var texturePath = workspace.Write(
            "Assets/Invalid.ppm",
            "P3\n3 2\n255\n255 0 0  0 255 0  0 0 255\n255 255 255  64 64 64  0 0 0\n");
        var environmentPath = workspace.Write("Assets/Invalid.arienvironment", $$"""
            Version: 1
            SourceTexture:
              Guid: {{sourceTextureGuid:D}}
            Layout: LatLong
            SourceColorSpace: Linear
            RuntimeFormat: R16G16B16A16SFloat
            Intensity: 1.0
            """);
        db.AddAsset(sourceTextureGuid, "Texture2D", texturePath);
        db.AddAsset(environmentGuid, "EnvironmentTexture", environmentPath);

        var error = Assert.Throws<InvalidOperationException>(
            () => EnvironmentTextureAssetCooker.LoadOrCook(db, environmentGuid));

        Assert.Contains("2:1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentLightingAssetCooker_GeneratesCachesAndRecooksIblPayloads()
    {
        using var workspace = TestWorkspace.Create();
        var environmentGuid = Guid.Parse("20202020-3030-4040-5050-606060606060");
        var sourceTextureGuid = Guid.Parse("21212121-3131-4141-5151-616161616161");
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var texturePath = workspace.Write(
            "Assets/ConstantEnvironment.ppm",
            "P3\n4 2\n255\n128 64 32  128 64 32  128 64 32  128 64 32\n128 64 32  128 64 32  128 64 32  128 64 32\n");
        var environmentPath = workspace.Write("Assets/Constant.arienvironment", $$"""
            Version: 1
            Name: Constant Environment
            SourceTexture:
              Guid: {{sourceTextureGuid:D}}
            Layout: LatLong
            SourceColorSpace: Linear
            RuntimeFormat: R16G16B16A16SFloat
            RotationDegrees: 15.0
            Intensity: 2.0
            """);
        db.AddAsset(sourceTextureGuid, "Texture2D", texturePath);
        db.AddAsset(environmentGuid, "EnvironmentTexture", environmentPath);

        var firstCooked = EnvironmentLightingAssetCooker.LoadOrCook(db, environmentGuid);
        var firstBytes = db.GetCookedAssetBytes(firstCooked.Handle);
        var irradiance = EnvironmentLightingAssetCooker.GetIrradiancePixelData(firstBytes);
        var specular = EnvironmentLightingAssetCooker.GetSpecularPixelData(firstBytes);
        var brdf = EnvironmentLightingAssetCooker.GetBrdfPixelData(firstBytes);

        Assert.True(firstCooked.IsValid);
        Assert.Equal(EnvironmentLightingAssetCooker.CookedVariant, firstCooked.Variant);
        Assert.Equal(EnvironmentLightingAssetCooker.IrradianceWidth, firstCooked.IrradianceWidth);
        Assert.Equal(EnvironmentLightingAssetCooker.IrradianceHeight, firstCooked.IrradianceHeight);
        Assert.Equal(1, firstCooked.IrradianceMipCount);
        Assert.Equal(EnvironmentLightingAssetCooker.SpecularWidth, firstCooked.SpecularWidth);
        Assert.Equal(EnvironmentLightingAssetCooker.SpecularHeight, firstCooked.SpecularHeight);
        Assert.Equal(8, firstCooked.SpecularMipCount);
        Assert.Equal(EnvironmentLightingAssetCooker.BrdfWidth, firstCooked.BrdfWidth);
        Assert.Equal(EnvironmentLightingAssetCooker.BrdfHeight, firstCooked.BrdfHeight);
        Assert.Equal(1, firstCooked.BrdfMipCount);
        Assert.Equal(
            EnvironmentLightingAssetCooker.GetPackedMipDataSize(
                firstCooked.SpecularWidth,
                firstCooked.SpecularHeight,
                firstCooked.SpecularMipCount),
            specular.Length);

        Assert.InRange(ReadHalf(irradiance, 0), 0.500f, 0.505f);
        Assert.InRange(ReadHalf(irradiance, 2), 0.249f, 0.253f);
        Assert.InRange(ReadHalf(irradiance, 4), 0.123f, 0.127f);
        Assert.InRange(ReadHalf(specular, 0), 0.500f, 0.505f);
        Assert.InRange(ReadHalf(specular, specular.Length - 8), 0.500f, 0.505f);

        var brdfCenterOffset = checked(
            (((int)EnvironmentLightingAssetCooker.BrdfHeight / 2) *
                (int)EnvironmentLightingAssetCooker.BrdfWidth +
                (int)EnvironmentLightingAssetCooker.BrdfWidth / 2) * 8);
        var brdfA = ReadHalf(brdf, brdfCenterOffset);
        var brdfB = ReadHalf(brdf, brdfCenterOffset + 2);
        Assert.InRange(brdfA, 0.1f, 1.0f);
        Assert.InRange(brdfB, 0.0f, 0.5f);
        Assert.True(brdfA > brdfB);

        Assert.True(db.TryGetCookedArtifact(environmentGuid, firstCooked.Variant, out var firstArtifact));
        db.Release(firstCooked.Handle);
        File.SetLastWriteTimeUtc(firstArtifact.Path, DateTime.UtcNow.AddMinutes(5));
        var preservedWriteTime = File.GetLastWriteTimeUtc(firstArtifact.Path);

        var cachedCooked = EnvironmentLightingAssetCooker.LoadOrCook(db, environmentGuid);
        Assert.Equal(preservedWriteTime, File.GetLastWriteTimeUtc(firstArtifact.Path));
        db.Release(cachedCooked.Handle);

        File.WriteAllText(
            texturePath,
            "P3\n4 2\n255\n0 32 255  0 32 255  0 32 255  0 32 255\n0 32 255  0 32 255  0 32 255  0 32 255\n");
        File.SetLastWriteTimeUtc(texturePath, preservedWriteTime.AddMinutes(1));
        var recooked = EnvironmentLightingAssetCooker.LoadOrCook(db, environmentGuid);
        var recookedIrradiance = EnvironmentLightingAssetCooker.GetIrradiancePixelData(
            db.GetCookedAssetBytes(recooked.Handle));

        Assert.InRange(ReadHalf(recookedIrradiance, 0), 0.0f, 0.001f);
        Assert.InRange(ReadHalf(recookedIrradiance, 4), 0.999f, 1.001f);
        db.Release(recooked.Handle);
    }

    [Fact]
    public void AssetImporter_InfersEnvironmentDescriptorAndHdrTextureMetadata()
    {
        using var workspace = TestWorkspace.Create();
        var assetsRoot = Path.Combine(workspace.Root, "Assets");
        var environmentPath = workspace.Write(
            "Assets/Sky.arienvironment",
            "Version: 1\nSourceTexture:\n  Guid: 11111111-2222-3333-4444-555555555555\n");
        var hdrPath = workspace.Write("Assets/Sky.hdr", "placeholder");
        var databasePath = Path.Combine(workspace.Root, ".arisen", "Editor", "AssetDatabase.db");
        EditorAssetDatabase.Initialize(databasePath);

        try
        {
            using var importer = new ArisenEditor.Core.Assets.AssetImporter(
                assetsRoot,
                workspace.Root,
                "com.arisen.test");
            importer.Start();

            var environmentMetadata = SerializationUtil.Deserialize<ArisenEditor.Core.Assets.AssetMetadata>(
                environmentPath + ".meta",
                serializeIfNotExist: false);
            var hdrMetadata = SerializationUtil.Deserialize<ArisenEditor.Core.Assets.AssetMetadata>(
                hdrPath + ".meta",
                serializeIfNotExist: false);

            Assert.Equal("EnvironmentTexture", environmentMetadata.AssetType);
            Assert.Equal("ArisenEnvironmentTextureImporter", environmentMetadata.Importer);
            Assert.Equal("Texture2D", hdrMetadata.AssetType);
            Assert.Equal("HdrTextureImporter", hdrMetadata.Importer);
        }
        finally
        {
            EditorAssetDatabase.Instance.Dispose();
        }
    }

    [Fact]
    public void GltfModelImportEmitter_ExtractsDataUriImageTextures()
    {
        using var workspace = TestWorkspace.Create();
        var sourceGuid = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000");
        var shaderGuid = Guid.Parse("33333333-4444-5555-6666-777777777777");
        var imageDataUri = $"data:image/png;base64,{Convert.ToBase64String(CreateTinyPng())}";
        var gltfPath = workspace.Write("Assets/DataUriRobot.gltf", $$"""
            {
              "asset": { "version": "2.0" },
              "materials": [
                {
                  "name": "EmbeddedPaint",
                  "pbrMetallicRoughness": {
                    "baseColorTexture": { "index": 0 }
                  }
                }
              ],
              "textures": [
                { "source": 0 }
              ],
              "images": [
                { "uri": "{{imageDataUri}}" }
              ]
            }
            """);

        var plan = GltfModelImportPlanner.CreatePlan(gltfPath, sourceGuid, "com.arisen.test");
        var result = GltfModelImportEmitter.Emit(
            plan,
            gltfPath,
            Path.Combine(workspace.Root, "Assets", "Generated"),
            new GltfModelImportEmissionSettings(shaderGuid, "Tests/StandardLit", "Robot"));

        var texturePath = Assert.Single(result.TexturePaths);
        Assert.Empty(result.Warnings);
        Assert.Equal(".png", Path.GetExtension(texturePath));
        Assert.Equal(CreateTinyPng(), File.ReadAllBytes(texturePath));

        var materialMetadata = SerializationUtil.Deserialize<AssetMetadata>(
            Assert.Single(result.MaterialPaths) + ".meta",
            serializeIfNotExist: false);
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        db.AddAsset(shaderGuid, ShaderAssetCooker.ShaderSourceAssetType, workspace.Write("Assets/StandardLit.shader", CreateStandardLitShader()));
        db.AddAsset(materialMetadata.Guid, "Material", result.MaterialPaths.Single());
        var loadedMaterial = MaterialAssetLoader.LoadSource(db, materialMetadata.Guid);

        Assert.Equal(Texture2DSourceFormat.ImageFile, loadedMaterial.Texture2DRefs.Single().Texture.SourceFormat);
    }

    [Fact]
    public void GltfModelImportEmitter_ExtractsBufferViewImageTexturesFromGlb()
    {
        using var workspace = TestWorkspace.Create();
        var sourceGuid = Guid.Parse("dddddddd-eeee-ffff-0000-111111111111");
        var shaderGuid = Guid.Parse("44444444-5555-6666-7777-888888888888");
        var image = CreateTinyPng();
        var glbPath = workspace.WriteBinary("Assets/GlbRobot.glb", CreateGltfImageGlb(image));

        var plan = GltfModelImportPlanner.CreatePlan(glbPath, sourceGuid, "com.arisen.test");
        var result = GltfModelImportEmitter.Emit(
            plan,
            glbPath,
            Path.Combine(workspace.Root, "Assets", "Generated"),
            new GltfModelImportEmissionSettings(shaderGuid, "Tests/StandardLit", "Robot"));

        var texturePath = Assert.Single(result.TexturePaths);
        Assert.Empty(result.Warnings);
        Assert.Equal(".png", Path.GetExtension(texturePath));
        Assert.Equal(image, File.ReadAllBytes(texturePath));

        var textureMetadata = SerializationUtil.Deserialize<AssetMetadata>(texturePath + ".meta", serializeIfNotExist: false);
        Assert.Equal("Texture2D", textureMetadata.AssetType);
        Assert.NotNull(textureMetadata.Generated);
        Assert.Equal("images/0", textureMetadata.Generated.ChildKey);
    }

    [Fact]
    public void ShaderVariantKey_NormalizesKeywordsForIdentityAndCookedPath()
    {
        var keywords = new[] { "USE_FOG", "  ALPHA_TEST ", "", "USE_FOG", "LIGHT-CLUSTER" };

        var normalized = ShaderVariantKey.NormalizeKeywordSet(keywords);
        var variant = ShaderVariantKey.VulkanDebug.GetCookedVariant("PSMain", keywords);
        var identity = ShaderVariantKey.VulkanDebug.GetVariantIdentity(keywords);

        Assert.Equal(new[] { "ALPHA_TEST", "LIGHT-CLUSTER", "USE_FOG" }, normalized);
        Assert.Contains(".kw-ALPHA_TEST-LIGHT_CLUSTER-USE_FOG.PSMain", variant, StringComparison.Ordinal);
        Assert.EndsWith("|ALPHA_TEST+LIGHT-CLUSTER+USE_FOG", identity, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderQueuePolicy_ClassifiesOpaqueAlphaTestAndTransparentMaterials()
    {
        var alphaTest = RenderQueuePolicy.Resolve(
            MaterialRenderState.Default,
            new[] { "ALPHA_TEST" });
        var opaque = RenderQueuePolicy.Resolve(
            MaterialRenderState.Default,
            new[] { "USE_TRIPLANAR" });
        var transparent = RenderQueuePolicy.Resolve(
            new MaterialRenderState(
                ECullModeFlagBits.CULL_MODE_NONE,
                EFrontFace.FRONT_FACE_COUNTER_CLOCKWISE,
                true,
                EBlendFactor.BLEND_FACTOR_SRC_ALPHA,
                EBlendFactor.BLEND_FACTOR_ONE_MINUS_SRC_ALPHA,
                EBlendOp.BLEND_OP_ADD),
            new[] { "ALPHA_TEST" });

        Assert.Equal(RenderQueueClass.Opaque, opaque.Class);
        Assert.Equal(RenderQueuePolicy.OpaqueQueue, opaque.Value);
        Assert.Equal(RenderQueueClass.AlphaTest, alphaTest.Class);
        Assert.Equal(RenderQueuePolicy.AlphaTestQueue, alphaTest.Value);
        Assert.Equal(RenderQueueClass.Transparent, transparent.Class);
        Assert.Equal(RenderQueuePolicy.TransparentQueue, transparent.Value);
    }

    [Fact]
    public void TransparentDrawOrdering_SortsBackToFrontWithStableDistanceTies()
    {
        var draws = new[]
        {
            new MeshDrawCommand
            {
                LocalToWorld = Matrix4x4.CreateTranslation(0.0f, 0.0f, 2.0f),
                MaterialID = 10
            },
            new MeshDrawCommand
            {
                LocalToWorld = Matrix4x4.CreateTranslation(1.0f, 0.0f, 5.0f),
                MaterialID = 20
            },
            new MeshDrawCommand
            {
                LocalToWorld = Matrix4x4.CreateTranslation(-1.0f, 0.0f, 5.0f),
                MaterialID = 30
            },
            new MeshDrawCommand
            {
                LocalToWorld = Matrix4x4.CreateTranslation(100.0f, 0.0f, 3.0f),
                MaterialID = 40
            }
        };
        var sortKeys = new TransparentDrawSortKey[draws.Length];

        TransparentDrawOrdering.SortBackToFront(
            draws,
            sortKeys,
            draws.Length,
            Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY));

        Assert.Equal(new uint[] { 20, 30, 40, 10 }, draws.Select(draw => draw.MaterialID));
        Assert.Equal(5.0f, sortKeys[0].CameraDepth);
        Assert.Equal(5.0f, sortKeys[1].CameraDepth);
        Assert.Equal(1u, sortKeys[0].SourceDrawIndex);
        Assert.Equal(2u, sortKeys[1].SourceDrawIndex);
    }

    [Fact]
    public void StaticMeshFrustumCuller_UsesMeshBoundsWhenSceneBoundsAreMissing()
    {
        var item = new StaticMeshRenderItem
        {
            LocalToWorld = Matrix4x4.Identity,
            MeshGuid = Guid.NewGuid(),
            Visible = 1
        };
        var meshBounds = new MeshBounds(
            new Vector3(-0.5f, -0.5f, 0.2f),
            new Vector3(0.5f, 0.5f, 0.8f));

        Assert.True(StaticMeshFrustumCuller.IsVisible(item, meshBounds, Matrix4x4.Identity));

        item.LocalToWorld = Matrix4x4.CreateTranslation(3.0f, 0.0f, 0.0f);

        Assert.False(StaticMeshFrustumCuller.IsVisible(item, meshBounds, Matrix4x4.Identity));
    }

    [Fact]
    public void StaticMeshFrustumCuller_UsesAuthoredSceneBoundsBeforeMeshBounds()
    {
        var item = new StaticMeshRenderItem
        {
            LocalToWorld = Matrix4x4.Identity,
            MeshGuid = Guid.NewGuid(),
            BoundsCenter = new Vector3(3.0f, 0.0f, 0.5f),
            BoundsExtents = new Vector3(0.25f, 0.25f, 0.25f),
            Visible = 1
        };
        var meshBounds = new MeshBounds(
            new Vector3(-0.5f, -0.5f, 0.2f),
            new Vector3(0.5f, 0.5f, 0.8f));

        Assert.False(StaticMeshFrustumCuller.IsVisible(item, meshBounds, Matrix4x4.Identity));
    }

    [Fact]
    public void ShaderMaterialContractAnnotations_ParseAndRejectInvalidContracts()
    {
        var contract = ShaderMaterialContractAnnotations.Parse(
            """
            // @arisen.material.texture2d BaseColor
            // @arisen.material.float RoughnessFactor
            // @arisen.material.color BaseColorFactor
            """,
            "Contract.hlsl");

        Assert.Equal(new[] { "BaseColor" }, contract.RequiredTexture2DRefs);
        Assert.Equal(new[] { "RoughnessFactor" }, contract.RequiredScalarProperties);
        Assert.Equal(new[] { "BaseColorFactor" }, contract.RequiredVector4Properties);

        var duplicate = Assert.Throws<InvalidOperationException>(() =>
            ShaderMaterialContractAnnotations.Parse(
                """
                // @arisen.material.texture BaseColor
                // @arisen.material.texture basecolor
                """,
                "Duplicate.hlsl"));
        Assert.Contains("duplicate name", duplicate.Message, StringComparison.OrdinalIgnoreCase);

        var unsupported = Assert.Throws<InvalidOperationException>(() =>
            ShaderMaterialContractAnnotations.Parse("// @arisen.material.matrix4x4 World", "Unsupported.hlsl"));
        Assert.Contains("unsupported material contract kind", unsupported.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShaderLabSource_LoadsStagesContractKeywordsAndRenderState()
    {
        using var workspace = TestWorkspace.Create();
        var includeDirectory = Path.Combine(workspace.Root, "Includes");
        Directory.CreateDirectory(includeDirectory);
        var shaderPath = Path.Combine(workspace.Root, "Lit.shader");
        File.WriteAllText(shaderPath, """
            Shader "Tests/Lit"
            {
                MaterialContract
                {
                    Texture2D BaseColor
                    Vector4 BaseColorFactor
                }

                SubShader
                {
                    Pass
                    {
                        Name "Forward"
                        Cull Back
                        Blend SrcAlpha OneMinusSrcAlpha
                        BlendOp ReverseSubtract

                        HLSLPROGRAM
                        #include "Includes/Common.hlsl"
                        #pragma vertex VSMain
                        #pragma fragment PSMain
                        #pragma multi_compile _ USE_FOG
                        #pragma shader_feature ALPHA_TEST
                        // @arisen.material.float RoughnessFactor
                        float4 VSMain() : SV_Position { return 0; }
                        float4 PSMain() : SV_Target0 { return 1; }
                        ENDHLSL
                    }
                }
            }
            """);

        var source = ShaderLabSource.Load(shaderPath);

        Assert.Equal("Tests/Lit", source.Name);
        Assert.Contains(workspace.Root, source.Includes);
        Assert.Contains("Includes", source.Includes);
        Assert.Equal(new[] { "_", "USE_FOG", "ALPHA_TEST" }, source.CompileTimeKeywords);
        Assert.Equal(new[] { "BaseColor" }, source.MaterialContract.RequiredTexture2DRefs);
        Assert.Equal(new[] { "RoughnessFactor" }, source.MaterialContract.RequiredScalarProperties);
        Assert.Equal(new[] { "BaseColorFactor" }, source.MaterialContract.RequiredVector4Properties);
        Assert.Equal(ECullModeFlagBits.CULL_MODE_BACK_BIT, source.RenderState.CullMode);
        Assert.True(source.RenderState.BlendEnabled);
        Assert.Equal(EBlendFactor.BLEND_FACTOR_SRC_ALPHA, source.RenderState.SrcColorBlendFactor);
        Assert.Equal(EBlendFactor.BLEND_FACTOR_ONE_MINUS_SRC_ALPHA, source.RenderState.DstColorBlendFactor);
        Assert.Equal(EBlendOp.BLEND_OP_REVERSE_SUBTRACT, source.RenderState.ColorBlendOp);

        var stages = source.BuildStages();
        Assert.Collection(
            stages,
            stage =>
            {
                Assert.Equal("Vertex", stage.Name);
                Assert.Equal(EProgramStage.Vertex, stage.ProgramStage);
                Assert.Equal("VSMain", stage.EntryPoint);
            },
            stage =>
            {
                Assert.Equal("Fragment", stage.Name);
                Assert.Equal(EProgramStage.Fragment, stage.ProgramStage);
                Assert.Equal("PSMain", stage.EntryPoint);
            });
    }

    [Fact]
    public void TonemapPackageAssets_ConsumePreparedSceneExposure()
    {
        var tonemapShaderPath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.generic-renderpipeline",
            "Assets",
            "Shaders",
            "Tonemap.hlsl");
        var tonemapPassPath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.generic-renderpipeline",
            "Src",
            "TonemapPass.cs");
        var pipelinePath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.generic-renderpipeline",
            "Src",
            "GenericRenderPipeline.cs");
        var inspectorPath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.editor",
            "Managed",
            "Core",
            "Views",
            "InspectorViewModel.cs");

        var tonemapShaderText = File.ReadAllText(tonemapShaderPath);
        var tonemapPassText = File.ReadAllText(tonemapPassPath);
        var pipelineText = File.ReadAllText(pipelinePath);
        var inspectorText = File.ReadAllText(inspectorPath);

        Assert.Contains(
            "float exposure = max(TonemapConstants.toneMapParams.x, 0.0);",
            tonemapShaderText,
            StringComparison.Ordinal);
        Assert.Contains("AcesFilm(hdrColor * exposure)", tonemapShaderText, StringComparison.Ordinal);
        Assert.Contains(
            "m_Exposure = SceneEnvironment.NormalizeExposure(exposure);",
            tonemapPassText,
            StringComparison.Ordinal);
        Assert.Contains(
            "m_TonemapPass.SetExposure(sceneEnvironment.Exposure);",
            pipelineText,
            StringComparison.Ordinal);
        Assert.DoesNotContain("m_TonemapPass.SetExposure(1.0f);", pipelineText, StringComparison.Ordinal);
        Assert.Contains(
            "AddReadOnly(category, \"Exposure\", environment.Exposure.ToString(\"0.###\"));",
            inspectorText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StandardLitPackageAssets_DefinePbrEnvironmentMaterialContract()
    {
        using var workspace = TestWorkspace.Create();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var shaderGuid = Guid.Parse("72b6d255-0f54-46e5-9d05-8e0486d4f875");
        var smokeCheckerGuid = Guid.Parse("c320bf66-0495-4e70-8f27-d54e90dd6c8d");
        var defaultNormalGuid = Guid.Parse("ead642b0-cde6-4ae0-8bdc-03472fb3d5aa");
        var materialGuid = Guid.Parse("4ac21c64-e984-4ed0-9e21-93878de5249e");
        var shaderPath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.generic-renderpipeline",
            "Assets",
            "Shaders",
            "StandardLit.shader");
        var smokeStaticMeshShaderPath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.generic-renderpipeline",
            "Assets",
            "Shaders",
            "SmokeStaticMesh.shader");
        var staticMeshPassPath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.generic-renderpipeline",
            "Src",
            "StaticMeshPass.cs");
        var smokeCheckerPath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.generic-renderpipeline",
            "Assets",
            "Textures",
            "SmokeChecker.ppm");
        var defaultNormalPath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.generic-renderpipeline",
            "Assets",
            "Textures",
            "DefaultNormal.ppm");
        var materialPath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.generic-renderpipeline",
            "Assets",
            "Materials",
            "StandardLitMaterial.arismaterial");

        var shaderSource = ShaderLabSource.Load(shaderPath);
        var shaderText = File.ReadAllText(shaderPath);
        var smokeStaticMeshShaderText = File.ReadAllText(smokeStaticMeshShaderPath);
        var staticMeshPassText = File.ReadAllText(staticMeshPassPath);

        Assert.Equal("GenericRP/StandardLit", shaderSource.Name);
        Assert.Contains("float4 environmentAmbient;", shaderText, StringComparison.Ordinal);
        Assert.Contains("float4 cameraWorldPosition;", shaderText, StringComparison.Ordinal);
        Assert.Contains("float3 WorldPosition : TEXCOORD1;", shaderText, StringComparison.Ordinal);
        Assert.Contains("DistributionGGX", shaderText, StringComparison.Ordinal);
        Assert.Contains("GeometrySmith", shaderText, StringComparison.Ordinal);
        Assert.Contains("FresnelSchlick", shaderText, StringComparison.Ordinal);
        Assert.Contains("FresnelSchlickRoughness", shaderText, StringComparison.Ordinal);
        Assert.Contains("float2 DirectionToLatLongUV", shaderText, StringComparison.Ordinal);
        Assert.Contains(
            "atan2(direction.x, direction.z) + rotationRadians;",
            shaderText,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (input.EnvironmentTextureIndices1.w > 0.5)",
            shaderText,
            StringComparison.Ordinal);
        Assert.Contains(
            "BindlessImages[NonUniformResourceIndex(irradianceImageIndex)].SampleLevel(",
            shaderText,
            StringComparison.Ordinal);
        Assert.Contains(
            "BindlessImages[NonUniformResourceIndex(specularImageIndex)].SampleLevel(",
            shaderText,
            StringComparison.Ordinal);
        Assert.Contains("roughness * specularMaxLod", shaderText, StringComparison.Ordinal);
        Assert.Contains("float2(normalDotView, roughness)", shaderText, StringComparison.Ordinal);
        Assert.Contains(
            "ambientDiffuse = irradiance * albedo * diffuseWeight * environmentIntensity;",
            shaderText,
            StringComparison.Ordinal);
        Assert.Contains(
            "(reflectanceAtNormal * brdf.x + brdf.y) * environmentIntensity;",
            shaderText,
            StringComparison.Ordinal);
        Assert.Contains("roughness *= packedMetallicRoughness.g;", shaderText, StringComparison.Ordinal);
        Assert.Contains("metallic *= packedMetallicRoughness.b;", shaderText, StringComparison.Ordinal);
        Assert.Contains("? packedMetallicRoughness.r", shaderText, StringComparison.Ordinal);
        Assert.Contains(
            "float3 ambientDiffuse = ambientRadiance * albedo * (1.0 - metallic);",
            shaderText,
            StringComparison.Ordinal);
        Assert.Contains(
            "float3 ambientSpecular = ambientRadiance * reflectanceAtNormal *",
            shaderText,
            StringComparison.Ordinal);
        Assert.Contains(
            "float3 indirectLighting = (ambientDiffuse + ambientSpecular) * occlusion;",
            shaderText,
            StringComparison.Ordinal);
        Assert.Contains(
            "float3 outputColor = directLighting + indirectLighting + emissive;",
            shaderText,
            StringComparison.Ordinal);
        Assert.Contains(
            "clip(baseColor.a - input.PbrMaterialParameters.y);",
            shaderText,
            StringComparison.Ordinal);
        Assert.Contains("float4 environmentTextureIndices0;", shaderText, StringComparison.Ordinal);
        Assert.Contains("float4 environmentTextureIndices1;", shaderText, StringComparison.Ordinal);
        Assert.Contains("float4 environmentParameters;", shaderText, StringComparison.Ordinal);
        Assert.Contains("float4 emissiveFactor;", smokeStaticMeshShaderText, StringComparison.Ordinal);
        Assert.Contains("float4 emissiveTextureIndices;", smokeStaticMeshShaderText, StringComparison.Ordinal);
        Assert.Contains("float4 metallicRoughnessTextureIndices;", smokeStaticMeshShaderText, StringComparison.Ordinal);
        Assert.Contains("float4 occlusionTextureIndices;", smokeStaticMeshShaderText, StringComparison.Ordinal);
        Assert.Contains("float4 pbrMaterialParameters;", smokeStaticMeshShaderText, StringComparison.Ordinal);
        Assert.Contains("float4 environmentTextureIndices0;", smokeStaticMeshShaderText, StringComparison.Ordinal);
        Assert.Contains("float4 environmentTextureIndices1;", smokeStaticMeshShaderText, StringComparison.Ordinal);
        Assert.Contains("float4 environmentParameters;", smokeStaticMeshShaderText, StringComparison.Ordinal);
        Assert.Contains(
            "public readonly Vector4 EnvironmentTextureIndices0;",
            staticMeshPassText,
            StringComparison.Ordinal);
        Assert.Contains(
            "public readonly Vector4 EnvironmentTextureIndices1;",
            staticMeshPassText,
            StringComparison.Ordinal);
        Assert.Contains(
            "public readonly Vector4 EnvironmentParameters;",
            staticMeshPassText,
            StringComparison.Ordinal);
        Assert.Contains(
            "public void SetEnvironmentLighting(RHIEnvironmentLightingResource? environmentLighting)",
            staticMeshPassText,
            StringComparison.Ordinal);
        Assert.Equal(
            new[] { MaterialTextureSlots.BaseColor, MaterialTextureSlots.Normal },
            shaderSource.MaterialContract.RequiredTexture2DRefs);
        Assert.Equal(
            new[] { MaterialPropertySlots.MetallicFactor, MaterialPropertySlots.RoughnessFactor },
            shaderSource.MaterialContract.RequiredScalarProperties);
        Assert.Equal(
            new[] { MaterialPropertySlots.BaseColorFactor, MaterialPropertySlots.EmissiveFactor },
            shaderSource.MaterialContract.RequiredVector4Properties);
        Assert.Equal(new[] { "USE_NORMAL_MAP", "ALPHA_TEST", "USE_TRIPLANAR" }, shaderSource.CompileTimeKeywords);
        Assert.Equal(ECullModeFlagBits.CULL_MODE_BACK_BIT, shaderSource.RenderState.CullMode);
        Assert.False(shaderSource.RenderState.BlendEnabled);

        db.AddAsset(shaderGuid, ShaderAssetCooker.ShaderSourceAssetType, shaderPath, "com.arisen.generic-renderpipeline");
        db.AddAsset(smokeCheckerGuid, "Texture2D", smokeCheckerPath, "com.arisen.generic-renderpipeline");
        db.AddAsset(defaultNormalGuid, "Texture2D", defaultNormalPath, "com.arisen.generic-renderpipeline");
        db.AddAsset(materialGuid, "Material", materialPath, "com.arisen.generic-renderpipeline");

        var material = MaterialAssetLoader.LoadSource(db, materialGuid);

        Assert.Equal("GenericRP/StandardLitMaterial", material.Name);
        Assert.Equal(shaderGuid, material.Shader.Guid);
        Assert.Equal(new[] { "USE_NORMAL_MAP" }, material.Shader.VariantKeywords);
        Assert.Equal(2, material.Shader.Stages.Count);
        Assert.Contains(material.Texture2DRefs, texture => texture.Name == MaterialTextureSlots.BaseColor);
        Assert.Contains(material.Texture2DRefs, texture => texture.Name == MaterialTextureSlots.Normal);
        Assert.Equal(
            Texture2DColorSpace.SRgb,
            material.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.BaseColor).Texture.Variant.ColorSpace);
        Assert.Equal(
            Texture2DColorSpace.Linear,
            material.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.Normal).Texture.Variant.ColorSpace);
        Assert.Equal(0.0f, material.ScalarProperties.Single(property => property.Name == MaterialPropertySlots.MetallicFactor).Value);
        Assert.Equal(0.7f, material.ScalarProperties.Single(property => property.Name == MaterialPropertySlots.RoughnessFactor).Value);
        Assert.Equal(
            MaterialPbrDefaults.OcclusionStrength,
            material.ScalarProperties.Single(property => property.Name == MaterialPropertySlots.OcclusionStrength).Value);
        Assert.Equal(
            MaterialPbrDefaults.AlphaCutoff,
            material.ScalarProperties.Single(property => property.Name == MaterialPropertySlots.AlphaCutoff).Value);
        Assert.Equal(
            new Vector4(1, 1, 1, 1),
            material.Vector4Properties.Single(property => property.Name == MaterialPropertySlots.BaseColorFactor).Value);
        Assert.Equal(
            Vector4.Zero,
            material.Vector4Properties.Single(property => property.Name == MaterialPropertySlots.EmissiveFactor).Value);
    }

    [Fact]
    public void EnvironmentSkyPackageAsset_DefinesFullscreenGradientShader()
    {
        var shaderPath = GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.generic-renderpipeline",
            "Assets",
            "Shaders",
            "EnvironmentSky.hlsl");
        var shaderText = File.ReadAllText(shaderPath);

        Assert.Contains("SV_VertexID", shaderText, StringComparison.Ordinal);
        Assert.Contains("commandList.Draw(3)", File.ReadAllText(GetRepositoryFile(
            "Arisen",
            "Development",
            "PackageGame",
            "Local",
            "com.arisen.generic-renderpipeline",
            "Src",
            "EnvironmentSkyPass.cs")), StringComparison.Ordinal);
        Assert.Contains("SkyConstants.skyColorIntensity", shaderText, StringComparison.Ordinal);
        Assert.Contains("SkyConstants.horizonColor", shaderText, StringComparison.Ordinal);
        Assert.Contains("SkyConstants.groundColor", shaderText, StringComparison.Ordinal);
        Assert.Contains("SkyConstants.environmentImageIndex", shaderText, StringComparison.Ordinal);
        Assert.Contains("BindlessImages", shaderText, StringComparison.Ordinal);
        Assert.Contains("atan2", shaderText, StringComparison.Ordinal);
    }

    [Fact]
    public void MaterialConventions_DefinePbrBindingsAndDeterministicTextureDefaults()
    {
        Assert.Equal("MetallicRoughness", MaterialTextureSlots.MetallicRoughness);
        Assert.Equal("Occlusion", MaterialTextureSlots.Occlusion);
        Assert.Equal("OcclusionStrength", MaterialPropertySlots.OcclusionStrength);
        Assert.Equal("AlphaCutoff", MaterialPropertySlots.AlphaCutoff);
        Assert.Equal(0, MaterialPbrTextureChannels.Occlusion);
        Assert.Equal(1, MaterialPbrTextureChannels.Roughness);
        Assert.Equal(2, MaterialPbrTextureChannels.Metallic);
        Assert.Equal(1.0f, MaterialPbrDefaults.OcclusionStrength);
        Assert.Equal(0.5f, MaterialPbrDefaults.AlphaCutoff);

        var textureRef = new MaterialTexture2DRef(
            MaterialTextureSlots.MetallicRoughness,
            new Texture2DAsset(
                Guid.NewGuid(),
                "Tests/MetallicRoughness",
                new Texture2DVariantKey(
                    Texture2DCookedFormat.R8G8B8A8UNorm,
                    Texture2DColorSpace.Linear,
                    GenerateMipMaps: false),
                Texture2DSourceFormat.ImageFile));

        Assert.Null(textureRef.Sampler);
        Assert.Null(textureRef.Transform);
        Assert.Equal(MaterialTextureSamplerSettings.Default, textureRef.ResolvedSampler);
        Assert.Equal(MaterialTextureTransform.Identity, textureRef.ResolvedTransform);
    }

    [Fact]
    public void MaterialCooker_PreservesShaderRenderStateAndTextureBindingMetadata()
    {
        using var workspace = TestWorkspace.Create();
        var shaderGuid = Guid.NewGuid();
        var textureGuid = Guid.NewGuid();
        var materialGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));

        var shaderPath = workspace.Write("Assets/Lit.shader", """
            Shader "Tests/Lit"
            {
                MaterialContract
                {
                    Texture2D BaseColor
                    Scalar RoughnessFactor
                    Vector4 BaseColorFactor
                }

                SubShader
                {
                    Pass
                    {
                        Cull Front
                        Blend SrcAlpha OneMinusSrcAlpha
                        BlendOp Add

                        HLSLPROGRAM
                        #pragma vertex VSMain
                        #pragma fragment PSMain
                        #pragma shader_feature ALPHA_TEST
                        float4 VSMain() : SV_Position { return 0; }
                        float4 PSMain() : SV_Target0 { return 1; }
                        ENDHLSL
                    }
                }
            }
            """);
        var texturePath = workspace.Write("Assets/BaseColor.ppm", "P3\n1 1\n255\n255 255 255\n");
        var materialPath = workspace.Write("Assets/Lit.arismaterial", $$"""
            Name: Tests/LitMaterial
            Shader:
              Guid: {{shaderGuid:D}}
              Keywords:
              - ALPHA_TEST
            Texture2DRefs:
            - Name: BaseColor
              Slot: 3
              Texture:
                Guid: {{textureGuid:D}}
                Name: Tests/BaseColor
                Variant:
                  Format: R8G8B8A8UNorm
                  ColorSpace: SRgb
                  GenerateMipMaps: false
                SourceFormat: PpmP3
              Sampler:
                MinFilter: Nearest
                MagFilter: Linear
                MipmapMode: Linear
                WrapU: MirroredRepeat
                WrapV: ClampToEdge
              Transform:
                Offset:
                  X: 0.25
                  Y: 0.5
                Scale:
                  X: 2.0
                  Y: 0.75
                Rotation: 0.3
                TexCoord: 1
            ScalarProperties:
            - Name: RoughnessFactor
              Value: 0.5
            Vector4Properties:
            - Name: BaseColorFactor
              Value:
                X: 0.1
                Y: 0.2
                Z: 0.3
                W: 0.4
            """);

        db.AddAsset(shaderGuid, ShaderAssetCooker.ShaderSourceAssetType, shaderPath);
        db.AddAsset(textureGuid, "Texture2D", texturePath);
        db.AddAsset(materialGuid, "Material", materialPath);

        var cooked = MaterialAssetCooker.LoadOrCook(db, materialGuid);

        Assert.True(cooked.IsValid);
        Assert.Equal("Tests/LitMaterial", cooked.Asset.Name);
        Assert.Equal(shaderGuid, cooked.Asset.Shader.Guid);
        Assert.Equal(new[] { "ALPHA_TEST" }, cooked.Asset.Shader.VariantKeywords);
        Assert.Equal(2, cooked.Asset.Shader.Stages.Count);
        Assert.Equal(ECullModeFlagBits.CULL_MODE_FRONT_BIT, cooked.Asset.RenderState.CullMode);
        Assert.True(cooked.Asset.RenderState.BlendEnabled);
        Assert.Equal(EBlendFactor.BLEND_FACTOR_SRC_ALPHA, cooked.Asset.RenderState.SrcColorBlendFactor);
        Assert.Equal(EBlendFactor.BLEND_FACTOR_ONE_MINUS_SRC_ALPHA, cooked.Asset.RenderState.DstColorBlendFactor);
        var textureRef = Assert.Single(cooked.Asset.Texture2DRefs);
        Assert.Equal(3u, textureRef.Slot);
        Assert.Equal(
            new MaterialTextureSamplerSettings(
                MaterialTextureFilter.Nearest,
                MaterialTextureFilter.Linear,
                MaterialTextureMipmapMode.Linear,
                MaterialTextureWrapMode.MirroredRepeat,
                MaterialTextureWrapMode.ClampToEdge),
            textureRef.ResolvedSampler);
        Assert.Equal(
            new MaterialTextureTransform(
                new Vector2(0.25f, 0.5f),
                new Vector2(2.0f, 0.75f),
                0.3f,
                1),
            textureRef.ResolvedTransform);
        Assert.Equal(0.5f, cooked.Asset.ScalarProperties.Single().Value);
        Assert.Equal(new Vector4(0.1f, 0.2f, 0.3f, 0.4f), cooked.Asset.Vector4Properties.Single().Value);
    }

    [Fact]
    public void MaterialLoader_ReportsMissingShaderContractBindings()
    {
        using var workspace = TestWorkspace.Create();
        var shaderGuid = Guid.NewGuid();
        var materialGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));

        var shaderPath = workspace.Write("Assets/MissingBinding.shader", """
            Shader "Tests/MissingBinding"
            {
                MaterialContract
                {
                    Texture2D BaseColor
                    Texture2D Normal
                    Scalar RoughnessFactor
                    Vector4 BaseColorFactor
                }

                SubShader
                {
                    Pass
                    {
                        HLSLPROGRAM
                        #pragma vertex VSMain
                        #pragma fragment PSMain
                        float4 VSMain() : SV_Position { return 0; }
                        float4 PSMain() : SV_Target0 { return 1; }
                        ENDHLSL
                    }
                }
            }
            """);
        var materialPath = workspace.Write("Assets/MissingBinding.arismaterial", $$"""
            Name: Tests/MissingBinding
            Shader:
              Guid: {{shaderGuid:D}}
            Texture2DRefs:
            - Name: BaseColor
              Texture:
                Guid: {{Guid.NewGuid():D}}
                Name: Tests/BaseColor
            """);

        db.AddAsset(shaderGuid, ShaderAssetCooker.ShaderSourceAssetType, shaderPath);
        db.AddAsset(materialGuid, "Material", materialPath);

        var inspection = MaterialAssetLoader.InspectSource(db, materialGuid);
        Assert.False(inspection.IsShaderContractValid);
        Assert.Collection(
            inspection.ShaderContractDiagnostics,
            diagnostic =>
            {
                Assert.Equal(MaterialShaderContractBindingKind.Texture2DRef, diagnostic.BindingKind);
                Assert.Equal("Normal", diagnostic.BindingName);
            },
            diagnostic =>
            {
                Assert.Equal(MaterialShaderContractBindingKind.ScalarProperty, diagnostic.BindingKind);
                Assert.Equal("RoughnessFactor", diagnostic.BindingName);
            },
            diagnostic =>
            {
                Assert.Equal(MaterialShaderContractBindingKind.Vector4Property, diagnostic.BindingKind);
                Assert.Equal("BaseColorFactor", diagnostic.BindingName);
            });

        var error = Assert.Throws<InvalidOperationException>(() => MaterialAssetLoader.LoadSource(db, materialGuid));
        Assert.Contains("Texture2DRefs", error.Message, StringComparison.Ordinal);
        Assert.Contains("Normal", error.Message, StringComparison.Ordinal);
        Assert.Contains("ScalarProperties", error.Message, StringComparison.Ordinal);
        Assert.Contains("RoughnessFactor", error.Message, StringComparison.Ordinal);
        Assert.Contains("Vector4Properties", error.Message, StringComparison.Ordinal);
        Assert.Contains("BaseColorFactor", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MaterialSourceEditor_UpdatesBindingsAndPreservesUnrelatedYaml()
    {
        using var workspace = TestWorkspace.Create();
        var shaderGuid = Guid.NewGuid();
        var oldTextureGuid = Guid.NewGuid();
        var newTextureGuid = Guid.NewGuid();
        var materialGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));

        var shaderPath = workspace.Write("Assets/Editable.shader", """
            Shader "Tests/Editable"
            {
                MaterialContract
                {
                    Texture2D BaseColor
                    Scalar RoughnessFactor
                    Vector4 BaseColorFactor
                }

                SubShader
                {
                    Pass
                    {
                        HLSLPROGRAM
                        #pragma vertex VSMain
                        #pragma fragment PSMain
                        float4 VSMain() : SV_Position { return 0; }
                        float4 PSMain() : SV_Target0 { return 1; }
                        ENDHLSL
                    }
                }
            }
            """);
        var oldTexturePath = workspace.Write("Assets/Old.ppm", "P3\n1 1\n255\n255 255 255\n");
        var newTexturePath = workspace.WriteBinary("Assets/New.png", CreateTinyPng());
        var materialPath = workspace.Write("Assets/Editable.arismaterial", $$"""
            Name: Tests/Editable
            Shader:
              Guid: {{shaderGuid:D}}
            Texture2DRefs:
            - Name: BaseColor
              Slot: 7
              Texture:
                Guid: {{oldTextureGuid:D}}
                Name: Tests/Old
                Variant:
                  Format: R8G8B8A8UNorm
                  ColorSpace: SRgb
                  GenerateMipMaps: true
                SourceFormat: PpmP3
              Sampler:
                MinFilter: Nearest
                MagFilter: Linear
                MipmapMode: Linear
                WrapU: MirroredRepeat
                WrapV: ClampToEdge
              Transform:
                Offset: { X: 0.25, Y: 0.5 }
                Scale: { X: 2.0, Y: 0.75 }
                Rotation: 0.3
                TexCoord: 1
            ScalarProperties:
            - Name: RoughnessFactor
              Value: 0.25
            Vector4Properties:
            - Name: BaseColorFactor
              Value: { X: 1, Y: 1, Z: 1, W: 1 }
            EditorData:
              PreserveMe: yes
              Nested:
                Value: 17
            """);

        db.AddAsset(shaderGuid, ShaderAssetCooker.ShaderSourceAssetType, shaderPath);
        db.AddAsset(oldTextureGuid, "Texture2D", oldTexturePath);
        db.AddAsset(newTextureGuid, "Texture2D", newTexturePath);
        db.AddAsset(materialGuid, "Material", materialPath);

        var textureEdit = MaterialSourceAssetEditor.UpdateTexture2DRef(
            materialPath,
            MaterialTextureSlots.BaseColor,
            new MaterialTextureSourceReference(newTextureGuid, "Tests/New", Texture2DSourceFormat.ImageFile));
        var scalarEdit = MaterialSourceAssetEditor.UpdateScalarProperty(
            materialPath,
            MaterialPropertySlots.RoughnessFactor,
            0.625f);
        var vectorEdit = MaterialSourceAssetEditor.UpdateVector4Property(
            materialPath,
            MaterialPropertySlots.BaseColorFactor,
            new Vector4(0.1f, 0.2f, 0.3f, 0.4f));

        Assert.True(textureEdit.Success, textureEdit.Diagnostic);
        Assert.True(scalarEdit.Success, scalarEdit.Diagnostic);
        Assert.True(vectorEdit.Success, vectorEdit.Diagnostic);

        var loaded = MaterialAssetLoader.LoadSource(db, materialGuid);
        var texture = Assert.Single(loaded.Texture2DRefs);
        Assert.Equal(newTextureGuid, texture.Texture.Guid);
        Assert.Equal("Tests/New", texture.Texture.Name);
        Assert.Equal(Texture2DSourceFormat.ImageFile, texture.Texture.SourceFormat);
        Assert.Equal(Texture2DColorSpace.SRgb, texture.Texture.Variant.ColorSpace);
        Assert.True(texture.Texture.Variant.GenerateMipMaps);
        Assert.Equal(7u, texture.Slot);
        Assert.Equal(MaterialTextureFilter.Nearest, texture.ResolvedSampler.MinFilter);
        Assert.Equal(MaterialTextureWrapMode.ClampToEdge, texture.ResolvedSampler.WrapV);
        Assert.Equal(new Vector2(0.25f, 0.5f), texture.ResolvedTransform.Offset);
        Assert.Equal(new Vector2(2.0f, 0.75f), texture.ResolvedTransform.Scale);
        Assert.Equal(0.3f, texture.ResolvedTransform.Rotation);
        Assert.Equal(1u, texture.ResolvedTransform.TexCoord);
        Assert.Equal(0.625f, Assert.Single(loaded.ScalarProperties).Value);
        Assert.Equal(new Vector4(0.1f, 0.2f, 0.3f, 0.4f), Assert.Single(loaded.Vector4Properties).Value);

        var editedYaml = File.ReadAllText(materialPath);
        Assert.Contains("EditorData:", editedYaml, StringComparison.Ordinal);
        Assert.Contains("PreserveMe: yes", editedYaml, StringComparison.Ordinal);
        Assert.Contains("Value: 17", editedYaml, StringComparison.Ordinal);

        var beforeInvalidEdits = editedYaml;
        Assert.False(MaterialSourceAssetEditor.UpdateTexture2DRef(
            materialPath,
            "MissingTexture",
            new MaterialTextureSourceReference(oldTextureGuid, "Tests/Old", Texture2DSourceFormat.PpmP3)).Success);
        Assert.False(MaterialSourceAssetEditor.UpdateScalarProperty(materialPath, "MissingScalar", 1.0f).Success);
        Assert.False(MaterialSourceAssetEditor.UpdateVector4Property(materialPath, "MissingVector", Vector4.One).Success);
        Assert.Equal(beforeInvalidEdits, File.ReadAllText(materialPath));
    }

    [Fact]
    public void MaterialPropertyCommand_ExecuteUndoIsDeterministicAndGeneratedSourcesAreReadOnly()
    {
        using var workspace = TestWorkspace.Create();
        var materialGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var materialPath = workspace.Write("Assets/Command.arismaterial", """
            Name: Tests/Command
            ScalarProperties:
            - Name: RoughnessFactor
              Value: 0.2
            """);
        SerializationUtil.Serialize(
            new AssetMetadata { Guid = materialGuid, AssetType = "Material" },
            materialPath + ".meta");
        File.AppendAllText(materialPath + ".meta", "ImporterType: LegacyMaterialImporter\n");
        db.AddAsset(materialGuid, "Material", materialPath);
        Assert.True(db.TryGetAsset(materialGuid, out var sourceAsset));

        var applied = new List<float>();
        var changeCount = 0;
        db.AssetChanged += _ => changeCount++;
        var command = new ModifyMaterialScalarPropertyCommand(
            db,
            sourceAsset,
            MaterialPropertySlots.RoughnessFactor,
            0.2f,
            0.8f,
            applied.Add);

        command.Execute();
        Assert.Contains("Value: 0.8", File.ReadAllText(materialPath), StringComparison.Ordinal);
        command.Undo();
        Assert.Contains("Value: 0.2", File.ReadAllText(materialPath), StringComparison.Ordinal);
        command.Execute();
        Assert.Contains("Value: 0.8", File.ReadAllText(materialPath), StringComparison.Ordinal);
        Assert.Equal(new[] { 0.8f, 0.2f, 0.8f }, applied);
        Assert.Equal(3, changeCount);

        SerializationUtil.Serialize(
            new AssetMetadata
            {
                Guid = materialGuid,
                AssetType = "Material",
                Importer = "GltfMaterialImporter",
                Generated = new GeneratedAssetMetadata
                {
                    SourceGuid = Guid.NewGuid(),
                    SourcePackageId = "com.arisen.test",
                    ChildKind = "material",
                    ChildKey = "materials/0",
                    GeneratedByImporter = "GltfMaterialImporter"
                }
            },
            materialPath + ".meta");
        var beforeGeneratedEdit = File.ReadAllText(materialPath);

        var generatedError = Assert.Throws<InvalidOperationException>(() => command.Undo());
        Assert.Contains("read-only", generatedError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeGeneratedEdit, File.ReadAllText(materialPath));
        Assert.Equal(3, changeCount);
    }

    [Fact]
    public void MeshCooker_WritesMetadataBoundsAndSubmeshTable()
    {
        using var workspace = TestWorkspace.Create();
        var meshGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var meshPath = workspace.Write("Assets/Triangle.armesh", """
            v -1 -2 0 0 0 1 0 0
            v 3 0 2 1 0 0 1 0
            v 0 4 -1 0 1 0 0 1
            i 0 1 2
            s 0 3 7
            """);
        db.AddAsset(meshGuid, "Mesh", meshPath);

        var mesh = new MeshAsset(meshGuid, "Tests/Triangle", MeshVariantKey.Default);
        var cooked = MeshAssetCooker.LoadOrCook(db, mesh);
        var bytes = db.GetCookedAssetBytes(cooked.Handle);
        Span<MeshSubmesh> submeshes = stackalloc MeshSubmesh[checked((int)cooked.SubmeshCount)];
        MeshAssetCooker.ReadSubmeshes(bytes.Span, cooked, submeshes);

        Assert.True(cooked.IsValid);
        Assert.Equal(3u, cooked.VertexCount);
        Assert.Equal(3u, cooked.IndexCount);
        Assert.Equal(MeshAssetCooker.StaticMeshVertexStride, cooked.VertexStride);
        Assert.Equal(60u, cooked.VertexStride);
        Assert.Equal(new Vector3(-1, -2, -1), cooked.Bounds.Min);
        Assert.Equal(new Vector3(3, 4, 2), cooked.Bounds.Max);
        Assert.Equal(1u, cooked.SubmeshCount);
        Assert.Equal(0u, submeshes[0].FirstIndex);
        Assert.Equal(3u, submeshes[0].IndexCount);
        Assert.Equal(7u, submeshes[0].MaterialSlot);
        Assert.Equal(1.0f, ReadSingle(bytes.Span, checked((int)cooked.VertexDataOffset) + 24));
        Assert.Equal(0.0f, ReadSingle(bytes.Span, checked((int)cooked.VertexDataOffset) + 28));
        Assert.Equal(0.0f, ReadSingle(bytes.Span, checked((int)cooked.VertexDataOffset) + 32));
        Assert.Equal(1.0f, ReadSingle(bytes.Span, checked((int)cooked.VertexDataOffset) + 36));
    }

    [Fact]
    public void MeshCooker_ReadCookedBoundsBalancesTemporaryHandle()
    {
        using var workspace = TestWorkspace.Create();
        var meshGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(
            AssetSourceAccessMode.Diagnostic,
            Path.Combine(workspace.Root, "Cooked"));
        var meshPath = workspace.Write("Assets/Bounds.armesh", """
            v -7 -3 -2 0 0 1 0 0
            v 5 11 4 1 0 0 1 0
            v 0 2 8 0 1 0 0 1
            i 0 1 2
            """);
        db.AddAsset(meshGuid, "Mesh", meshPath);

        var mesh = new MeshAsset(meshGuid, "Tests/Bounds", MeshVariantKey.Default);
        CookedMesh cooked = MeshAssetCooker.LoadOrCook(db, mesh);
        db.Release(cooked.Handle);
        Assert.Empty(db.GetLoadedCookedAssetDiagnostics());

        Assert.True(MeshAssetCooker.TryReadCookedBounds(db, meshGuid, out MeshBounds bounds));
        Assert.Equal(new Vector3(-7.0f, -3.0f, -2.0f), bounds.Min);
        Assert.Equal(new Vector3(5.0f, 11.0f, 8.0f), bounds.Max);
        Assert.Empty(db.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void MeshCooker_ObjDiagnosticsIncludeLineAndFaceToken()
    {
        using var workspace = TestWorkspace.Create();
        var meshGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var meshPath = workspace.Write("Assets/Broken.obj", """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f 1 2/not-a-number 3
            """);
        db.AddAsset(meshGuid, "Mesh", meshPath);

        var mesh = new MeshAsset(meshGuid, "Tests/BrokenObj", MeshVariantKey.Default, MeshSourceFormat.WavefrontObj);
        var error = Assert.Throws<InvalidOperationException>(() => MeshAssetCooker.LoadOrCook(db, mesh));

        Assert.Contains("line 4", error.Message, StringComparison.Ordinal);
        Assert.Contains("2/not-a-number", error.Message, StringComparison.Ordinal);
        Assert.Contains("texcoord index", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MeshCooker_GltfDiagnosticsIncludeArrayPropertyContext()
    {
        using var workspace = TestWorkspace.Create();
        var meshGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var gltfPath = workspace.Write("Assets/Broken.gltf", """
            {
              "asset": { "version": "2.0" },
              "buffers": [
                { "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAA", "byteLength": 12 }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 12 }
              ],
              "accessors": [
                { "componentType": 5126, "count": 1, "type": "VEC3" }
              ],
              "meshes": [
                {
                  "primitives": [
                    { "attributes": { "POSITION": 0 } }
                  ]
                }
              ]
            }
            """);
        db.AddAsset(meshGuid, "Mesh", gltfPath);

        var mesh = new MeshAsset(meshGuid, "Tests/BrokenGltf", MeshVariantKey.Default, MeshSourceFormat.GltfJson);
        var error = Assert.Throws<InvalidOperationException>(() => MeshAssetCooker.LoadOrCook(db, mesh));

        Assert.Contains("accessors[0].bufferView", error.Message, StringComparison.Ordinal);
        Assert.Contains(gltfPath, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MeshCooker_GltfRejectsUnsupportedExtraUvChannels()
    {
        using var workspace = TestWorkspace.Create();
        var meshGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var gltfPath = workspace.Write("Assets/ExtraUv.gltf", """
            {
              "asset": { "version": "2.0" },
              "buffers": [
                { "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAA", "byteLength": 12 }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 12 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 1, "type": "VEC3" },
                { "bufferView": 0, "componentType": 5126, "count": 1, "type": "VEC2" }
              ],
              "meshes": [
                {
                  "primitives": [
                    { "attributes": { "POSITION": 0, "TEXCOORD_1": 1 } }
                  ]
                }
              ]
            }
            """);
        db.AddAsset(meshGuid, "Mesh", gltfPath);

        var mesh = new MeshAsset(meshGuid, "Tests/ExtraUv", MeshVariantKey.Default, MeshSourceFormat.GltfJson);
        var error = Assert.Throws<NotSupportedException>(() => MeshAssetCooker.LoadOrCook(db, mesh));

        Assert.Contains("TEXCOORD_1", error.Message, StringComparison.Ordinal);
        Assert.Contains("TEXCOORD_0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MeshCooker_LoadsFirstGltfStaticMeshScope()
    {
        using var workspace = TestWorkspace.Create();
        var meshGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var binPath = workspace.WriteBinary("Assets/Triangle.bin", CreateGltfTriangleBuffer(1.0f));
        var gltfPath = workspace.Write("Assets/Triangle.gltf", $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [
                { "uri": "{{Path.GetFileName(binPath)}}", "byteLength": 114 }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 72, "byteLength": 24 },
                { "buffer": 0, "byteOffset": 96, "byteLength": 12 },
                { "buffer": 0, "byteOffset": 108, "byteLength": 6 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
                { "bufferView": 3, "componentType": 5121, "normalized": true, "count": 3, "type": "VEC4" },
                { "bufferView": 4, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ],
              "meshes": [
                {
                  "primitives": [
                    {
                      "attributes": {
                        "POSITION": 0,
                        "NORMAL": 1,
                        "TEXCOORD_0": 2,
                        "COLOR_0": 3
                      },
                      "indices": 4,
                      "material": 3
                    }
                  ]
                }
              ]
            }
            """);
        db.AddAsset(meshGuid, "Mesh", gltfPath);

        var mesh = new MeshAsset(meshGuid, "Tests/GltfTriangle", MeshVariantKey.Default, MeshSourceFormat.GltfJson);
        var cooked = MeshAssetCooker.LoadOrCook(db, mesh);
        var bytes = db.GetCookedAssetBytes(cooked.Handle);
        Span<MeshSubmesh> submeshes = stackalloc MeshSubmesh[checked((int)cooked.SubmeshCount)];
        MeshAssetCooker.ReadSubmeshes(bytes.Span, cooked, submeshes);

        Assert.True(cooked.IsValid);
        Assert.Equal(3u, cooked.VertexCount);
        Assert.Equal(3u, cooked.IndexCount);
        Assert.Equal(new Vector3(0, 0, 0), cooked.Bounds.Min);
        Assert.Equal(new Vector3(1, 1, 0), cooked.Bounds.Max);
        Assert.Equal(1u, cooked.SubmeshCount);
        Assert.Equal(0u, submeshes[0].FirstIndex);
        Assert.Equal(3u, submeshes[0].IndexCount);
        Assert.Equal(0u, submeshes[0].MaterialSlot);
    }

    [Fact]
    public void MeshCooker_AppliesGltfNodeHierarchyTransforms()
    {
        using var workspace = TestWorkspace.Create();
        var meshGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var binPath = workspace.WriteBinary("Assets/Triangle.bin", CreateGltfTriangleBuffer(1.0f));
        var gltfPath = workspace.Write("Assets/NodeTriangle.gltf", $$"""
            {
              "asset": { "version": "2.0" },
              "scene": 0,
              "scenes": [
                { "nodes": [0] }
              ],
              "nodes": [
                { "translation": [2, 0, 0], "children": [1] },
                { "translation": [0, 3, 0], "mesh": 0 }
              ],
              "buffers": [
                { "uri": "{{Path.GetFileName(binPath)}}", "byteLength": 114 }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 72, "byteLength": 24 },
                { "buffer": 0, "byteOffset": 96, "byteLength": 12 },
                { "buffer": 0, "byteOffset": 108, "byteLength": 6 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
                { "bufferView": 3, "componentType": 5121, "normalized": true, "count": 3, "type": "VEC4" },
                { "bufferView": 4, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ],
              "meshes": [
                {
                  "primitives": [
                    {
                      "attributes": {
                        "POSITION": 0,
                        "NORMAL": 1,
                        "TEXCOORD_0": 2,
                        "COLOR_0": 3
                      },
                      "indices": 4,
                      "material": 3
                    }
                  ]
                }
              ]
            }
            """);
        db.AddAsset(meshGuid, "Mesh", gltfPath);

        var mesh = new MeshAsset(meshGuid, "Tests/GltfNodeTriangle", MeshVariantKey.Default, MeshSourceFormat.GltfJson);
        var cooked = MeshAssetCooker.LoadOrCook(db, mesh);

        Assert.Equal(new Vector3(2, 3, 0), cooked.Bounds.Min);
        Assert.Equal(new Vector3(3, 4, 0), cooked.Bounds.Max);
    }

    [Fact]
    public void MeshCooker_LoadsFirstGlbStaticMeshScope()
    {
        using var workspace = TestWorkspace.Create();
        var meshGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var glbPath = workspace.WriteBinary("Assets/Triangle.glb", CreateGltfTriangleGlb(1.5f));
        db.AddAsset(meshGuid, "Mesh", glbPath);

        var mesh = new MeshAsset(meshGuid, "Tests/GlbTriangle", MeshVariantKey.Default, MeshSourceFormat.GltfBinary);
        var cooked = MeshAssetCooker.LoadOrCook(db, mesh);
        var bytes = db.GetCookedAssetBytes(cooked.Handle);
        Span<MeshSubmesh> submeshes = stackalloc MeshSubmesh[checked((int)cooked.SubmeshCount)];
        MeshAssetCooker.ReadSubmeshes(bytes.Span, cooked, submeshes);

        Assert.True(cooked.IsValid);
        Assert.Equal(3u, cooked.VertexCount);
        Assert.Equal(3u, cooked.IndexCount);
        Assert.Equal(new Vector3(0, 0, 0), cooked.Bounds.Min);
        Assert.Equal(new Vector3(1.5f, 1.5f, 0), cooked.Bounds.Max);
        Assert.Equal(1u, cooked.SubmeshCount);
        Assert.Equal(0u, submeshes[0].FirstIndex);
        Assert.Equal(3u, submeshes[0].IndexCount);
        Assert.Equal(0u, submeshes[0].MaterialSlot);
    }

    [Fact]
    public void MeshCooker_RecooksGltfWhenExternalBufferChanges()
    {
        using var workspace = TestWorkspace.Create();
        var meshGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, Path.Combine(workspace.Root, "Cooked"));
        var binPath = workspace.WriteBinary("Assets/Triangle.bin", CreateGltfTriangleBuffer(1.0f));
        var gltfPath = workspace.Write("Assets/Triangle.gltf", $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [
                { "uri": "{{Path.GetFileName(binPath)}}", "byteLength": 114 }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 72, "byteLength": 24 },
                { "buffer": 0, "byteOffset": 96, "byteLength": 12 },
                { "buffer": 0, "byteOffset": 108, "byteLength": 6 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
                { "bufferView": 3, "componentType": 5121, "normalized": true, "count": 3, "type": "VEC4" },
                { "bufferView": 4, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ],
              "meshes": [
                {
                  "primitives": [
                    {
                      "attributes": {
                        "POSITION": 0,
                        "NORMAL": 1,
                        "TEXCOORD_0": 2,
                        "COLOR_0": 3
                      },
                      "indices": 4,
                      "material": 3
                    }
                  ]
                }
              ]
            }
            """);
        db.AddAsset(meshGuid, "Mesh", gltfPath);
        var mesh = new MeshAsset(meshGuid, "Tests/GltfTriangle", MeshVariantKey.Default, MeshSourceFormat.GltfJson);

        var firstCook = MeshAssetCooker.LoadOrCook(db, mesh);
        Assert.Equal(new Vector3(1, 1, 0), firstCook.Bounds.Max);

        File.WriteAllBytes(binPath, CreateGltfTriangleBuffer(2.0f));
        File.SetLastWriteTimeUtc(binPath, File.GetLastWriteTimeUtc(db.GetCookedArtifactPath(meshGuid, mesh.Variant.GetCookedVariant(), ".mesh")).AddSeconds(2));

        var secondCook = MeshAssetCooker.LoadOrCook(db, mesh);

        Assert.Equal(new Vector3(2, 2, 0), secondCook.Bounds.Max);
    }

    private static byte[] CreateTinyPng()
    {
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/lTEpGAAAAABJRU5ErkJggg==");
    }

    private static AssetRecord CreateModelReimportValidationSourceAsset(
        TestWorkspace workspace,
        TestAssetDatabase db,
        Guid sourceGuid,
        Guid shaderGuid,
        string packageId)
    {
        var shaderPath = workspace.Write(
            "Assets/Shaders/ReimportValidation.shader",
            CreateModelReimportValidationShader());
        workspace.WriteBinary(
            "Assets/Models/ReimportValidation/Source/Triangle.bin",
            CreateGltfTriangleBuffer(1.0f));
        workspace.Write(
            "Assets/Models/ReimportValidation/Source/BaseColor.ppm",
            "P3\n1 1\n255\n210 120 45\n");
        workspace.Write(
            "Assets/Models/ReimportValidation/Source/Normal.ppm",
            "P3\n1 1\n255\n128 128 255\n");
        workspace.Write(
            "Assets/Models/ReimportValidation/Source/Packed.ppm",
            "P3\n1 1\n255\n220 96 180\n");
        workspace.Write(
            "Assets/Models/ReimportValidation/Source/ReimportValidation.gltf",
            CreateModelReimportValidationGltf());
        var modelPath = workspace.Write("Assets/Models/ReimportValidation/ReimportValidation.arismodel", $$"""
            Name: ReimportValidation
            Source:
              Path: Source/ReimportValidation.gltf
              Format: GltfJson
            Import:
              OutputRoot: Assets/Generated/ReimportValidation
              SceneIndex: 0
              UnitScale: 1.0
              EmitTextures: true
            Shader:
              Guid: {{shaderGuid:D}}
              Name: Tests/ReimportValidation
            """);
        SerializationUtil.Serialize(
            new AssetMetadata
            {
                Guid = sourceGuid,
                AssetType = ModelSourceAssetLoader.ModelAssetType,
                Importer = "ArisenModelImporter"
            },
            modelPath + ".meta");

        db.AddAsset(shaderGuid, ShaderAssetCooker.ShaderSourceAssetType, shaderPath, packageId);
        db.AddAsset(sourceGuid, ModelSourceAssetLoader.ModelAssetType, modelPath, packageId);
        Assert.True(db.TryGetAsset(sourceGuid, out var sourceAsset));
        return sourceAsset;
    }

    private static string CreateModelReimportValidationGltf()
    {
        return """
            {
              "asset": { "version": "2.0" },
              "scene": 0,
              "scenes": [
                { "name": "ReimportValidationScene", "nodes": [0] }
              ],
              "nodes": [
                { "name": "ValidationTriangle", "mesh": 0 }
              ],
              "buffers": [
                { "uri": "Triangle.bin", "byteLength": 114 }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 72, "byteLength": 24 },
                { "buffer": 0, "byteOffset": 96, "byteLength": 12 },
                { "buffer": 0, "byteOffset": 108, "byteLength": 6 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
                { "bufferView": 3, "componentType": 5121, "normalized": true, "count": 3, "type": "VEC4" },
                { "bufferView": 4, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ],
              "images": [
                { "uri": "BaseColor.ppm" },
                { "uri": "Normal.ppm" },
                { "uri": "Packed.ppm" }
              ],
              "textures": [
                { "source": 0 },
                { "source": 1 },
                { "source": 2 },
                { "source": 2 }
              ],
              "materials": [
                {
                  "name": "ValidationMaterial",
                  "pbrMetallicRoughness": {
                    "baseColorTexture": { "index": 0 },
                    "metallicRoughnessTexture": { "index": 2 },
                    "metallicFactor": 0.7,
                    "roughnessFactor": 0.35
                  },
                  "normalTexture": { "index": 1 },
                  "occlusionTexture": { "index": 3, "strength": 0.8 }
                }
              ],
              "meshes": [
                {
                  "primitives": [
                    {
                      "attributes": {
                        "POSITION": 0,
                        "NORMAL": 1,
                        "TEXCOORD_0": 2,
                        "COLOR_0": 3
                      },
                      "indices": 4,
                      "material": 0
                    }
                  ]
                }
              ]
            }
            """;
    }

    private static string CreateModelReimportValidationShader()
    {
        return """
            Shader "Tests/ReimportValidation"
            {
                MaterialContract
                {
                    Texture2D BaseColor
                    Texture2D Normal
                    Texture2D MetallicRoughness
                    Texture2D Occlusion
                    Scalar MetallicFactor
                    Scalar RoughnessFactor
                    Scalar OcclusionStrength
                    Scalar AlphaCutoff
                    Vector4 BaseColorFactor
                    Vector4 EmissiveFactor
                }

                SubShader
                {
                    Pass
                    {
                        HLSLPROGRAM
                        #pragma vertex VSMain
                        #pragma fragment PSMain
                        #pragma shader_feature USE_NORMAL_MAP
                        float4 VSMain() : SV_Position { return 0; }
                        float4 PSMain() : SV_Target0 { return 1; }
                        ENDHLSL
                    }
                }
            }
            """;
    }

    private static string[] GetEmittedSourcePaths(ModelSourceReimportResult result)
    {
        return result.Emission.ScenePaths
            .Concat(result.Emission.MeshPaths)
            .Concat(result.Emission.MaterialPaths)
            .Concat(result.Emission.TexturePaths)
            .ToArray();
    }

    private static AssetMetadata[] IndexGeneratedAssets(
        TestAssetDatabase db,
        ModelSourceReimportResult result,
        IReadOnlyList<string> emittedPaths)
    {
        var metadata = new AssetMetadata[emittedPaths.Count];
        for (int i = 0; i < emittedPaths.Count; i++)
        {
            var sourcePath = emittedPaths[i];
            metadata[i] = SerializationUtil.Deserialize<AssetMetadata>(
                sourcePath + ".meta",
                serializeIfNotExist: false);
            db.AddAsset(
                metadata[i].Guid,
                metadata[i].AssetType,
                sourcePath,
                result.Plan.PackageId);
        }

        return metadata;
    }

    private static AssetRecord CreateModelSourceAsset(
        TestWorkspace workspace,
        TestAssetDatabase db,
        Guid sourceGuid,
        Guid shaderGuid,
        string outputRoot)
    {
        workspace.WriteBinary("Assets/Models/Robot/Source/Robot.glb", CreateGltfSceneTriangleGlb(1.0f));
        var modelPath = workspace.Write("Assets/Models/Robot/Robot.arismodel", $$"""
            Name: Robot
            Source:
              Path: Source/Robot.glb
              Format: GltfBinary
            Import:
              OutputRoot: {{outputRoot}}
              SceneIndex: 0
              UnitScale: 1.0
              EmitTextures: false
            Shader:
              Guid: {{shaderGuid:D}}
              Name: Tests/StandardLit
            """);
        db.AddAsset(sourceGuid, "Model", modelPath, "com.arisen.test");
        Assert.True(db.TryGetAsset(sourceGuid, out var sourceAsset));
        return sourceAsset;
    }

    private static AssetRecord CreateTexturedModelSourceAsset(
        TestWorkspace workspace,
        TestAssetDatabase db,
        Guid sourceGuid,
        Guid shaderGuid,
        float roughnessFactor)
    {
        var shaderPath = workspace.Write("Assets/Shaders/StandardLit.shader", CreateStandardLitShader());
        workspace.Write(
            "Assets/Models/TexturedRobot/Source/BaseColor.ppm",
            "P3\n1 1\n255\n255 0 0\n");
        WriteTexturedModelGltf(workspace, roughnessFactor);
        var modelPath = workspace.Write("Assets/Models/TexturedRobot/TexturedRobot.arismodel", $$"""
            Name: TexturedRobot
            Source:
              Path: Source/TexturedRobot.gltf
              Format: GltfJson
            Import:
              OutputRoot: Assets/Generated/TexturedRobot
              SceneIndex: 0
              UnitScale: 1.0
              EmitTextures: true
            Shader:
              Guid: {{shaderGuid:D}}
              Name: Tests/StandardLit
            """);

        db.AddAsset(shaderGuid, ShaderAssetCooker.ShaderSourceAssetType, shaderPath);
        db.AddAsset(sourceGuid, ModelSourceAssetLoader.ModelAssetType, modelPath);
        Assert.True(db.TryGetAsset(sourceGuid, out var sourceAsset));
        return sourceAsset;
    }

    private static string WriteTexturedModelGltf(TestWorkspace workspace, float roughnessFactor)
    {
        var roughness = roughnessFactor.ToString(
            "0.00",
            System.Globalization.CultureInfo.InvariantCulture);
        return workspace.Write("Assets/Models/TexturedRobot/Source/TexturedRobot.gltf", $$"""
            {
              "asset": { "version": "2.0" },
              "materials": [
                {
                  "name": "TexturedPaint",
                  "pbrMetallicRoughness": {
                    "baseColorTexture": { "index": 0 },
                    "metallicFactor": 0.5,
                    "roughnessFactor": {{roughness}}
                  }
                }
              ],
              "textures": [
                { "source": 0 }
              ],
              "images": [
                { "uri": "BaseColor.ppm" }
              ]
            }
            """);
    }

    private static string CreateStandardLitShader()
    {
        return """
            Shader "Tests/StandardLit"
            {
                MaterialContract
                {
                    Texture2D BaseColor
                    Scalar MetallicFactor
                    Scalar RoughnessFactor
                    Vector4 BaseColorFactor
                }

                SubShader
                {
                    Pass
                    {
                        HLSLPROGRAM
                        #pragma vertex VSMain
                        #pragma fragment PSMain
                        float4 VSMain() : SV_Position { return 0; }
                        float4 PSMain() : SV_Target0 { return 1; }
                        ENDHLSL
                    }
                }
            }
            """;
    }

    private static byte[] CreateGltfImageGlb(byte[] image)
    {
        var json = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [
                { "byteLength": {{image.Length}} }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": {{image.Length}} }
              ],
              "materials": [
                {
                  "name": "EmbeddedPaint",
                  "pbrMetallicRoughness": {
                    "baseColorTexture": { "index": 0 }
                  }
                }
              ],
              "textures": [
                { "source": 0 }
              ],
              "images": [
                { "bufferView": 0, "mimeType": "image/png" }
              ]
            }
            """;
        var jsonBytes = PadTo4(System.Text.Encoding.UTF8.GetBytes(json), 0x20);
        var binBytes = PadTo4(image, 0);
        var totalLength = checked(12 + 8 + jsonBytes.Length + 8 + binBytes.Length);
        var glb = new byte[totalLength];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0, sizeof(uint)), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4, sizeof(uint)), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8, sizeof(uint)), checked((uint)totalLength));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12, sizeof(uint)), checked((uint)jsonBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16, sizeof(uint)), 0x4E4F534A);
        jsonBytes.CopyTo(glb.AsSpan(20));
        var binHeaderOffset = 20 + jsonBytes.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binHeaderOffset, sizeof(uint)), checked((uint)binBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binHeaderOffset + 4, sizeof(uint)), 0x004E4942);
        binBytes.CopyTo(glb.AsSpan(binHeaderOffset + 8));
        return glb;
    }

    private static byte[] CreateGltfSceneTriangleGlb(float scale)
    {
        var bin = CreateGltfTriangleBuffer(scale);
        var json = $$"""
            {
              "asset": { "version": "2.0" },
              "scene": 0,
              "scenes": [
                { "name": "GeneratedShowcase", "nodes": [0] }
              ],
              "nodes": [
                {
                  "name": "TriangleNode",
                  "translation": [2.0, 3.0, 4.0],
                  "scale": [2.0, 2.0, 2.0],
                  "mesh": 0
                }
              ],
              "buffers": [
                { "byteLength": {{bin.Length}} }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 72, "byteLength": 24 },
                { "buffer": 0, "byteOffset": 96, "byteLength": 12 },
                { "buffer": 0, "byteOffset": 108, "byteLength": 6 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
                { "bufferView": 3, "componentType": 5121, "normalized": true, "count": 3, "type": "VEC4" },
                { "bufferView": 4, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ],
              "materials": [
                {
                  "name": "WarmPaint",
                  "pbrMetallicRoughness": {
                    "baseColorFactor": [1.0, 0.65, 0.35, 1.0],
                    "metallicFactor": 0.0,
                    "roughnessFactor": 0.45
                  }
                }
              ],
              "meshes": [
                {
                  "primitives": [
                    {
                      "attributes": {
                        "POSITION": 0,
                        "NORMAL": 1,
                        "TEXCOORD_0": 2,
                        "COLOR_0": 3
                      },
                      "indices": 4,
                      "material": 0
                    }
                  ]
                }
              ]
            }
            """;
        var jsonBytes = PadTo4(System.Text.Encoding.UTF8.GetBytes(json), 0x20);
        var binBytes = PadTo4(bin, 0);
        var totalLength = checked(12 + 8 + jsonBytes.Length + 8 + binBytes.Length);
        var glb = new byte[totalLength];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0, sizeof(uint)), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4, sizeof(uint)), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8, sizeof(uint)), checked((uint)totalLength));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12, sizeof(uint)), checked((uint)jsonBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16, sizeof(uint)), 0x4E4F534A);
        jsonBytes.CopyTo(glb.AsSpan(20));
        var binHeaderOffset = 20 + jsonBytes.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binHeaderOffset, sizeof(uint)), checked((uint)binBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binHeaderOffset + 4, sizeof(uint)), 0x004E4942);
        binBytes.CopyTo(glb.AsSpan(binHeaderOffset + 8));
        return glb;
    }

    private static byte[] CreateGltfMultiPrimitiveSceneGlb(float scale)
    {
        var bin = CreateGltfTriangleBuffer(scale);
        var json = $$"""
            {
              "asset": { "version": "2.0" },
              "scene": 0,
              "scenes": [
                { "name": "GeneratedMultiPrimitive", "nodes": [0] }
              ],
              "nodes": [
                {
                  "name": "MultiMaterialNode",
                  "translation": [0.0, 1.0, 2.0],
                  "mesh": 0
                }
              ],
              "buffers": [
                { "byteLength": {{bin.Length}} }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 72, "byteLength": 24 },
                { "buffer": 0, "byteOffset": 96, "byteLength": 12 },
                { "buffer": 0, "byteOffset": 108, "byteLength": 6 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
                { "bufferView": 3, "componentType": 5121, "normalized": true, "count": 3, "type": "VEC4" },
                { "bufferView": 4, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ],
              "materials": [
                {
                  "name": "GoldPaint",
                  "pbrMetallicRoughness": {
                    "baseColorFactor": [1.0, 0.75, 0.25, 1.0],
                    "metallicFactor": 0.15,
                    "roughnessFactor": 0.35
                  }
                },
                {
                  "name": "BluePaint",
                  "pbrMetallicRoughness": {
                    "baseColorFactor": [0.15, 0.35, 1.0, 1.0],
                    "metallicFactor": 0.0,
                    "roughnessFactor": 0.55
                  }
                }
              ],
              "meshes": [
                {
                  "primitives": [
                    {
                      "attributes": {
                        "POSITION": 0,
                        "NORMAL": 1,
                        "TEXCOORD_0": 2,
                        "COLOR_0": 3
                      },
                      "indices": 4,
                      "material": 0
                    },
                    {
                      "attributes": {
                        "POSITION": 0,
                        "NORMAL": 1,
                        "TEXCOORD_0": 2,
                        "COLOR_0": 3
                      },
                      "indices": 4,
                      "material": 1
                    }
                  ]
                }
              ]
            }
            """;
        var jsonBytes = PadTo4(System.Text.Encoding.UTF8.GetBytes(json), 0x20);
        var binBytes = PadTo4(bin, 0);
        var totalLength = checked(12 + 8 + jsonBytes.Length + 8 + binBytes.Length);
        var glb = new byte[totalLength];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0, sizeof(uint)), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4, sizeof(uint)), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8, sizeof(uint)), checked((uint)totalLength));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12, sizeof(uint)), checked((uint)jsonBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16, sizeof(uint)), 0x4E4F534A);
        jsonBytes.CopyTo(glb.AsSpan(20));
        var binHeaderOffset = 20 + jsonBytes.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binHeaderOffset, sizeof(uint)), checked((uint)binBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binHeaderOffset + 4, sizeof(uint)), 0x004E4942);
        binBytes.CopyTo(glb.AsSpan(binHeaderOffset + 8));
        return glb;
    }

    private static byte[] CreateGltfTriangleGlb(float scale)
    {
        var bin = CreateGltfTriangleBuffer(scale);
        var json = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [
                { "byteLength": {{bin.Length}} }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 72, "byteLength": 24 },
                { "buffer": 0, "byteOffset": 96, "byteLength": 12 },
                { "buffer": 0, "byteOffset": 108, "byteLength": 6 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
                { "bufferView": 3, "componentType": 5121, "normalized": true, "count": 3, "type": "VEC4" },
                { "bufferView": 4, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ],
              "meshes": [
                {
                  "primitives": [
                    {
                      "attributes": {
                        "POSITION": 0,
                        "NORMAL": 1,
                        "TEXCOORD_0": 2,
                        "COLOR_0": 3
                      },
                      "indices": 4,
                      "material": 3
                    }
                  ]
                }
              ]
            }
            """;
        var jsonBytes = PadTo4(System.Text.Encoding.UTF8.GetBytes(json), 0x20);
        var binBytes = PadTo4(bin, 0);
        var totalLength = checked(12 + 8 + jsonBytes.Length + 8 + binBytes.Length);
        var glb = new byte[totalLength];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0, sizeof(uint)), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4, sizeof(uint)), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8, sizeof(uint)), checked((uint)totalLength));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12, sizeof(uint)), checked((uint)jsonBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16, sizeof(uint)), 0x4E4F534A);
        jsonBytes.CopyTo(glb.AsSpan(20));
        var binHeaderOffset = 20 + jsonBytes.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binHeaderOffset, sizeof(uint)), checked((uint)binBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binHeaderOffset + 4, sizeof(uint)), 0x004E4942);
        binBytes.CopyTo(glb.AsSpan(binHeaderOffset + 8));
        return glb;
    }

    private static byte[] PadTo4(byte[] bytes, byte padding)
    {
        var paddedLength = (bytes.Length + 3) & ~3;
        if (paddedLength == bytes.Length)
        {
            return bytes;
        }

        var padded = new byte[paddedLength];
        bytes.CopyTo(padded, 0);
        padded.AsSpan(bytes.Length).Fill(padding);
        return padded;
    }

    private static string GetRepositoryFile(params string[] relativeSegments)
    {
        var relativePath = Path.Combine(relativeSegments);
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{relativePath}'.");
    }

    private static byte[] CreateGltfTriangleBuffer(float scale)
    {
        var bytes = new byte[114];
        WriteVector3(bytes, 0, new Vector3(0, 0, 0));
        WriteVector3(bytes, 12, new Vector3(scale, 0, 0));
        WriteVector3(bytes, 24, new Vector3(0, scale, 0));
        WriteVector3(bytes, 36, new Vector3(0, 0, 1));
        WriteVector3(bytes, 48, new Vector3(0, 0, 1));
        WriteVector3(bytes, 60, new Vector3(0, 0, 1));
        WriteVector2(bytes, 72, new Vector2(0, 0));
        WriteVector2(bytes, 80, new Vector2(1, 0));
        WriteVector2(bytes, 88, new Vector2(0, 1));
        bytes[96] = 255;
        bytes[97] = 0;
        bytes[98] = 0;
        bytes[99] = 255;
        bytes[100] = 0;
        bytes[101] = 255;
        bytes[102] = 0;
        bytes[103] = 255;
        bytes[104] = 0;
        bytes[105] = 0;
        bytes[106] = 255;
        bytes[107] = 255;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(108, sizeof(ushort)), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(110, sizeof(ushort)), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(112, sizeof(ushort)), 2);
        return bytes;
    }

    private static void WriteVector3(byte[] bytes, int offset, Vector3 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset, sizeof(float)), value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4, sizeof(float)), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 8, sizeof(float)), value.Z);
    }

    private static void WriteVector2(byte[] bytes, int offset, Vector2 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset, sizeof(float)), value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4, sizeof(float)), value.Y);
    }

    private static float ReadSingle(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset, sizeof(float)));
    }

    private static float ReadHalf(ReadOnlySpan<byte> bytes, int offset)
    {
        return (float)BitConverter.UInt16BitsToHalf(
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort))));
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ArisenRenderingTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TestWorkspace(root);
        }

        public string Write(string relativePath, string contents)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            return path;
        }

        public string WriteBinary(string relativePath, byte[] contents)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, contents);
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}

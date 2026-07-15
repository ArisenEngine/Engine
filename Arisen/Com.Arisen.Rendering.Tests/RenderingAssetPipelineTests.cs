using System.Buffers.Binary;
using Arisen.Native.RHI;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.RHI;
using ArisenEngine.Core.Serialization;
using ArisenEngine.Rendering;
using ArisenEngine.Rendering.Resources;
using ArisenEngine.Resources.Serialization;
using Xunit;

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
                  "pbrMetallicRoughness": {
                    "baseColorFactor": [0.25, 0.5, 0.75, 1.0],
                    "baseColorTexture": { "index": 0 },
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
                  "normalTexture": { "index": 1 }
                }
              ],
              "textures": [
                { "source": 0 },
                { "source": 1 },
                { "source": 2 }
              ],
              "images": [
                { "uri": "BaseColor.png" },
                { "uri": "Normal.png" },
                { "uri": "Emissive.png" }
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
        Assert.NotNull(material.NormalTexture);
        Assert.Equal(1, material.NormalTexture.TextureIndex);
        Assert.Equal(1, material.NormalTexture.ImageIndex);
        Assert.NotNull(material.EmissiveTexture);
        Assert.Equal(2, material.EmissiveTexture.TextureIndex);
        Assert.Equal(2, material.EmissiveTexture.ImageIndex);
        Assert.Equal("Emissive.png", material.EmissiveTexture.Uri);
        Assert.Contains(plan.Warnings, warning => warning.Contains("alphaMode", StringComparison.Ordinal));
        Assert.Contains(plan.Warnings, warning => warning.Contains("animations", StringComparison.Ordinal));
        Assert.Contains(plan.Warnings, warning => warning.Contains("morph targets", StringComparison.Ordinal));
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
        var gltfPath = workspace.Write("Assets/Robot.gltf", $$"""
            {
              "asset": { "version": "2.0" },
              "materials": [
                {
                  "name": "PaintedMetal",
                  "pbrMetallicRoughness": {
                    "baseColorFactor": [0.25, 0.5, 0.75, 1.0],
                    "baseColorTexture": { "index": 0 },
                    "metallicFactor": 0.8,
                    "roughnessFactor": 0.35
                  },
                  "emissiveFactor": [0.05, 0.06, 0.07],
                  "emissiveTexture": { "index": 2 },
                  "normalTexture": { "index": 1 }
                }
              ],
              "textures": [
                { "source": 0 },
                { "source": 1 },
                { "source": 2 }
              ],
              "images": [
                { "uri": "{{Path.GetFileName(baseColorPath)}}"},
                { "uri": "{{Path.GetFileName(normalPath)}}"},
                { "uri": "{{Path.GetFileName(emissivePath)}}"}
              ]
            }
            """);
        var shaderPath = workspace.Write("Assets/StandardLit.shader", """
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
            """);

        var plan = GltfModelImportPlanner.CreatePlan(gltfPath, sourceGuid, "com.arisen.test");
        var result = GltfModelImportEmitter.Emit(
            plan,
            gltfPath,
            Path.Combine(workspace.Root, "Assets", "Generated"),
            new GltfModelImportEmissionSettings(shaderGuid, "Tests/StandardLit", "Robot"));

        var materialPath = Assert.Single(result.MaterialPaths);
        Assert.Equal(3, result.TexturePaths.Count);
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

        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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
        Assert.Equal(3, loadedMaterial.Texture2DRefs.Count);
        Assert.Contains(loadedMaterial.Texture2DRefs, texture => texture.Name == MaterialTextureSlots.BaseColor);
        Assert.Contains(loadedMaterial.Texture2DRefs, texture => texture.Name == MaterialTextureSlots.Normal);
        Assert.Contains(loadedMaterial.Texture2DRefs, texture => texture.Name == MaterialTextureSlots.Emissive);
        Assert.Equal(Texture2DSourceFormat.ImageFile, loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.BaseColor).Texture.SourceFormat);
        Assert.Equal(Texture2DSourceFormat.PpmP3, loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.Normal).Texture.SourceFormat);
        Assert.Equal(Texture2DSourceFormat.ImageFile, loadedMaterial.Texture2DRefs.Single(texture => texture.Name == MaterialTextureSlots.Emissive).Texture.SourceFormat);
        Assert.Equal(0.8f, loadedMaterial.ScalarProperties.Single(property => property.Name == MaterialPropertySlots.MetallicFactor).Value);
        Assert.Equal(0.35f, loadedMaterial.ScalarProperties.Single(property => property.Name == MaterialPropertySlots.RoughnessFactor).Value);
        Assert.Equal(
            new Vector4(0.25f, 0.5f, 0.75f, 1.0f),
            loadedMaterial.Vector4Properties.Single(property => property.Name == MaterialPropertySlots.BaseColorFactor).Value);
        Assert.Equal(
            new Vector4(0.05f, 0.06f, 0.07f, 1.0f),
            loadedMaterial.Vector4Properties.Single(property => property.Name == MaterialPropertySlots.EmissiveFactor).Value);
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

        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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

        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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
    public void StandardLitPackageAssets_DefinePbrNormalMapMaterialContract()
    {
        using var workspace = TestWorkspace.Create();
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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

        Assert.Equal("GenericRP/StandardLit", shaderSource.Name);
        Assert.Contains("float4 environmentAmbient;", shaderText, StringComparison.Ordinal);
        Assert.Contains("float4 cameraWorldPosition;", shaderText, StringComparison.Ordinal);
        Assert.Contains("float3 WorldPosition : TEXCOORD1;", shaderText, StringComparison.Ordinal);
        Assert.Contains("DistributionGGX", shaderText, StringComparison.Ordinal);
        Assert.Contains("GeometrySmith", shaderText, StringComparison.Ordinal);
        Assert.Contains("FresnelSchlick", shaderText, StringComparison.Ordinal);
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
    }

    [Fact]
    public void MaterialCooker_PreservesShaderKeywordsAndRenderStateInCookedPayload()
    {
        using var workspace = TestWorkspace.Create();
        var shaderGuid = Guid.NewGuid();
        var textureGuid = Guid.NewGuid();
        var materialGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));

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
        Assert.Equal(3u, cooked.Asset.Texture2DRefs.Single().Slot);
        Assert.Equal(0.5f, cooked.Asset.ScalarProperties.Single().Value);
        Assert.Equal(new Vector4(0.1f, 0.2f, 0.3f, 0.4f), cooked.Asset.Vector4Properties.Single().Value);
    }

    [Fact]
    public void MaterialLoader_ReportsMissingShaderContractBindings()
    {
        using var workspace = TestWorkspace.Create();
        var shaderGuid = Guid.NewGuid();
        var materialGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));

        var shaderPath = workspace.Write("Assets/MissingBinding.shader", """
            Shader "Tests/MissingBinding"
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

        var error = Assert.Throws<InvalidOperationException>(() => MaterialAssetLoader.LoadSource(db, materialGuid));
        Assert.Contains("Vector4Properties", error.Message, StringComparison.Ordinal);
        Assert.Contains("BaseColorFactor", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MeshCooker_WritesMetadataBoundsAndSubmeshTable()
    {
        using var workspace = TestWorkspace.Create();
        var meshGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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
    public void MeshCooker_ObjDiagnosticsIncludeLineAndFaceToken()
    {
        using var workspace = TestWorkspace.Create();
        var meshGuid = Guid.NewGuid();
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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
        var db = new TestAssetDatabase(Path.Combine(workspace.Root, "Cooked"));
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

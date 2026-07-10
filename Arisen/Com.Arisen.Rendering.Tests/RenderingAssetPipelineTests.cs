using System.Buffers.Binary;
using Arisen.Native.RHI;
using ArisenEngine.Rendering;
using ArisenEngine.Rendering.Resources;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderingAssetPipelineTests
{
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

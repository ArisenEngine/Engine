using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class CookedSceneAssetTests
{
    [Fact]
    public void CookedScene_IsDeterministicRegisteredAndMatchesSourceWithoutYaml()
    {
        using var temp = new TempDirectory();
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, temp.Path);
        Guid sceneGuid = Guid.Parse("10203040-5060-7080-90a0-b0c0d0e0f001");
        Guid meshGuid = Guid.Parse("10203040-5060-7080-90a0-b0c0d0e0f002");
        Guid materialGuid = Guid.Parse("10203040-5060-7080-90a0-b0c0d0e0f003");
        Guid environmentGuid = Guid.Parse("10203040-5060-7080-90a0-b0c0d0e0f004");
        string scenePath = Path.Combine(temp.Path, "CookedParity.arisenscene");
        string meshPath = Path.Combine(temp.Path, "CookedParity.armesh");
        string materialPath = Path.Combine(temp.Path, "CookedParity.arismaterial");
        string environmentPath = Path.Combine(temp.Path, "CookedParity.arienvironment");

        File.WriteAllText(scenePath, SceneTestSource.MigrateLegacy(sceneGuid, scenePath, $$"""
            Version: 1
            Name: Cooked Parity Scene
            Entities:
            - Name: Main Camera
              Transform:
                Position: { X: 1.25, Y: 2.5, Z: -7.75 }
                Rotation: { X: 0, Y: 0.258819, Z: 0, W: 0.965926 }
                Scale: { X: 1, Y: 1, Z: 1 }
              Camera:
                VerticalFov: 52
                NearPlane: 0.15
                FarPlane: 750
                IsPerspective: true
            - Name: Rendered Mesh
              Transform:
                Position: { X: -2, Y: 0.5, Z: 3 }
                Rotation: { X: 0, Y: 0, Z: 0, W: 1 }
                Scale: { X: 1.5, Y: 2, Z: 0.75 }
              MeshRenderer:
                Mesh:
                  Guid: {{meshGuid:D}}
                  PackageId: com.arisen.test
                Material:
                  Guid: {{materialGuid:D}}
                  PackageId: com.arisen.test
                FirstSubmeshIndex: 2
                SubmeshCount: 3
                BoundsCenter: { X: 0, Y: 1, Z: 0 }
                BoundsExtents: { X: 2, Y: 3, Z: 4 }
                Visible: true
            - Name: Sun
              DirectionalLight:
                Direction: { X: 0.3, Y: 0.8, Z: -0.5 }
                Color: { X: 1, Y: 0.92, Z: 0.8 }
                Intensity: 1.7
                AmbientIntensity: 0.21
                Enabled: true
            - Name: Fill
              Transform:
                Position: { X: 3, Y: 2, Z: -1 }
              PointLight:
                Color: { X: 0.45, Y: 0.7, Z: 1 }
                Intensity: 2.25
                Range: 8.5
                Enabled: true
            - Name: Spot
              Transform:
                Position: { X: -3, Y: 4, Z: -2 }
              SpotLight:
                Color: { X: 1, Y: 0.7, Z: 0.35 }
                Intensity: 3.5
                Range: 12
                InnerConeAngleDegrees: 11
                OuterConeAngleDegrees: 29
                Enabled: true
            - Name: World
              Environment:
                EnvironmentTexture:
                  Guid: {{environmentGuid:D}}
                  PackageId: com.arisen.test
                SkyColor: { X: 0.08, Y: 0.2, Z: 0.45 }
                HorizonColor: { X: 0.6, Y: 0.72, Z: 0.85 }
                GroundColor: { X: 0.04, Y: 0.05, Z: 0.08 }
                AmbientColor: { X: 0.5, Y: 0.62, Z: 0.8 }
                SkyIntensity: 0.95
                AmbientIntensity: 0.33
                Exposure: 1.4
                Enabled: true
            """));
        File.WriteAllText(meshPath, string.Empty);
        File.WriteAllText(materialPath, string.Empty);
        File.WriteAllText(environmentPath, string.Empty);
        database.AddAsset(sceneGuid, "Scene", scenePath);
        database.AddAsset(meshGuid, "Mesh", meshPath);
        database.AddAsset(materialGuid, "Material", materialPath);
        database.AddAsset(environmentGuid, "EnvironmentTexture", environmentPath);

        var sceneRef = new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test");
        var sourceWorld = new EntityManager();
        SceneLoadResult sourceResult = SceneAssetLoader.LoadScene(database, sceneRef, sourceWorld);
        Assert.True(sourceResult.Success, sourceResult.Diagnostic);

        CookedSceneArtifact firstArtifact = SceneAssetCooker.Cook(database, sceneRef);
        byte[] firstBytes = File.ReadAllBytes(firstArtifact.Path);
        CookedSceneArtifact secondArtifact = SceneAssetCooker.Cook(database, sceneRef);
        byte[] secondBytes = File.ReadAllBytes(secondArtifact.Path);

        Assert.Equal(SceneAssetCooker.RuntimeVariant, firstArtifact.Variant);
        Assert.Equal(6, firstArtifact.EntityCount);
        Assert.Equal(3, firstArtifact.AssetReferenceCount);
        Assert.Equal(firstBytes, secondBytes);
        Assert.Equal(firstArtifact.SizeInBytes, secondArtifact.SizeInBytes);
        Assert.True(database.TryGetCookedArtifact(sceneGuid, SceneAssetCooker.RuntimeVariant, out var registered));
        Assert.Equal(secondArtifact.Path, registered.Path);
        Assert.Equal(secondBytes.Length, registered.SizeInBytes);

        File.Delete(scenePath);
        var cookedWorld = new EntityManager();
        SceneLoadResult cookedResult = SceneAssetCooker.LoadCooked(database, sceneRef, cookedWorld);

        Assert.True(cookedResult.Success, cookedResult.Diagnostic);
        Assert.Equal(sourceResult.SceneName, cookedResult.SceneName);
        Assert.Equal(sourceResult.EntityCount, cookedResult.EntityCount);
        Assert.Equal(sourceResult.CameraCount, cookedResult.CameraCount);
        Assert.Equal(sourceResult.MeshRendererCount, cookedResult.MeshRendererCount);
        Assert.Equal(sourceResult.DirectionalLightCount, cookedResult.DirectionalLightCount);
        Assert.Equal(sourceResult.PointLightCount, cookedResult.PointLightCount);
        Assert.Equal(sourceResult.SpotLightCount, cookedResult.SpotLightCount);
        Assert.Equal(sourceResult.EnvironmentCount, cookedResult.EnvironmentCount);
        AssertWorldEquivalent(sourceWorld, cookedWorld);
    }

    [Fact]
    public void CookedScene_RejectsMalformedPayloadsBeforeEntityMutation()
    {
        using var temp = new TempDirectory();
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, temp.Path);
        Guid sceneGuid = Guid.Parse("20203040-5060-7080-90a0-b0c0d0e0f001");
        string scenePath = Path.Combine(temp.Path, "Corruption.arisenscene");
        File.WriteAllText(scenePath, SceneTestSource.MigrateLegacy(sceneGuid, scenePath, """
            Name: Corruption Scene
            Entities:
            - Name: Camera
              Transform:
                Position: { X: 1, Y: 2, Z: 3 }
              Camera:
                VerticalFov: 60
                NearPlane: 0.1
                FarPlane: 100
            """));
        database.AddAsset(sceneGuid, "Scene", scenePath);
        var sceneRef = new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test");
        CookedSceneArtifact artifact = SceneAssetCooker.Cook(database, sceneRef);
        byte[] validBytes = File.ReadAllBytes(artifact.Path);

        var mutations = new List<(string Name, byte[] Bytes)>
        {
            ("truncated", validBytes[..^1]),
            ("wrong version", Mutate(validBytes, bytes =>
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), 99))),
            ("wrong hash", Mutate(validBytes, bytes => bytes[SceneAssetCooker.HashOffset] ^= 0x5A)),
            ("nonzero reserved header", Mutate(validBytes, bytes => bytes[80] = 1)),
            ("wrong source guid", Mutate(validBytes, bytes => bytes[16] ^= 0x40)),
            ("overflowed section", MutateAndRehash(validBytes, bytes =>
            {
                int descriptor = FindDescriptor(bytes, CookedSceneSectionType.Metadata);
                BinaryPrimitives.WriteUInt64LittleEndian(
                    bytes.AsSpan(descriptor + 8, 8),
                    0xFFFFFFFFFFFFFFF8UL);
            })),
            ("overlapping sections", MutateAndRehash(validBytes, bytes =>
            {
                int metadataDescriptor = FindDescriptor(bytes, CookedSceneSectionType.Metadata);
                int stringsDescriptor = FindDescriptor(bytes, CookedSceneSectionType.Strings);
                ulong metadataOffset = BinaryPrimitives.ReadUInt64LittleEndian(
                    bytes.AsSpan(metadataDescriptor + 8, 8));
                BinaryPrimitives.WriteUInt64LittleEndian(
                    bytes.AsSpan(stringsDescriptor + 8, 8),
                    metadataOffset);
            })),
            ("non-finite transform", MutateAndRehash(validBytes, bytes =>
            {
                int transformDescriptor = FindDescriptor(bytes, CookedSceneSectionType.Transforms);
                int transformOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(
                    bytes.AsSpan(transformDescriptor + 8, 8)));
                BinaryPrimitives.WriteUInt32LittleEndian(
                    bytes.AsSpan(transformOffset + 4, 4),
                    0x7FC00000U);
            }))
        };

        foreach ((string name, byte[] bytes) in mutations)
        {
            File.WriteAllBytes(artifact.Path, bytes);
            var entityManager = new EntityManager();
            Entity existing = entityManager.CreateEntity();
            entityManager.AddComponent(existing, TransformComponent.Identity);

            SceneLoadResult result = SceneAssetCooker.LoadCooked(database, sceneRef, entityManager);

            Assert.False(result.Success, $"Mutation '{name}' unexpectedly loaded.");
            Assert.Contains("invalid", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
            Assert.Single(entityManager.GetAllEntities());
            Assert.Equal(1, entityManager.GetPool<TransformComponent>().Count);
        }
    }

    [Fact]
    public void CookedScene_SkipsUnknownOptionalSectionButRejectsUnknownRequiredSection()
    {
        using var temp = new TempDirectory();
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, temp.Path);
        Guid sceneGuid = Guid.Parse("30203040-5060-7080-90a0-b0c0d0e0f001");
        string scenePath = Path.Combine(temp.Path, "OptionalSection.arisenscene");
        File.WriteAllText(scenePath, SceneTestSource.MigrateLegacy(sceneGuid, scenePath, """
            Name: Optional Section Scene
            Entities:
            - Name: Empty Entity
            """));
        database.AddAsset(sceneGuid, "Scene", scenePath);
        var sceneRef = new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test");
        CookedSceneArtifact artifact = SceneAssetCooker.Cook(database, sceneRef);
        byte[] validBytes = File.ReadAllBytes(artifact.Path);

        const uint unknownSectionType = 0xF0000001;
        byte[] unknownOptional = MutateAndRehash(validBytes, bytes =>
        {
            int cameraDescriptor = FindDescriptor(bytes, CookedSceneSectionType.Cameras);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cameraDescriptor, 4), unknownSectionType);
        });
        File.WriteAllBytes(artifact.Path, unknownOptional);
        var optionalWorld = new EntityManager();
        SceneLoadResult optionalResult = SceneAssetCooker.LoadCooked(database, sceneRef, optionalWorld);

        Assert.True(optionalResult.Success, optionalResult.Diagnostic);
        Assert.Single(optionalWorld.GetAllEntities());

        byte[] unknownRequired = MutateAndRehash(unknownOptional, bytes =>
        {
            int descriptor = FindDescriptor(bytes, unknownSectionType);
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(descriptor + 4, 4),
                (uint)CookedSceneSectionFlags.Required);
        });
        File.WriteAllBytes(artifact.Path, unknownRequired);
        var requiredWorld = new EntityManager();
        SceneLoadResult requiredResult = SceneAssetCooker.LoadCooked(database, sceneRef, requiredWorld);

        Assert.False(requiredResult.Success);
        Assert.Contains("unknown required section", requiredResult.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(requiredWorld.GetAllEntities());
    }

    [Fact]
    public void SceneCooker_RejectsUnsupportedSourceSchemaWithoutRegisteringArtifact()
    {
        using var temp = new TempDirectory();
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, temp.Path);
        Guid sceneGuid = Guid.Parse("40203040-5060-7080-90a0-b0c0d0e0f001");
        string scenePath = Path.Combine(temp.Path, "FutureSchema.arisenscene");
        File.WriteAllText(scenePath, """
            Version: 3
            Name: Future Scene
            Entities:
            - Name: Entity
            """);
        database.AddAsset(sceneGuid, "Scene", scenePath);
        var sceneRef = new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test");

        var error = Assert.Throws<InvalidOperationException>(() =>
            SceneAssetCooker.Cook(database, sceneRef));

        Assert.Contains("schema version '3'", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(database.TryGetCookedArtifact(sceneGuid, SceneAssetCooker.RuntimeVariant, out _));
    }

    private static void AssertWorldEquivalent(EntityManager source, EntityManager cooked)
    {
        Assert.Equal(source.GetAllEntities().Count(), cooked.GetAllEntities().Count());

        var sourceNames = source.GetPool<NameComponent>();
        var cookedNames = cooked.GetPool<NameComponent>();
        Assert.Equal(sourceNames.Count, cookedNames.Count);
        for (int i = 0; i < sourceNames.Count; i++)
        {
            Assert.Equal(
                sourceNames.GetRawComponentArray()[i].Name,
                cookedNames.GetRawComponentArray()[i].Name);
        }

        var sourceTransforms = source.GetPool<TransformComponent>();
        var cookedTransforms = cooked.GetPool<TransformComponent>();
        Assert.Equal(sourceTransforms.Count, cookedTransforms.Count);
        for (int i = 0; i < sourceTransforms.Count; i++)
        {
            Assert.Equal(sourceTransforms.GetRawComponentArray()[i].Position, cookedTransforms.GetRawComponentArray()[i].Position);
            Assert.Equal(sourceTransforms.GetRawComponentArray()[i].Rotation, cookedTransforms.GetRawComponentArray()[i].Rotation);
            Assert.Equal(sourceTransforms.GetRawComponentArray()[i].Scale, cookedTransforms.GetRawComponentArray()[i].Scale);
        }

        var sourceCameras = source.GetPool<CameraComponent>();
        var cookedCameras = cooked.GetPool<CameraComponent>();
        Assert.Equal(sourceCameras.Count, cookedCameras.Count);
        for (int i = 0; i < sourceCameras.Count; i++)
        {
            Assert.Equal(sourceCameras.GetRawComponentArray()[i].VerticalFov, cookedCameras.GetRawComponentArray()[i].VerticalFov);
            Assert.Equal(sourceCameras.GetRawComponentArray()[i].NearPlane, cookedCameras.GetRawComponentArray()[i].NearPlane);
            Assert.Equal(sourceCameras.GetRawComponentArray()[i].FarPlane, cookedCameras.GetRawComponentArray()[i].FarPlane);
            Assert.Equal(sourceCameras.GetRawComponentArray()[i].IsPerspective, cookedCameras.GetRawComponentArray()[i].IsPerspective);
        }

        var sourceMeshes = source.GetPool<MeshRendererComponent>();
        var cookedMeshes = cooked.GetPool<MeshRendererComponent>();
        Assert.Equal(sourceMeshes.Count, cookedMeshes.Count);
        for (int i = 0; i < sourceMeshes.Count; i++)
        {
            Assert.Equal(sourceMeshes.GetRawComponentArray()[i].MeshGuid, cookedMeshes.GetRawComponentArray()[i].MeshGuid);
            Assert.Equal(sourceMeshes.GetRawComponentArray()[i].MaterialGuid, cookedMeshes.GetRawComponentArray()[i].MaterialGuid);
            Assert.Equal(sourceMeshes.GetRawComponentArray()[i].FirstSubmeshIndex, cookedMeshes.GetRawComponentArray()[i].FirstSubmeshIndex);
            Assert.Equal(sourceMeshes.GetRawComponentArray()[i].SubmeshCount, cookedMeshes.GetRawComponentArray()[i].SubmeshCount);
            Assert.Equal(sourceMeshes.GetRawComponentArray()[i].BoundsCenter, cookedMeshes.GetRawComponentArray()[i].BoundsCenter);
            Assert.Equal(sourceMeshes.GetRawComponentArray()[i].BoundsExtents, cookedMeshes.GetRawComponentArray()[i].BoundsExtents);
            Assert.Equal(sourceMeshes.GetRawComponentArray()[i].Visible, cookedMeshes.GetRawComponentArray()[i].Visible);
        }

        AssertDirectionalLightsEquivalent(source, cooked);
        AssertPointLightsEquivalent(source, cooked);
        AssertSpotLightsEquivalent(source, cooked);
        AssertEnvironmentsEquivalent(source, cooked);
    }

    private static void AssertDirectionalLightsEquivalent(EntityManager source, EntityManager cooked)
    {
        var sourcePool = source.GetPool<DirectionalLightComponent>();
        var cookedPool = cooked.GetPool<DirectionalLightComponent>();
        Assert.Equal(sourcePool.Count, cookedPool.Count);
        for (int i = 0; i < sourcePool.Count; i++)
        {
            ref DirectionalLightComponent left = ref sourcePool.GetRawComponentArray()[i];
            ref DirectionalLightComponent right = ref cookedPool.GetRawComponentArray()[i];
            Assert.Equal(left.Direction, right.Direction);
            Assert.Equal(left.Color, right.Color);
            Assert.Equal(left.Intensity, right.Intensity);
            Assert.Equal(left.AmbientIntensity, right.AmbientIntensity);
            Assert.Equal(left.Enabled, right.Enabled);
        }
    }

    private static void AssertPointLightsEquivalent(EntityManager source, EntityManager cooked)
    {
        var sourcePool = source.GetPool<PointLightComponent>();
        var cookedPool = cooked.GetPool<PointLightComponent>();
        Assert.Equal(sourcePool.Count, cookedPool.Count);
        for (int i = 0; i < sourcePool.Count; i++)
        {
            ref PointLightComponent left = ref sourcePool.GetRawComponentArray()[i];
            ref PointLightComponent right = ref cookedPool.GetRawComponentArray()[i];
            Assert.Equal(left.Color, right.Color);
            Assert.Equal(left.Intensity, right.Intensity);
            Assert.Equal(left.Range, right.Range);
            Assert.Equal(left.Enabled, right.Enabled);
        }
    }

    private static void AssertSpotLightsEquivalent(EntityManager source, EntityManager cooked)
    {
        var sourcePool = source.GetPool<SpotLightComponent>();
        var cookedPool = cooked.GetPool<SpotLightComponent>();
        Assert.Equal(sourcePool.Count, cookedPool.Count);
        for (int i = 0; i < sourcePool.Count; i++)
        {
            ref SpotLightComponent left = ref sourcePool.GetRawComponentArray()[i];
            ref SpotLightComponent right = ref cookedPool.GetRawComponentArray()[i];
            Assert.Equal(left.Color, right.Color);
            Assert.Equal(left.Intensity, right.Intensity);
            Assert.Equal(left.Range, right.Range);
            Assert.Equal(left.InnerConeAngleDegrees, right.InnerConeAngleDegrees);
            Assert.Equal(left.OuterConeAngleDegrees, right.OuterConeAngleDegrees);
            Assert.Equal(left.Enabled, right.Enabled);
        }
    }

    private static void AssertEnvironmentsEquivalent(EntityManager source, EntityManager cooked)
    {
        var sourcePool = source.GetPool<SceneEnvironmentComponent>();
        var cookedPool = cooked.GetPool<SceneEnvironmentComponent>();
        Assert.Equal(sourcePool.Count, cookedPool.Count);
        for (int i = 0; i < sourcePool.Count; i++)
        {
            ref SceneEnvironmentComponent left = ref sourcePool.GetRawComponentArray()[i];
            ref SceneEnvironmentComponent right = ref cookedPool.GetRawComponentArray()[i];
            Assert.Equal(left.EnvironmentTextureGuid, right.EnvironmentTextureGuid);
            Assert.Equal(left.SkyColor, right.SkyColor);
            Assert.Equal(left.HorizonColor, right.HorizonColor);
            Assert.Equal(left.GroundColor, right.GroundColor);
            Assert.Equal(left.AmbientColor, right.AmbientColor);
            Assert.Equal(left.SkyIntensity, right.SkyIntensity);
            Assert.Equal(left.AmbientIntensity, right.AmbientIntensity);
            Assert.Equal(left.Exposure, right.Exposure);
            Assert.Equal(left.Enabled, right.Enabled);
        }
    }

    private static byte[] Mutate(byte[] source, Action<byte[]> mutation)
    {
        byte[] result = (byte[])source.Clone();
        mutation(result);
        return result;
    }

    private static byte[] MutateAndRehash(byte[] source, Action<byte[]> mutation)
    {
        byte[] result = Mutate(source, mutation);
        byte[] hash = SHA256.HashData(result.AsSpan(SceneAssetCooker.HeaderSize));
        hash.CopyTo(result.AsSpan(SceneAssetCooker.HashOffset, SceneAssetCooker.HashSize));
        return result;
    }

    private static int FindDescriptor(byte[] bytes, CookedSceneSectionType sectionType)
    {
        return FindDescriptor(bytes, (uint)sectionType);
    }

    private static int FindDescriptor(byte[] bytes, uint sectionType)
    {
        int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(36, 4));
        for (int i = 0; i < sectionCount; i++)
        {
            int offset = SceneAssetCooker.HeaderSize + (i * SceneAssetCooker.SectionDirectoryEntrySize);
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) == sectionType)
            {
                return offset;
            }
        }

        throw new InvalidOperationException($"Section type '{sectionType}' was not found.");
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenCookedSceneTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}

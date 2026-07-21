using System.Buffers.Binary;
using System.Security.Cryptography;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class SceneIdentitySchemaTests
{
    private static readonly Guid s_SceneGuid =
        Guid.Parse("51000000-0000-0000-0000-000000000001");
    private static readonly Guid s_ParentGuid =
        Guid.Parse("51000000-0000-0000-0000-000000000010");
    private static readonly Guid s_FirstChildGuid =
        Guid.Parse("51000000-0000-0000-0000-000000000020");
    private static readonly Guid s_SecondChildGuid =
        Guid.Parse("51000000-0000-0000-0000-000000000030");

    [Fact]
    public void SourceAndCookedScenes_PreserveIdentityAndHierarchyAcrossSourceReorder()
    {
        using var temp = new TempDirectory();
        string scenePath = Path.Combine(temp.Path, "Identity.arisenscene");
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, temp.Path);
        database.AddAsset(s_SceneGuid, "Scene", scenePath, "com.arisen.test");
        var sceneRef = new AssetRef<SceneSourceAsset>(
            s_SceneGuid,
            "Scene",
            "com.arisen.test");

        File.WriteAllText(scenePath, CreateHierarchySource(reordered: false));
        var firstWorld = new EntityManager();
        SceneLoadResult firstLoad = SceneAssetLoader.LoadScene(database, sceneRef, firstWorld);
        Assert.True(firstLoad.Success, firstLoad.Diagnostic);
        AssertHierarchy(firstWorld, Assert.IsType<SceneAuthoringEntityMap>(firstLoad.AuthoringEntities));
        CookedSceneArtifact firstCook = SceneAssetCooker.Cook(database, sceneRef);
        byte[] firstBytes = File.ReadAllBytes(firstCook.Path);

        File.WriteAllText(scenePath, CreateHierarchySource(reordered: true));
        var reorderedWorld = new EntityManager();
        SceneLoadResult reorderedLoad = SceneAssetLoader.LoadScene(
            database,
            sceneRef,
            reorderedWorld);
        Assert.True(reorderedLoad.Success, reorderedLoad.Diagnostic);
        var reorderedMap = Assert.IsType<SceneAuthoringEntityMap>(reorderedLoad.AuthoringEntities);
        AssertHierarchy(reorderedWorld, reorderedMap);
        AssertMapsResolveSameDenseIds(firstLoad.AuthoringEntities!, reorderedMap);

        SceneInspectionResult inspection = SceneAssetLoader.InspectScene(database, sceneRef);
        Assert.True(inspection.Success, inspection.Diagnostic);
        Assert.Equal(
            new[] { s_SecondChildGuid, s_ParentGuid, s_FirstChildGuid },
            inspection.Entities.Select(entity => entity.AuthoringGuid));
        Assert.Equal(s_ParentGuid, inspection.Entities[0].ParentGuid);
        Assert.Equal(s_ParentGuid, inspection.Entities[2].ParentGuid);

        CookedSceneArtifact reorderedCook = SceneAssetCooker.Cook(database, sceneRef);
        byte[] reorderedBytes = File.ReadAllBytes(reorderedCook.Path);
        Assert.Equal(firstBytes, reorderedBytes);

        File.Delete(scenePath);
        var cookedWorld = new EntityManager();
        SceneLoadResult cookedLoad = SceneAssetCooker.LoadCooked(
            database,
            sceneRef,
            cookedWorld);
        Assert.True(cookedLoad.Success, cookedLoad.Diagnostic);
        var cookedMap = Assert.IsType<SceneAuthoringEntityMap>(cookedLoad.AuthoringEntities);
        AssertHierarchy(cookedWorld, cookedMap);
        AssertMapsResolveSameDenseIds(reorderedMap, cookedMap);
    }

    [Fact]
    public void LegacyMigration_AssignsPersistentIdsOnceAndCurrentLoadingRejectsMissingOrDuplicateIds()
    {
        using var temp = new TempDirectory();
        string scenePath = Path.Combine(temp.Path, "Legacy.arisenscene");
        const string legacySource = """
            Version: 1
            Name: Legacy Scene
            Entities:
            - Name: First
            - Name: Second
            """;

        SceneAssetEditResult migration = SceneAssetLoader.MigrateLegacySceneSource(
            s_SceneGuid,
            scenePath,
            legacySource);
        Assert.True(migration.Success, migration.Diagnostic);
        Assert.Contains("Version: 2", migration.UpdatedSource);
        Assert.Contains("ComponentSchemas:", migration.UpdatedSource);
        File.WriteAllText(scenePath, migration.UpdatedSource);

        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, temp.Path);
        database.AddAsset(s_SceneGuid, "Scene", scenePath, "com.arisen.test");
        var sceneRef = new AssetRef<SceneSourceAsset>(s_SceneGuid, "Scene", "com.arisen.test");
        SceneInspectionResult firstInspection = SceneAssetLoader.InspectScene(database, sceneRef);
        Assert.True(firstInspection.Success, firstInspection.Diagnostic);
        Guid[] migratedIds = firstInspection.Entities.Select(entity => entity.AuthoringGuid).ToArray();
        Assert.Equal(2, migratedIds.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, migratedIds);

        SceneInspectionResult secondInspection = SceneAssetLoader.InspectScene(database, sceneRef);
        Assert.Equal(migratedIds, secondInspection.Entities.Select(entity => entity.AuthoringGuid));
        SceneAssetEditResult secondMigration = SceneAssetLoader.MigrateLegacySceneSource(
            s_SceneGuid,
            scenePath,
            migration.UpdatedSource);
        Assert.False(secondMigration.Success);
        Assert.Contains("accepts legacy schema version 1", secondMigration.Diagnostic);

        SceneInspectionResult missing = InspectSource(temp.Path, "MissingGuid", """
            Version: 2
            Name: Missing Guid
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            Entities:
            - Name: Missing
            """);
        Assert.False(missing.Success);
        Assert.Contains("has no persistent Guid", missing.Diagnostic);

        SceneInspectionResult duplicate = InspectSource(temp.Path, "DuplicateGuid", $$"""
            Version: 2
            Name: Duplicate Guid
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            Entities:
            - Guid: {{s_ParentGuid:D}}
              Name: First
            - Guid: {{s_ParentGuid:D}}
              Name: Duplicate
            """);
        Assert.False(duplicate.Success);
        Assert.Contains("duplicate entity Guid", duplicate.Diagnostic);
    }

    [Fact]
    public void ComponentSchemas_MigrateKnownDataSkipOptionalDataAndFailClosedWhenRequired()
    {
        using var temp = new TempDirectory();
        Guid entityGuid = Guid.Parse("52000000-0000-0000-0000-000000000001");
        SceneInspectionResult migratedCamera = InspectSource(temp.Path, "MigratedCamera", $$"""
            Version: 2
            Name: Migrated Camera
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            - TypeId: 2
              Name: Camera
              Version: 1
              Required: true
            Entities:
            - Guid: {{entityGuid:D}}
              Name: Camera
              Camera:
                FieldOfView: 47
                NearPlane: 0.25
                FarPlane: 500
            """);
        Assert.True(migratedCamera.Success, migratedCamera.Diagnostic);
        Assert.Equal(47.0f, Assert.Single(migratedCamera.Entities).Camera!.VerticalFov);

        SceneInspectionResult optionalFuture = InspectSource(temp.Path, "OptionalFuture", $$"""
            Version: 2
            Name: Optional Future
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            - TypeId: 2
              Name: Camera
              Version: 99
              Required: false
            - TypeId: 9001
              Name: FutureFog
              Version: 1
              Required: false
            Entities:
            - Guid: {{entityGuid:D}}
              Name: Optional Entity
              Camera:
                VerticalFov: 15
              FutureFog:
                Density: 0.5
            """);
        Assert.True(optionalFuture.Success, optionalFuture.Diagnostic);
        Assert.Null(Assert.Single(optionalFuture.Entities).Camera);

        SceneInspectionResult missingMigration = InspectSource(temp.Path, "MissingMigration", $$"""
            Version: 2
            Name: Missing Migration
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 0
              Required: true
            Entities:
            - Guid: {{entityGuid:D}}
              Transform:
                Position: { X: 1, Y: 2, Z: 3 }
            """);
        Assert.False(missingMigration.Success);
        Assert.Contains("no migration from version '0'", missingMigration.Diagnostic);

        SceneInspectionResult newerRequired = InspectSource(temp.Path, "NewerRequired", $$"""
            Version: 2
            Name: Newer Required
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            - TypeId: 2
              Name: Camera
              Version: 99
              Required: true
            Entities:
            - Guid: {{entityGuid:D}}
              Camera:
                VerticalFov: 60
            """);
        Assert.False(newerRequired.Success);
        Assert.Contains("required component 'Camera'", newerRequired.Diagnostic);

        Guid externalScene = Guid.Parse("52000000-0000-0000-0000-000000000099");
        SceneInspectionResult crossScene = InspectSource(temp.Path, "CrossScene", $$"""
            Version: 2
            Name: Cross Scene
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            Entities:
            - Guid: {{entityGuid:D}}
              Parent:
                SceneGuid: {{externalScene:D}}
                EntityGuid: {{s_ParentGuid:D}}
            """);
        Assert.False(crossScene.Success);
        Assert.Contains("future world-reference policy", crossScene.Diagnostic);
    }

    [Fact]
    public void CookedComponentSchemas_RejectNewerRequiredAndSkipNewerOptionalComponents()
    {
        using var temp = new TempDirectory();
        Guid sceneGuid = Guid.Parse("53000000-0000-0000-0000-000000000001");
        Guid entityGuid = Guid.Parse("53000000-0000-0000-0000-000000000002");
        string scenePath = Path.Combine(temp.Path, "CookedSchemas.arisenscene");
        File.WriteAllText(scenePath, $$"""
            Version: 2
            Name: Cooked Schemas
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            - TypeId: 2
              Name: Camera
              Version: 2
              Required: true
            Entities:
            - Guid: {{entityGuid:D}}
              Camera:
                VerticalFov: 60
            """);
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, temp.Path);
        database.AddAsset(sceneGuid, "Scene", scenePath, "com.arisen.test");
        var sceneRef = new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test");
        CookedSceneArtifact artifact = SceneAssetCooker.Cook(database, sceneRef);
        byte[] validBytes = File.ReadAllBytes(artifact.Path);

        byte[] requiredBytes = MutateCameraSchema(validBytes, required: true);
        File.WriteAllBytes(artifact.Path, requiredBytes);
        var rejectedWorld = new EntityManager();
        SceneLoadResult rejected = SceneAssetCooker.LoadCooked(database, sceneRef, rejectedWorld);
        Assert.False(rejected.Success);
        Assert.Contains("newer than supported", rejected.Diagnostic);
        Assert.Empty(rejectedWorld.GetAllEntities());

        byte[] optionalBytes = MutateCameraSchema(validBytes, required: false);
        File.WriteAllBytes(artifact.Path, optionalBytes);
        var optionalWorld = new EntityManager();
        SceneLoadResult optional = SceneAssetCooker.LoadCooked(database, sceneRef, optionalWorld);
        Assert.True(optional.Success, optional.Diagnostic);
        Assert.Equal(0, optional.CameraCount);
        Assert.Single(optionalWorld.GetAllEntities());
        Assert.Equal(0, optionalWorld.GetPool<CameraComponent>().Count);
    }

    private static SceneInspectionResult InspectSource(
        string root,
        string name,
        string source)
    {
        Guid sceneGuid = GeneratedAssetIdentity.CreateChildGuid(
            s_SceneGuid,
            "com.arisen.test",
            "identity-schema-test",
            name);
        string sourcePath = Path.Combine(root, $"{name}.arisenscene");
        File.WriteAllText(sourcePath, source);
        var database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, root);
        database.AddAsset(sceneGuid, "Scene", sourcePath, "com.arisen.test");
        return SceneAssetLoader.InspectScene(
            database,
            new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test"));
    }

    private static void AssertMapsResolveSameDenseIds(
        SceneAuthoringEntityMap left,
        SceneAuthoringEntityMap right)
    {
        foreach (Guid guid in new[] { s_ParentGuid, s_FirstChildGuid, s_SecondChildGuid })
        {
            Assert.True(left.TryGetEntity(guid, out var leftEntity));
            Assert.True(right.TryGetEntity(guid, out var rightEntity));
            Assert.Equal(leftEntity, rightEntity);
        }
    }

    private static void AssertHierarchy(
        EntityManager world,
        SceneAuthoringEntityMap map)
    {
        Assert.Equal(3, map.Count);
        Assert.True(map.TryGetEntity(s_ParentGuid, out var parent));
        Assert.True(map.TryGetEntity(s_FirstChildGuid, out var firstChild));
        Assert.True(map.TryGetEntity(s_SecondChildGuid, out var secondChild));
        Assert.Equal(parent, world.GetComponent<ParentComponent>(firstChild).Parent);
        Assert.Equal(parent, world.GetComponent<ParentComponent>(secondChild).Parent);

        ref ChildComponent children = ref world.GetComponent<ChildComponent>(parent);
        Assert.Equal(2, children.ChildCount);
        Guid expectedFirstGuid = s_FirstChildGuid.CompareTo(s_SecondChildGuid) < 0
            ? s_FirstChildGuid
            : s_SecondChildGuid;
        Guid expectedSecondGuid = expectedFirstGuid == s_FirstChildGuid
            ? s_SecondChildGuid
            : s_FirstChildGuid;
        Assert.True(map.TryGetEntity(expectedFirstGuid, out var expectedFirst));
        Assert.True(map.TryGetEntity(expectedSecondGuid, out var expectedSecond));
        Assert.Equal(expectedFirst, children.FirstChild);
        ref SiblingComponent firstSibling = ref world.GetComponent<SiblingComponent>(expectedFirst);
        ref SiblingComponent secondSibling = ref world.GetComponent<SiblingComponent>(expectedSecond);
        Assert.Equal(Entity.Null, firstSibling.PrevSibling);
        Assert.Equal(expectedSecond, firstSibling.NextSibling);
        Assert.Equal(expectedFirst, secondSibling.PrevSibling);
        Assert.Equal(Entity.Null, secondSibling.NextSibling);
    }

    private static string CreateHierarchySource(bool reordered)
    {
        string parent = $$"""
            - Guid: {{s_ParentGuid:D}}
              Name: Parent
              Transform:
                Position: { X: 1, Y: 0, Z: 0 }
            """;
        string firstChild = $$"""
            - Guid: {{s_FirstChildGuid:D}}
              Name: First Child
              Parent:
                EntityGuid: {{s_ParentGuid:D}}
              Transform:
                Position: { X: 0, Y: 1, Z: 0 }
            """;
        string secondChild = $$"""
            - Guid: {{s_SecondChildGuid:D}}
              Name: Second Child
              Parent:
                SceneGuid: {{s_SceneGuid:D}}
                EntityGuid: {{s_ParentGuid:D}}
              Transform:
                Position: { X: 0, Y: 0, Z: 1 }
            """;
        string entities = reordered
            ? string.Concat(secondChild, Environment.NewLine, parent, Environment.NewLine, firstChild)
            : string.Concat(parent, Environment.NewLine, firstChild, Environment.NewLine, secondChild);
        return $$"""
            Version: 2
            Name: Identity Scene
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            Entities:
            {{entities}}
            """;
    }

    private static byte[] MutateCameraSchema(byte[] source, bool required)
    {
        byte[] result = (byte[])source.Clone();
        int descriptor = FindDescriptor(result, CookedSceneSectionType.ComponentSchemas);
        int sectionOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(
            result.AsSpan(descriptor + 8, 8)));
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(descriptor + 24, 4));
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            int recordOffset = sectionOffset + (i * 16);
            if (BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(recordOffset, 4)) !=
                SceneComponentSchemas.CameraTypeId)
            {
                continue;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(recordOffset + 4, 4), 99);
            BinaryPrimitives.WriteUInt32LittleEndian(
                result.AsSpan(recordOffset + 8, 4),
                required ? 1u : 0u);
            found = true;
            break;
        }

        Assert.True(found);
        byte[] hash = SHA256.HashData(result.AsSpan(SceneAssetCooker.HeaderSize));
        hash.CopyTo(result.AsSpan(SceneAssetCooker.HashOffset, SceneAssetCooker.HashSize));
        return result;
    }

    private static int FindDescriptor(byte[] bytes, CookedSceneSectionType sectionType)
    {
        int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(36, 4));
        for (int i = 0; i < sectionCount; i++)
        {
            int offset = SceneAssetCooker.HeaderSize +
                         (i * SceneAssetCooker.SectionDirectoryEntrySize);
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) ==
                (uint)sectionType)
            {
                return offset;
            }
        }

        throw new InvalidOperationException($"Section '{sectionType}' was not found.");
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenSceneIdentitySchemaTests",
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

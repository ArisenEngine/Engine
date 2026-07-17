using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class SceneAssetLoaderTests
{
    [Fact]
    public void RuntimeSceneService_ActivatesOnlyFullyLoadedSceneWorlds()
    {
        using var temp = new TempDirectory();
        var db = new TestAssetDatabase(temp.Path);
        var validSceneGuid = Guid.Parse("11111111-aaaa-bbbb-cccc-222222222222");
        var brokenSceneGuid = Guid.Parse("33333333-aaaa-bbbb-cccc-444444444444");
        var missingMeshGuid = Guid.Parse("55555555-aaaa-bbbb-cccc-666666666666");
        string validScenePath = Path.Combine(temp.Path, "RuntimeScene.arisenscene");
        string brokenScenePath = Path.Combine(temp.Path, "BrokenRuntimeScene.arisenscene");

        File.WriteAllText(validScenePath,
            """
            Name: Runtime Scene
            Entities:
            - Name: Main Camera
              Camera:
                VerticalFov: 60
                NearPlane: 0.1
                FarPlane: 100
                IsPerspective: true
            """);
        File.WriteAllText(brokenScenePath, $"""
            Name: Broken Runtime Scene
            Entities:
            - Name: Missing Mesh
              MeshRenderer:
                Mesh:
                  Guid: {missingMeshGuid:D}
            """);
        db.AddAsset(validSceneGuid, "Scene", validScenePath);
        db.AddAsset(brokenSceneGuid, "Scene", brokenScenePath);

        var originalWorld = new EntityManager();
        EntityManager activeWorld = originalWorld;
        RuntimeSceneState? publishedState = null;
        var service = new RuntimeSceneService(db, world => activeWorld = world);
        service.ActiveSceneChanged += state => publishedState = state;

        var failed = service.LoadScene(
            new AssetRef<SceneSourceAsset>(brokenSceneGuid, "Scene", "com.arisen.test"));

        Assert.False(failed.Success);
        Assert.Same(originalWorld, activeWorld);
        Assert.Null(service.ActiveScene);
        Assert.Null(publishedState);

        var loaded = service.LoadScene(
            new AssetRef<SceneSourceAsset>(validSceneGuid, "Scene", "com.arisen.test"));

        Assert.True(loaded.Success, loaded.Diagnostic);
        Assert.Equal("Runtime Scene", loaded.SceneName);
        Assert.Equal(validScenePath, loaded.SourcePath);
        Assert.NotSame(originalWorld, activeWorld);
        Assert.Same(activeWorld, service.ActiveScene!.EntityManager);
        Assert.Equal(validSceneGuid, service.ActiveScene.Scene.Guid);
        Assert.Equal("Runtime Scene", service.ActiveScene.Name);
        Assert.Same(service.ActiveScene, publishedState);
        Assert.Equal(1, activeWorld.GetPool<CameraComponent>().Count);
    }

    [Fact]
    public void RuntimeSceneService_QueuedReloadActivatesOnlyAtFrameBoundary()
    {
        using var temp = new TempDirectory();
        var db = new TestAssetDatabase(temp.Path);
        var sceneGuid = Guid.Parse("77777777-aaaa-bbbb-cccc-888888888888");
        string scenePath = Path.Combine(temp.Path, "LiveEditScene.arisenscene");
        File.WriteAllText(scenePath,
            """
            Name: Live Edit Scene
            Entities:
            - Name: Editable Mesh
              Transform:
                Position:
                  X: 1
                  Y: 2
                  Z: 3
            """);
        db.AddAsset(sceneGuid, "Scene", scenePath);

        EntityManager? activeWorld = null;
        var service = new RuntimeSceneService(db, world => activeWorld = world);
        var sceneRef = new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test");
        var initialLoad = service.LoadScene(sceneRef);
        Assert.True(initialLoad.Success, initialLoad.Diagnostic);
        var initialWorld = Assert.IsType<EntityManager>(activeWorld);
        Assert.Equal(
            new System.Numerics.Vector3(1, 2, 3),
            initialWorld.GetPool<TransformComponent>().GetRawComponentArray()[0].Position);

        var edit = SceneAssetLoader.UpdateEntityTransform(
            scenePath,
            0,
            new SceneTransformInspection(
                new System.Numerics.Vector3(4, 5, 6),
                System.Numerics.Quaternion.Identity,
                System.Numerics.Vector3.One));
        Assert.True(edit.Success, edit.Diagnostic);

        service.RequestSceneLoad(sceneRef);

        Assert.Same(initialWorld, activeWorld);
        Assert.Equal(
            new System.Numerics.Vector3(1, 2, 3),
            initialWorld.GetPool<TransformComponent>().GetRawComponentArray()[0].Position);

        var processed = service.ProcessPendingSceneLoadAtFrameBoundary();

        Assert.True(processed.HasValue);
        Assert.True(processed.Value.Success, processed.Value.Diagnostic);
        Assert.NotSame(initialWorld, activeWorld);
        Assert.Same(activeWorld, service.ActiveScene!.EntityManager);
        Assert.Equal(
            new System.Numerics.Vector3(4, 5, 6),
            activeWorld!.GetPool<TransformComponent>().GetRawComponentArray()[0].Position);
        Assert.Null(service.ProcessPendingSceneLoadAtFrameBoundary());

        var lastValidWorld = activeWorld;
        File.WriteAllText(scenePath, "Name: Broken Live Edit Scene\nEntities: []\n");
        service.RequestSceneLoad(sceneRef);

        var failedReload = service.ProcessPendingSceneLoadAtFrameBoundary();

        Assert.True(failedReload.HasValue);
        Assert.False(failedReload.Value.Success);
        Assert.Same(lastValidWorld, activeWorld);
        Assert.Same(lastValidWorld, service.ActiveScene.EntityManager);
    }

    [Fact]
    public void RuntimeSceneService_CoalescesQueuedLoadsToLatestRequest()
    {
        using var temp = new TempDirectory();
        var db = new TestAssetDatabase(temp.Path);
        var firstSceneGuid = Guid.Parse("99999999-aaaa-bbbb-cccc-111111111111");
        var latestSceneGuid = Guid.Parse("99999999-aaaa-bbbb-cccc-222222222222");
        string firstScenePath = Path.Combine(temp.Path, "FirstQueuedScene.arisenscene");
        string latestScenePath = Path.Combine(temp.Path, "LatestQueuedScene.arisenscene");
        File.WriteAllText(firstScenePath,
            """
            Name: First Queued Scene
            Entities:
            - Name: First Camera
              Camera:
                VerticalFov: 45
            """);
        File.WriteAllText(latestScenePath,
            """
            Name: Latest Queued Scene
            Entities:
            - Name: Latest Camera
              Camera:
                VerticalFov: 60
            """);
        db.AddAsset(firstSceneGuid, "Scene", firstScenePath);
        db.AddAsset(latestSceneGuid, "Scene", latestScenePath);

        EntityManager? activeWorld = null;
        var service = new RuntimeSceneService(db, world => activeWorld = world);
        service.RequestSceneLoad(
            new AssetRef<SceneSourceAsset>(firstSceneGuid, "Scene", "com.arisen.test"));
        service.RequestSceneLoad(
            new AssetRef<SceneSourceAsset>(latestSceneGuid, "Scene", "com.arisen.test"));

        Assert.Null(activeWorld);

        var processed = service.ProcessPendingSceneLoadAtFrameBoundary();

        Assert.True(processed.HasValue);
        Assert.True(processed.Value.Success, processed.Value.Diagnostic);
        Assert.Equal("Latest Queued Scene", processed.Value.SceneName);
        Assert.Equal(latestSceneGuid, service.ActiveScene!.Scene.Guid);
        Assert.Same(activeWorld, service.ActiveScene.EntityManager);
        Assert.Null(service.ProcessPendingSceneLoadAtFrameBoundary());
    }

    [Fact]
    public void SceneAssetLoader_SpawnsCameraAndMeshRenderers()
    {
        using var temp = new TempDirectory();
        var db = new TestAssetDatabase(temp.Path);
        var sceneGuid = Guid.Parse("0bb7d5fb-1924-45ee-9b45-85891d0e6d9f");
        var meshGuid = Guid.Parse("3b392205-8cad-4d61-bf47-040b3549f0cf");
        var environmentTextureGuid = Guid.Parse("4c4c4c4c-5d5d-6e6e-7f7f-808080808080");
        string scenePath = Path.Combine(temp.Path, "SmokeScene.arisenscene");
        string meshPath = Path.Combine(temp.Path, "MultiSubmeshQuad.armesh");

        File.WriteAllText(scenePath, $"""
            Name: Test Scene
            Entities:
            - Name: Main Camera
              Transform:
                Position:
                  X: 0
                  Y: 0
                  Z: -2.5
              Camera:
                VerticalFov: 70
                NearPlane: 0.2
                FarPlane: 250
                IsPerspective: true
            - Name: Key Light
              DirectionalLight:
                Direction:
                  X: 0.35
                  Y: 0.65
                  Z: -0.68
                Color:
                  X: 1
                  Y: 0.96
                  Z: 0.88
                Intensity: 1.25
                AmbientIntensity: 0.2
            - Name: Fill Point
              Transform:
                Position:
                  X: -0.75
                  Y: 1.2
                  Z: -1.25
              PointLight:
                Color:
                  X: 0.65
                  Y: 0.82
                  Z: 1
                Intensity: 1.4
                Range: 3.5
            - Name: Focus Spot
              Transform:
                Position:
                  X: 0.25
                  Y: 1.4
                  Z: -1.5
              SpotLight:
                Color:
                  X: 1
                  Y: 0.82
                  Z: 0.55
                Intensity: 2.3
                Range: 4.25
                InnerConeAngleDegrees: 12
                OuterConeAngleDegrees: 26
            - Name: Sky Environment
              Environment:
                EnvironmentTexture:
                  Guid: {environmentTextureGuid:D}
                SkyColor:
                  X: 0.1
                  Y: 0.2
                  Z: 0.4
                HorizonColor:
                  X: 0.6
                  Y: 0.7
                  Z: 0.8
                GroundColor:
                  X: 0.03
                  Y: 0.04
                  Z: 0.06
                AmbientColor:
                  X: 0.5
                  Y: 0.6
                  Z: 0.75
                SkyIntensity: 0.9
                AmbientIntensity: 0.3
                Exposure: 1.25
            - Name: Mesh A
              Transform:
                Position:
                  X: 1
                  Y: 2
                  Z: 3
              MeshRenderer:
                Mesh:
                  Guid: {meshGuid:D}
                Visible: true
            - Name: Mesh B
              MeshRenderer:
                Mesh:
                  Guid: {meshGuid:D}
                FirstSubmeshIndex: 1
                SubmeshCount: 1
                Visible: true
            """);
        File.WriteAllText(meshPath, string.Empty);
        db.AddAsset(sceneGuid, "Scene", scenePath);
        db.AddAsset(meshGuid, "Mesh", meshPath);

        var entityManager = new EntityManager();
        var result = SceneAssetLoader.LoadScene(
            db,
            new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test"),
            entityManager);

        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal(7, result.EntityCount);
        Assert.Equal(1, result.CameraCount);
        Assert.Equal(2, result.MeshRendererCount);
        Assert.Equal(1, result.DirectionalLightCount);
        Assert.Equal(1, result.PointLightCount);
        Assert.Equal(1, result.SpotLightCount);
        Assert.Equal(1, result.EnvironmentCount);
        Assert.Equal(7, entityManager.GetPool<TransformComponent>().Count);
        Assert.Equal(1, entityManager.GetPool<CameraComponent>().Count);
        Assert.Equal(1, entityManager.GetPool<DirectionalLightComponent>().Count);
        Assert.Equal(1, entityManager.GetPool<PointLightComponent>().Count);
        Assert.Equal(1, entityManager.GetPool<SpotLightComponent>().Count);
        Assert.Equal(1, entityManager.GetPool<SceneEnvironmentComponent>().Count);
        Assert.Equal(2, entityManager.GetPool<MeshRendererComponent>().Count);

        var light = entityManager.GetPool<DirectionalLightComponent>().GetRawComponentArray()[0];
        Assert.Equal(1.25f, light.Intensity);
        Assert.Equal(0.2f, light.AmbientIntensity);
        Assert.Equal(1, light.Enabled);

        var pointLight = entityManager.GetPool<PointLightComponent>().GetRawComponentArray()[0];
        Assert.Equal(new System.Numerics.Vector3(0.65f, 0.82f, 1.0f), pointLight.Color);
        Assert.Equal(1.4f, pointLight.Intensity);
        Assert.Equal(3.5f, pointLight.Range);
        Assert.Equal(1, pointLight.Enabled);

        var spotLight = entityManager.GetPool<SpotLightComponent>().GetRawComponentArray()[0];
        Assert.Equal(new System.Numerics.Vector3(1.0f, 0.82f, 0.55f), spotLight.Color);
        Assert.Equal(2.3f, spotLight.Intensity);
        Assert.Equal(4.25f, spotLight.Range);
        Assert.Equal(12.0f, spotLight.InnerConeAngleDegrees);
        Assert.Equal(26.0f, spotLight.OuterConeAngleDegrees);
        Assert.Equal(1, spotLight.Enabled);

        var environment = entityManager.GetPool<SceneEnvironmentComponent>().GetRawComponentArray()[0];
        Assert.Equal(new System.Numerics.Vector3(0.1f, 0.2f, 0.4f), environment.SkyColor);
        Assert.Equal(environmentTextureGuid, environment.EnvironmentTextureGuid);
        Assert.Equal(new System.Numerics.Vector3(0.5f, 0.6f, 0.75f), environment.AmbientColor);
        Assert.Equal(0.9f, environment.SkyIntensity);
        Assert.Equal(0.3f, environment.AmbientIntensity);
        Assert.Equal(1.25f, environment.Exposure);
        Assert.Equal(1, environment.Enabled);

        var meshComponents = entityManager.GetPool<MeshRendererComponent>().GetRawComponentArray();
        Assert.Equal(meshGuid, meshComponents[0].MeshGuid);
        Assert.Equal(meshGuid, meshComponents[1].MeshGuid);
        Assert.Equal(1, meshComponents[1].FirstSubmeshIndex);
        Assert.Equal(1, meshComponents[1].SubmeshCount);
    }

    [Fact]
    public void SceneAssetLoader_ReportsMissingMeshReferenceWithEntityName()
    {
        using var temp = new TempDirectory();
        var db = new TestAssetDatabase(temp.Path);
        var sceneGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var missingMeshGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        string scenePath = Path.Combine(temp.Path, "Broken.arisenscene");
        File.WriteAllText(scenePath, $"""
            Name: Broken
            Entities:
            - Name: Broken Mesh
              MeshRenderer:
                Mesh:
                  Guid: {missingMeshGuid:D}
            """);
        db.AddAsset(sceneGuid, "Scene", scenePath);

        var result = SceneAssetLoader.LoadScene(
            db,
            new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test"),
            new EntityManager());

        Assert.False(result.Success);
        Assert.Contains("Broken Mesh", result.Diagnostic);
        Assert.Contains(missingMeshGuid.ToString("D"), result.Diagnostic);
    }

    [Fact]
    public void SceneAssetInspector_ReportsEntitiesComponentsAndReferences()
    {
        using var temp = new TempDirectory();
        var db = new TestAssetDatabase(temp.Path);
        var sceneGuid = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");
        var meshGuid = Guid.Parse("cccccccc-1111-2222-3333-444444444444");
        var materialGuid = Guid.Parse("dddddddd-1111-2222-3333-444444444444");
        var environmentTextureGuid = Guid.Parse("eeeeeeee-1111-2222-3333-444444444444");
        string scenePath = Path.Combine(temp.Path, "Inspectable.arisenscene");
        string meshPath = Path.Combine(temp.Path, "Inspectable.obj");
        string materialPath = Path.Combine(temp.Path, "Inspectable.arismaterial");
        string environmentPath = Path.Combine(temp.Path, "Inspectable.arienvironment");

        File.WriteAllText(scenePath, $"""
            Name: Inspectable Scene
            Entities:
            - Name: Main Camera
              Transform:
                Position:
                  X: 0
                  Y: 1.5
                  Z: -6
              Camera:
                VerticalFov: 55
                NearPlane: 0.1
                FarPlane: 200
                IsPerspective: true
            - Name: Rendered Mesh
              Transform:
                Position:
                  X: 1
                  Y: 2
                  Z: 3
                Scale:
                  X: 2
                  Y: 2
                  Z: 2
              MeshRenderer:
                Mesh:
                  Guid: {meshGuid:D}
                Material:
                  Guid: {materialGuid:D}
                FirstSubmeshIndex: 1
                SubmeshCount: 2
                BoundsCenter:
                  X: 0
                  Y: 0.5
                  Z: 0
                BoundsExtents:
                  X: 1
                  Y: 2
                  Z: 3
                Visible: true
            - Name: Key Light
              DirectionalLight:
                Direction:
                  X: 0.4
                  Y: 0.7
                  Z: -0.6
                Color:
                  X: 1
                  Y: 0.9
                  Z: 0.8
                Intensity: 1.7
                AmbientIntensity: 0.25
            - Name: Accent Light
              Transform:
                Position:
                  X: -1
                  Y: 1.25
                  Z: -2
              PointLight:
                Color:
                  X: 0.5
                  Y: 0.7
                  Z: 1
                Intensity: 1.1
                Range: 2.75
            - Name: Focus Spot
              Transform:
                Position:
                  X: 0.5
                  Y: 1.75
                  Z: -2.25
              SpotLight:
                Color:
                  X: 1
                  Y: 0.8
                  Z: 0.55
                Intensity: 2.2
                Range: 4.5
                InnerConeAngleDegrees: 14
                OuterConeAngleDegrees: 32
            - Name: World
              Environment:
                EnvironmentTexture:
                  Guid: {environmentTextureGuid:D}
                SkyColor:
                  X: 0.1
                  Y: 0.2
                  Z: 0.3
                AmbientColor:
                  X: 0.4
                  Y: 0.5
                  Z: 0.6
            """);
        File.WriteAllText(meshPath, string.Empty);
        File.WriteAllText(materialPath, string.Empty);
        File.WriteAllText(environmentPath, string.Empty);
        db.AddAsset(sceneGuid, "Scene", scenePath);
        db.AddAsset(meshGuid, "Mesh", meshPath);
        db.AddAsset(materialGuid, "Material", materialPath);
        db.AddAsset(environmentTextureGuid, "EnvironmentTexture", environmentPath);

        var inspection = SceneAssetLoader.InspectScene(
            db,
            new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test"));

        Assert.True(inspection.Success, inspection.Diagnostic);
        Assert.Equal("Inspectable Scene", inspection.SceneName);
        Assert.Equal(6, inspection.EntityCount);
        Assert.Equal(1, inspection.CameraCount);
        Assert.Equal(1, inspection.MeshRendererCount);
        Assert.Equal(1, inspection.DirectionalLightCount);
        Assert.Equal(1, inspection.PointLightCount);
        Assert.Equal(1, inspection.SpotLightCount);
        Assert.Equal(1, inspection.EnvironmentCount);
        Assert.Empty(inspection.Diagnostics);

        var renderedMesh = Assert.Single(inspection.Entities, entity => entity.Name == "Rendered Mesh");
        Assert.NotNull(renderedMesh.MeshRenderer);
        Assert.Equal(new System.Numerics.Vector3(1, 2, 3), renderedMesh.Transform.Position);
        Assert.Equal(new System.Numerics.Vector3(2, 2, 2), renderedMesh.Transform.Scale);
        Assert.Equal(meshGuid, renderedMesh.MeshRenderer!.Mesh.Guid);
        Assert.Equal(materialGuid, renderedMesh.MeshRenderer.Material.Guid);
        Assert.True(renderedMesh.MeshRenderer.Mesh.IsResolved);
        Assert.True(renderedMesh.MeshRenderer.Material.IsResolved);
        Assert.Equal(1, renderedMesh.MeshRenderer.FirstSubmeshIndex);
        Assert.Equal(2, renderedMesh.MeshRenderer.SubmeshCount);

        var camera = Assert.Single(inspection.Entities, entity => entity.Camera != null).Camera!;
        Assert.Equal(55.0f, camera.VerticalFov);

        var environment = Assert.Single(inspection.Entities, entity => entity.Environment != null).Environment!;
        Assert.Equal(environmentTextureGuid, environment.EnvironmentTexture.Guid);
        Assert.True(environment.EnvironmentTexture.IsResolved, environment.EnvironmentTexture.Diagnostic);
        Assert.Equal(SceneEnvironmentComponent.DefaultExposure, environment.Exposure);

        var accent = Assert.Single(inspection.Entities, entity => entity.PointLight != null);
        Assert.Equal(new System.Numerics.Vector3(-1, 1.25f, -2), accent.Transform.Position);
        Assert.Equal(new System.Numerics.Vector3(0.5f, 0.7f, 1.0f), accent.PointLight!.Color);
        Assert.Equal(1.1f, accent.PointLight.Intensity);
        Assert.Equal(2.75f, accent.PointLight.Range);

        var focus = Assert.Single(inspection.Entities, entity => entity.SpotLight != null);
        Assert.Equal(new System.Numerics.Vector3(0.5f, 1.75f, -2.25f), focus.Transform.Position);
        Assert.Equal(new System.Numerics.Vector3(1.0f, 0.8f, 0.55f), focus.SpotLight!.Color);
        Assert.Equal(2.2f, focus.SpotLight.Intensity);
        Assert.Equal(4.5f, focus.SpotLight.Range);
        Assert.Equal(14.0f, focus.SpotLight.InnerConeAngleDegrees);
        Assert.Equal(32.0f, focus.SpotLight.OuterConeAngleDegrees);
    }

    [Fact]
    public void SceneAssetInspector_SurfacesMissingReferenceDiagnosticsWithoutSpawning()
    {
        using var temp = new TempDirectory();
        var db = new TestAssetDatabase(temp.Path);
        var sceneGuid = Guid.Parse("eeeeeeee-1111-2222-3333-444444444444");
        var missingMeshGuid = Guid.Parse("ffffffff-1111-2222-3333-444444444444");
        string scenePath = Path.Combine(temp.Path, "MissingReference.arisenscene");

        File.WriteAllText(scenePath, $"""
            Name: Missing Reference
            Entities:
            - Name: Broken Mesh
              MeshRenderer:
                Mesh:
                  Guid: {missingMeshGuid:D}
            """);
        db.AddAsset(sceneGuid, "Scene", scenePath);

        var inspection = SceneAssetLoader.InspectScene(
            db,
            new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test"));

        Assert.False(inspection.Success);
        Assert.Equal(1, inspection.EntityCount);
        Assert.Equal(1, inspection.MeshRendererCount);
        var diagnostic = Assert.Single(inspection.Diagnostics);
        Assert.Contains("Broken Mesh", diagnostic);
        Assert.Contains(missingMeshGuid.ToString("D"), diagnostic);
        Assert.Contains("missing mesh", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SceneAssetEditor_UpdatesEntityTransformWithoutDroppingComponents()
    {
        using var temp = new TempDirectory();
        var db = new TestAssetDatabase(temp.Path);
        var sceneGuid = Guid.Parse("12345678-1111-2222-3333-444444444444");
        var meshGuid = Guid.Parse("12345678-aaaa-bbbb-cccc-444444444444");
        string scenePath = Path.Combine(temp.Path, "Editable.arisenscene");
        string meshPath = Path.Combine(temp.Path, "Editable.obj");

        File.WriteAllText(scenePath, $"""
            Name: Editable Scene
            Entities:
            - Name: Camera
              Camera:
                VerticalFov: 45
            - Name: Mesh
              Transform:
                Position:
                  X: 1
                  Y: 2
                  Z: 3
              MeshRenderer:
                Mesh:
                  Guid: {meshGuid:D}
                Visible: true
            """);
        File.WriteAllText(meshPath, string.Empty);
        db.AddAsset(sceneGuid, "Scene", scenePath);
        db.AddAsset(meshGuid, "Mesh", meshPath);

        var newTransform = new SceneTransformInspection(
            new System.Numerics.Vector3(4.5f, 5.5f, 6.5f),
            new System.Numerics.Quaternion(0.1f, 0.2f, 0.3f, 0.9f),
            new System.Numerics.Vector3(2.0f, 3.0f, 4.0f));

        var edit = SceneAssetLoader.UpdateEntityTransform(scenePath, 1, newTransform);
        Assert.True(edit.Success, edit.Diagnostic);

        var inspection = SceneAssetLoader.InspectScene(
            db,
            new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test"));

        Assert.True(inspection.Success, inspection.Diagnostic);
        Assert.Equal(2, inspection.EntityCount);
        Assert.Equal(1, inspection.CameraCount);
        Assert.Equal(1, inspection.MeshRendererCount);

        var mesh = Assert.Single(inspection.Entities, entity => entity.Name == "Mesh");
        Assert.Equal(newTransform.Position, mesh.Transform.Position);
        Assert.Equal(newTransform.Rotation, mesh.Transform.Rotation);
        Assert.Equal(newTransform.Scale, mesh.Transform.Scale);
        Assert.NotNull(mesh.MeshRenderer);
        Assert.Equal(meshGuid, mesh.MeshRenderer!.Mesh.Guid);
    }

    [Fact]
    public void SceneAssetEditor_RejectsOutOfRangeEntityIndex()
    {
        using var temp = new TempDirectory();
        string scenePath = Path.Combine(temp.Path, "Editable.arisenscene");
        File.WriteAllText(scenePath, """
            Name: Editable Scene
            Entities:
            - Name: Only Entity
            """);

        var edit = SceneAssetLoader.UpdateEntityTransform(
            scenePath,
            3,
            new SceneTransformInspection(
                System.Numerics.Vector3.Zero,
                System.Numerics.Quaternion.Identity,
                System.Numerics.Vector3.One));

        Assert.False(edit.Success);
        Assert.Contains("outside the entity count", edit.Diagnostic);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ArisenSceneTests", Guid.NewGuid().ToString("N"));
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

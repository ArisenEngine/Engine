using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.Rendering;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeSceneInstanceTests
{
    [Fact]
    public void PersistentAndAdditiveInstances_ActivateAndUnloadInOneWorld()
    {
        using var context = new SceneInstanceContext();
        SceneLoadResult persistentLoad = context.Service.LoadScene(context.Persistent.Scene);
        Assert.True(persistentLoad.Success, persistentLoad.Diagnostic);
        EntityManager stableWorld = context.World;

        RuntimeSceneInstanceId firstId = context.Service.RequestAdditiveSceneLoad(context.FirstCell.Scene);
        RuntimeSceneInstanceId secondId = context.Service.RequestAdditiveSceneLoad(context.SecondCell.Scene);

        Assert.Equal(1, stableWorld.EntityCount);
        Assert.True(context.Service.TryGetSceneInstance(firstId, out var queued));
        Assert.Equal(RuntimeSceneInstanceState.QueuedForActivation, queued.State);

        SceneLoadResult? activation = context.Service.ProcessPendingSceneLoadAtFrameBoundary();

        Assert.True(activation.HasValue);
        Assert.True(activation.Value.Success, activation.Value.Diagnostic);
        Assert.Same(stableWorld, context.Service.ActiveScene!.EntityManager);
        Assert.Equal(3, stableWorld.EntityCount);
        Assert.Equal(3, context.Service.GetSceneInstances().Count);
        Assert.All(
            context.Service.GetSceneInstances(),
            instance => Assert.Equal(RuntimeSceneInstanceState.Active, instance.State));

        Assert.True(context.Service.TryResolveEntity(firstId, context.FirstCell.EntityGuid, out Entity firstEntity));
        Assert.True(context.Service.TryResolveEntity(secondId, context.SecondCell.EntityGuid, out Entity secondEntity));
        Assert.True(context.Service.TryGetEntityOwner(firstEntity, out RuntimeSceneInstanceId firstOwner));
        Assert.Equal(firstId, firstOwner);
        Assert.True(stableWorld.IsAlive(secondEntity));
        Assert.Equal(3, ExtractPointLights(stableWorld));

        Assert.True(context.Service.RequestSceneUnload(firstId));
        Assert.Equal(3, stableWorld.EntityCount);
        Assert.True(context.Service.TryGetSceneInstance(firstId, out var queuedUnload));
        Assert.Equal(RuntimeSceneInstanceState.QueuedForUnload, queuedUnload.State);

        Assert.Null(context.Service.ProcessPendingSceneLoadAtFrameBoundary());

        Assert.Equal(2, stableWorld.EntityCount);
        Assert.False(stableWorld.IsAlive(firstEntity));
        Assert.True(stableWorld.IsAlive(secondEntity));
        Assert.False(context.Service.TryResolveEntity(firstId, context.FirstCell.EntityGuid, out _));
        Assert.False(context.Service.TryGetEntityOwner(firstEntity, out _));
        Assert.True(context.Service.TryGetSceneInstance(firstId, out var unloaded));
        Assert.Equal(RuntimeSceneInstanceState.Unloaded, unloaded.State);
        Assert.Equal(2, ExtractPointLights(stableWorld));

        Entity reused = stableWorld.CreateEntity();
        Assert.Equal(firstEntity.Id, reused.Id);
        Assert.NotEqual(firstEntity.Generation, reused.Generation);
        Assert.False(stableWorld.HasComponent<PointLightComponent>(firstEntity));
        stableWorld.DestroyEntity(reused);
    }

    [Fact]
    public void CrossInstanceHierarchyReference_RejectsUnloadUntilReferenceIsRemoved()
    {
        using var context = new SceneInstanceContext();
        Assert.True(context.Service.LoadScene(context.Persistent.Scene).Success);
        RuntimeSceneInstanceId cellId = context.Service.RequestAdditiveSceneLoad(context.FirstCell.Scene);
        Assert.True(context.Service.ProcessPendingSceneLoadAtFrameBoundary()!.Value.Success);

        Assert.True(context.Service.TryResolveEntity(
            context.Service.ActiveScene!.InstanceId,
            context.Persistent.EntityGuid,
            out Entity persistentEntity));
        Assert.True(context.Service.TryResolveEntity(cellId, context.FirstCell.EntityGuid, out Entity cellEntity));
        context.World.AddComponent(cellEntity, new ParentComponent { Parent = persistentEntity });

        Assert.True(context.Service.RequestSceneUnload(cellId));
        context.Service.ProcessPendingSceneLoadAtFrameBoundary();

        Assert.True(context.World.IsAlive(cellEntity));
        Assert.True(context.Service.TryGetSceneInstance(cellId, out var rejected));
        Assert.Equal(RuntimeSceneInstanceState.Active, rejected.State);
        Assert.Contains("crosses the unload boundary", rejected.Diagnostic, StringComparison.OrdinalIgnoreCase);

        context.World.RemoveComponent<ParentComponent>(cellEntity);
        Assert.True(context.Service.RequestSceneUnload(cellId));
        context.Service.ProcessPendingSceneLoadAtFrameBoundary();
        Assert.False(context.World.IsAlive(cellEntity));
    }

    [Fact]
    public void ReplaceScene_UsesControlledOwnershipUnloadWithoutReplacingWorld()
    {
        using var context = new SceneInstanceContext();
        Assert.True(context.Service.LoadScene(context.Persistent.Scene).Success);
        RuntimeSceneInstanceId cellId = context.Service.RequestAdditiveSceneLoad(context.FirstCell.Scene);
        Assert.True(context.Service.ProcessPendingSceneLoadAtFrameBoundary()!.Value.Success);
        Assert.True(context.Service.TryResolveEntity(cellId, context.FirstCell.EntityGuid, out Entity oldCellEntity));
        EntityManager stableWorld = context.World;

        SceneLoadResult replacement = context.Service.LoadScene(context.Replacement.Scene);

        Assert.True(replacement.Success, replacement.Diagnostic);
        Assert.Same(stableWorld, context.Service.ActiveScene!.EntityManager);
        Assert.Equal(context.Replacement.Scene.Guid, context.Service.ActiveScene.Scene.Guid);
        Assert.Single(context.Service.GetSceneInstances());
        Assert.Equal(1, stableWorld.EntityCount);
        Assert.False(stableWorld.IsAlive(oldCellEntity));
    }

    [Fact]
    public void ReloadActivePersistentScene_PreservesAdditiveInstances()
    {
        using var context = new SceneInstanceContext();
        Assert.True(context.Service.LoadScene(context.Persistent.Scene).Success);
        RuntimeSceneInstanceId originalPersistentId = context.Service.ActiveScene!.InstanceId;
        Assert.True(context.Service.TryResolveEntity(
            originalPersistentId,
            context.Persistent.EntityGuid,
            out Entity originalPersistentEntity));

        RuntimeSceneInstanceId cellId = context.Service.RequestAdditiveSceneLoad(context.FirstCell.Scene);
        Assert.True(context.Service.ProcessPendingSceneLoadAtFrameBoundary()!.Value.Success);
        Assert.True(context.Service.TryResolveEntity(
            cellId,
            context.FirstCell.EntityGuid,
            out Entity cellEntity));

        context.Service.RequestSceneLoad(context.Persistent.Scene);
        SceneLoadResult? reload = context.Service.ProcessPendingSceneLoadAtFrameBoundary();

        Assert.True(reload.HasValue);
        Assert.True(reload.Value.Success, reload.Value.Diagnostic);
        Assert.NotEqual(originalPersistentId, context.Service.ActiveScene!.InstanceId);
        Assert.False(context.World.IsAlive(originalPersistentEntity));
        Assert.True(context.World.IsAlive(cellEntity));
        Assert.True(context.Service.TryResolveEntity(
            cellId,
            context.FirstCell.EntityGuid,
            out Entity preservedCellEntity));
        Assert.Equal(cellEntity, preservedCellEntity);
        Assert.Equal(2, context.Service.GetSceneInstances().Count);

        Assert.True(context.Service.RequestSceneUnload(cellId));
        Assert.Null(context.Service.ProcessPendingSceneLoadAtFrameBoundary());
        Assert.False(context.World.IsAlive(cellEntity));
        Assert.Single(context.Service.GetSceneInstances());
    }

    [Fact]
    public void RepeatedAdditiveCycles_KeepSlotsPoolsOwnershipAndHistoryBounded()
    {
        using var context = new SceneInstanceContext();
        Assert.True(context.Service.LoadScene(context.Persistent.Scene).Success);
        int lifecycleEventCount = 0;
        context.Service.SceneInstanceStateChanged += _ => lifecycleEventCount++;

        for (int i = 0; i < 160; i++)
        {
            RuntimeSceneInstanceId cellId = context.Service.RequestAdditiveSceneLoad(context.FirstCell.Scene);
            SceneLoadResult? loaded = context.Service.ProcessPendingSceneLoadAtFrameBoundary();
            Assert.True(loaded.HasValue);
            Assert.True(loaded.Value.Success, loaded.Value.Diagnostic);
            Assert.True(context.Service.TryResolveEntity(cellId, context.FirstCell.EntityGuid, out Entity entity));

            Assert.True(context.Service.RequestSceneUnload(cellId));
            Assert.Null(context.Service.ProcessPendingSceneLoadAtFrameBoundary());
            Assert.False(context.World.IsAlive(entity));
            Assert.False(context.Service.TryGetEntityOwner(entity, out _));
        }

        Assert.Equal(320, lifecycleEventCount);
        Assert.Equal(1, context.World.EntityCount);
        Assert.Equal(1, context.World.GetPool<TransformComponent>().Count);
        Assert.Equal(1, context.World.GetPool<PointLightComponent>().Count);
        Assert.Single(context.Service.GetSceneInstances());
        Assert.True(context.World.AllocatedSlotCount <= 2);
        Assert.True(context.Service.GetDiagnostics().Count <= 128);
    }

    private static int ExtractPointLights(EntityManager entityManager)
    {
        ComponentPool<PointLightComponent> lightPool = entityManager.GetPool<PointLightComponent>();
        var destination = new PointLight[PointLightSnapshotExtractor.MaxPointLightsPerFrame];
        PointLightExtractionStats stats = PointLightSnapshotExtractor.Extract(
            lightPool.GetRawComponentArray().AsSpan(0, lightPool.Count),
            lightPool.GetRawEntityArray().AsSpan(0, lightPool.Count),
            entityManager.GetPool<TransformComponent>(),
            destination);
        return stats.AcceptedCount;
    }

    private sealed class SceneInstanceContext : IDisposable
    {
        private readonly TestDirectory m_Temp = new();

        public SceneInstanceContext()
        {
            Database = new TestAssetDatabase(AssetSourceAccessMode.Diagnostic, m_Temp.Path);
            Persistent = AddScene("Persistent", "71000000-0000-0000-0000-000000000001", 1.0f);
            FirstCell = AddScene("FirstCell", "71000000-0000-0000-0000-000000000002", 2.0f);
            SecondCell = AddScene("SecondCell", "71000000-0000-0000-0000-000000000003", 3.0f);
            Replacement = AddScene("Replacement", "71000000-0000-0000-0000-000000000004", 4.0f);
            World = new EntityManager();
            Service = new RuntimeSceneService(Database, World);
        }

        public TestAssetDatabase Database { get; }
        public EntityManager World { get; }
        public RuntimeSceneService Service { get; }
        public SceneFixture Persistent { get; }
        public SceneFixture FirstCell { get; }
        public SceneFixture SecondCell { get; }
        public SceneFixture Replacement { get; }

        public void Dispose()
        {
            m_Temp.Dispose();
        }

        private SceneFixture AddScene(string name, string guidText, float x)
        {
            Guid sceneGuid = Guid.Parse(guidText);
            string sourcePath = Path.Combine(m_Temp.Path, $"{name}.arisenscene");
            File.WriteAllText(sourcePath, SceneTestSource.MigrateLegacy(sceneGuid, sourcePath, $$"""
                Name: {{name}}
                Entities:
                - Name: {{name}} Light
                  Transform:
                    Position: { X: {{x}}, Y: 1, Z: 2 }
                  PointLight:
                    Color: { X: 1, Y: 0.8, Z: 0.6 }
                    Intensity: 2
                    Range: 10
                    Enabled: true
                """));
            Database.AddAsset(sceneGuid, "Scene", sourcePath, "com.arisen.test");
            var scene = new AssetRef<SceneSourceAsset>(sceneGuid, "Scene", "com.arisen.test");
            SceneInspectionResult inspection = SceneAssetLoader.InspectScene(Database, scene);
            Assert.True(inspection.Success, inspection.Diagnostic);
            return new SceneFixture(scene, Assert.Single(inspection.Entities).AuthoringGuid);
        }
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenRuntimeSceneInstanceTests",
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
                // Best-effort test cleanup.
            }
        }
    }

    private readonly record struct SceneFixture(
        AssetRef<SceneSourceAsset> Scene,
        Guid EntityGuid);
}

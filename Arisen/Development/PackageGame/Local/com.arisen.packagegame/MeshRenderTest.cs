using System;
using System.Numerics;
using ArisenEngine.Core.ECS;
using ArisenEngine.ECS.Lifecycle;
using ArisenKernel.Diagnostics;

namespace PackageGame;

public static class MeshRenderTest
{
    public static void Setup(SceneSubsystem scene)
    {
        KernelLog.Info("[MeshRenderTest] Setting up verification scene...");

        scene.RegisterSystem(new MeshSystem());

        var em = scene.ActiveEntityManager;
        var cameraEntity = em.CreateEntity();
        em.AddComponent(cameraEntity, new TransformComponent
        {
            Position = new Vector3(0.0f, 0.0f, -2.5f),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });
        em.AddComponent(cameraEntity, CameraComponent.Default);

        var meshGuid = GameLogicAssetRefs.MultiSubmeshQuadMesh.Ref.Guid;
        var materialGuid = Guid.Empty;

        AddMeshEntity(em, meshGuid, materialGuid, new Vector3(-0.05f, 0.0f, 0.35f));
        AddMeshEntity(em, meshGuid, materialGuid, new Vector3(0.05f, 0.0f, 0.0f));

        KernelLog.Info("[MeshRenderTest] Scene smoke created with 1 camera and 2 asset-authored static mesh entities.");
    }

    public static void Shutdown()
    {
    }

    private static void AddMeshEntity(EntityManager em, Guid meshGuid, Guid materialGuid, Vector3 position)
    {
        var entity = em.CreateEntity();
        em.AddComponent(entity, new TransformComponent
        {
            Position = position,
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });

        em.AddComponent(entity, MeshRendererComponent.Create(meshGuid, materialGuid));
    }
}

using System;
using System.Numerics;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.ECS.Lifecycle;
using ArisenEngine.Resources.Serialization;
using ArisenKernel.Diagnostics;

namespace PackageGame;

public static class MeshRenderTest
{
    public static void Setup(SceneSubsystem scene, IAssetDatabase? assetDatabase)
    {
        KernelLog.Info("[MeshRenderTest] Setting up verification scene...");

        scene.RegisterSystem(new MeshSystem());

        if (assetDatabase != null)
        {
            var result = SceneAssetLoader.LoadScene(
                assetDatabase,
                GameLogicAssetRefs.SmokeScene.Ref,
                scene.ActiveEntityManager);
            if (result.Success)
            {
                KernelLog.Info(result.Diagnostic);
                return;
            }

            KernelLog.Warning(result.Diagnostic);
            KernelLog.Warning("[MeshRenderTest] Falling back to code-created smoke scene.");
        }
        else
        {
            KernelLog.Warning("[MeshRenderTest] Asset database unavailable. Falling back to code-created smoke scene.");
        }

        var em = scene.ActiveEntityManager;
        var cameraEntity = em.CreateEntity();
        em.AddComponent(cameraEntity, new TransformComponent
        {
            Position = new Vector3(0.0f, 0.0f, -2.5f),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });
        em.AddComponent(cameraEntity, CameraComponent.Default);

        var lightEntity = em.CreateEntity();
        em.AddComponent(lightEntity, TransformComponent.Identity);
        em.AddComponent(lightEntity, DirectionalLightComponent.Default);

        var environmentEntity = em.CreateEntity();
        em.AddComponent(environmentEntity, TransformComponent.Identity);
        em.AddComponent(environmentEntity, SceneEnvironmentComponent.Default);

        var meshGuid = GameLogicAssetRefs.MultiSubmeshQuadMesh.Ref.Guid;
        var materialGuid = Guid.Empty;

        AddMeshEntity(em, meshGuid, materialGuid, new Vector3(-0.05f, 0.0f, 0.35f));
        AddMeshEntity(em, meshGuid, materialGuid, new Vector3(0.05f, 0.0f, 0.0f));

        KernelLog.Info("[MeshRenderTest] Scene smoke created with 1 camera, 1 directional light, 1 environment, and 2 asset-authored static mesh entities.");
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

using System;
using ArisenEngine.Core.Lifecycle;

namespace ArisenEngine.Core.ECS.Systems;

/// <summary>
/// A system that processes Editor Camera entities to handle movement and rotation.
/// Adheres to DOD by processing Transform and Camera components in bulk.
/// </summary>
public class EditorCameraSystem
{
    private EntityManager _entityManager;

    public EditorCameraSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Update(float deltaTime)
    {
        // In a true DOD engine, we would 'Query' for entities with both Transform and Camera.
        // For simplicity in Phase 2, we iterate the Camera pool and match transforms.
        var cameraPool = _entityManager.GetPool<CameraComponent>();
        var transformPool = _entityManager.GetPool<TransformComponent>();
        
        var cameras = cameraPool.GetRawComponentArray();
        var cameraEntities = cameraPool.GetRawEntityArray();
        int count = cameraPool.Count;

        for (int i = 0; i < count; i++)
        {
            Entity entity = cameraEntities[i];
            if (transformPool.Has(entity))
            {
                ref TransformComponent transform = ref transformPool.GetRef(entity);
                UpdateCameraMovement(ref transform, deltaTime);
            }
        }
    }

    private void UpdateCameraMovement(ref TransformComponent transform, float deltaTime)
    {
        // Simple WASD movement placeholder
        // In a real system, we'd query the InputSubsystem
        float speed = 10.0f;
        
        // This is still a placeholder until Input bridge is verified
        // For now, let's just make the camera rotate slowly to see if rendering works
        transform.Rotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, deltaTime * 0.2f);
    }
}

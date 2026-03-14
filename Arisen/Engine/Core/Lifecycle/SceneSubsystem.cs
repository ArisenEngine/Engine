using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.ECS.Systems;
using ArisenEngine.Core.ECS;

namespace ArisenEngine.Core.Lifecycle;

/// <summary>
/// Manages the active ECS world, entities, and systemic updates.
/// </summary>
public class SceneSubsystem : ITickableSubsystem
{
    public int Priority => 50; // Execute before Rendering (100)
    public EnginePhase InitPhase => EnginePhase.Init;

    public EntityManager ActiveEntityManager { get; private set; }
    private EditorCameraSystem _cameraSystem;

    public void Initialize()
    {
        ActiveEntityManager = new EntityManager();
        _cameraSystem = new EditorCameraSystem(ActiveEntityManager);
    }

    public void Tick(float deltaTime)
    {
        // Execute ECS systems in order
        _cameraSystem.Update(deltaTime);
    }

    public void Shutdown()
    {
        ActiveEntityManager = null;
    }

    public void Dispose() => Shutdown();
}

using ArisenEngine.Core.Lifecycle;
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

    public void Initialize()
    {
        ActiveEntityManager = new EntityManager();
    }

    public void Tick(float deltaTime)
    {
        // Execute ECS systems in order (will be populated dynamically later)
    }

    public void Shutdown()
    {
        ActiveEntityManager = null;
    }

    public void Dispose() => Shutdown();
}

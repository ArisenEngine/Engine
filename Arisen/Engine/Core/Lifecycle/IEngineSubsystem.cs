namespace ArisenEngine.Core.Lifecycle;

public enum EnginePhase
{
    None,
    PreInit,      // Native DLL loading, Logger initialization
    Init,         // RHI, JobSystem, MemoryManager initialization
    PostInit,     // AssetPipeline, default World creation
    Running,      // Frame loop
    PreShutdown,  // Cleanup World, release GPU resources
    Shutdown      // Native shutdown, log flush
}

public interface IEngineSubsystem : IDisposable
{
    int Priority { get; }
    EnginePhase InitPhase { get; }
    
    void Initialize();
    void Shutdown();
}

public interface ITickableSubsystem : IEngineSubsystem
{
    void Tick(float deltaTime);
}

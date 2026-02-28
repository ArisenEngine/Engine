using ArisenEngine.Core.Diagnostics;

namespace ArisenEngine.Core.Lifecycle;

public sealed class EngineKernel : IDisposable
{
    private static readonly Lazy<EngineKernel> s_Instance = new(() => new EngineKernel());
    public static EngineKernel Instance => s_Instance.Value;

    private readonly List<IEngineSubsystem> m_Subsystems = new();
    private EnginePhase m_CurrentPhase = EnginePhase.None;
    private bool m_IsRunning = false;

    public EnginePhase CurrentPhase => m_CurrentPhase;

    private EngineKernel() { }

    public void RegisterSubsystem(IEngineSubsystem subsystem)
    {
        if (m_CurrentPhase != EnginePhase.None)
            throw new InvalidOperationException("Cannot register subsystems after initialization has started");
        
        m_Subsystems.Add(subsystem);
    }

    public void Initialize()
    {
        Logger.Log("[EngineKernel] Initializing...");
        
        // Sort subsystems by priority
        m_Subsystems.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        TransitionTo(EnginePhase.PreInit);
        TransitionTo(EnginePhase.Init);
        TransitionTo(EnginePhase.PostInit);
        
        m_IsRunning = true;
        m_CurrentPhase = EnginePhase.Running;
        Logger.Log("[EngineKernel] Engine is now Running.");
    }

    private void TransitionTo(EnginePhase phase)
    {
        m_CurrentPhase = phase;
        Logger.Log($"[EngineKernel] Transitioning to phase: {phase}");

        foreach (var subsystem in m_Subsystems)
        {
            if (subsystem.InitPhase == phase)
            {
                Logger.Log($"  [Subsystem] Initializing: {subsystem.GetType().Name}");
                subsystem.Initialize();
            }
        }
    }

    public void Run()
    {
        if (!m_IsRunning) Initialize();
        
        // Simplified loop for now (real one will be in ArisenApplication)
        // while (m_IsRunning) { ... }
    }

    public void Shutdown()
    {
        Logger.Log("[EngineKernel] Shutting down...");
        m_CurrentPhase = EnginePhase.PreShutdown;
        
        // Shutdown in reverse priority order
        var reversedSubsystems = m_Subsystems.AsEnumerable().Reverse().ToList();
        
        foreach (var subsystem in reversedSubsystems)
        {
            Logger.Log($"  [Subsystem] Shutting down: {subsystem.GetType().Name}");
            subsystem.Shutdown();
        }

        m_CurrentPhase = EnginePhase.Shutdown;
        m_IsRunning = false;
        Logger.Log("[EngineKernel] Shutdown complete.");
    }

    public void Dispose()
    {
        Shutdown();
    }
}

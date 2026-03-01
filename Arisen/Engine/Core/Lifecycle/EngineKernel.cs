using ArisenEngine.Core.Diagnostics;
using System.Linq;

namespace ArisenEngine.Core.Lifecycle;

public sealed class EngineKernel : IDisposable
{
    private static readonly Lazy<EngineKernel> s_Instance = new(() => new EngineKernel());
    public static EngineKernel Instance => s_Instance.Value;

    private readonly List<IEngineSubsystem> m_Subsystems = new();
    private EnginePhase m_CurrentPhase = EnginePhase.None;
    private bool m_IsRunning = false;

    public EnginePhase CurrentPhase => m_CurrentPhase;
    public EngineConfig Config { get; private set; }
    public uint CurrentFrameIndex { get; private set; } = 0;

    private EngineKernel()
    {
    }

    public void RegisterSubsystem<T>(T subsystem) where T : class, IEngineSubsystem
    {
        if (m_CurrentPhase != EnginePhase.None)
            throw new InvalidOperationException("Cannot register subsystems after initialization has started");

        if (m_Subsystems.Any(s => s is T))
            throw new InvalidOperationException($"Subsystem of type {typeof(T).Name} is already registered.");

        m_Subsystems.Add(subsystem);
    }

    public T GetSubsystem<T>() where T : class, IEngineSubsystem
    {
        return m_Subsystems.OfType<T>().FirstOrDefault();
    }

    public void Initialize(EngineConfig config)
    {
        using var _ = Profiler.Zone("EngineKernel.Initialize");
        Config = config;
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
        using var _ = Profiler.Zone($"EngineKernel.TransitionTo({phase})");
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

    public int Run()
    {
        using var _ = Profiler.Zone("EngineKernel.Run");
        if (!m_IsRunning)
        {
            Initialize(Config ?? new EngineConfig());
        }

        var frameScheduler = new FrameScheduler();

        while (m_IsRunning)
        {
            Profiler.FrameMark();
            using (Profiler.Zone("EngineKernel.Update"))
            {
                Time.Update();
                float deltaTime = Time.deltaTime;

                frameScheduler.ExecuteFrame(deltaTime, m_Subsystems);
            }

            // End of frame cleanup
            ArisenEngine.Core.Memory.FrameArena.Instance.Reset();
            CurrentFrameIndex++;
        }

        return 0;
    }

    public void RequestShutdown()
    {
        m_IsRunning = false;
    }

    public void Shutdown()
    {
        using var _ = Profiler.Zone("EngineKernel.Shutdown");
        if (m_CurrentPhase == EnginePhase.Shutdown || m_CurrentPhase == EnginePhase.PreShutdown)
            return;

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
        Logger.Log("[EngineKernel] Shutdown complete.");
    }

    public void Dispose()
    {
        Shutdown();
    }
}
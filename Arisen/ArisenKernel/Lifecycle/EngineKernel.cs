using System.Collections.Generic;
using System.Linq;
using ArisenKernel.Services;
using ArisenKernel.Diagnostics;
using ArisenKernel.Packages;

namespace ArisenKernel.Lifecycle;

public sealed class EngineKernel : IDisposable
{
    private static readonly Lazy<EngineKernel> s_Instance = new(() => new EngineKernel());
    public static EngineKernel Instance => s_Instance.Value;
    /// <summary>B13: Allows checking if the kernel has been instantiated without triggering Lazy creation.</summary>
    public static bool IsCreated => s_Instance.IsValueCreated;

    private readonly List<IEngineSubsystem> m_Subsystems = new();
    private readonly Dictionary<IEngineSubsystem, SubsystemRegistrationInfo> m_SubsystemInfo = new();
    private readonly List<IEngineSubsystem> m_InitializedSubsystems = new();
    private long m_NextSubsystemRegistrationOrder;
    private EnginePhase m_CurrentPhase = EnginePhase.None;
    private bool m_IsRunning = false;
    private FrameScheduler m_FrameScheduler = new FrameScheduler();

    public IServiceRegistry Services { get; } = new ServiceRegistry();

    public event Action? OnFrameEnd;

    public EnginePhase CurrentPhase => m_CurrentPhase;
    public EngineConfig? Config { get; private set; }
    public uint CurrentFrameIndex { get; private set; } = 0;

    public EngineKernel()
    {
    }

    public void Reset()
    {
        // Properly shutdown subsystems before clearing to avoid resource leaks
        if (m_CurrentPhase != EnginePhase.None && m_CurrentPhase != EnginePhase.Shutdown)
        {
            Shutdown();
        }

        m_Subsystems.Clear();
        m_SubsystemInfo.Clear();
        m_InitializedSubsystems.Clear();
        m_NextSubsystemRegistrationOrder = 0;
        m_CurrentPhase = EnginePhase.None;
        m_IsRunning = false;
        Config = null;
        CurrentFrameIndex = 0;
        // B10: Reset ServiceRegistry to avoid duplicate registrations on re-init
        (Services as ServiceRegistry)?.Clear();
    }

    public void RegisterSubsystem<T>(T subsystem) where T : class, IEngineSubsystem
    {
        RegisterSubsystem(
            subsystem,
            packageId: string.Empty,
            packageOrder: int.MaxValue,
            declaredClassName: subsystem.GetType().FullName ?? subsystem.GetType().Name,
            initPhase: subsystem.InitPhase,
            priority: subsystem.Priority);
    }

    public void RegisterSubsystem<T>(T subsystem, string packageId, int packageOrder, string declaredClassName) where T : class, IEngineSubsystem
    {
        RegisterSubsystem(subsystem, packageId, packageOrder, declaredClassName, subsystem.InitPhase, subsystem.Priority);
    }

    public void RegisterSubsystem<T>(T subsystem, string packageId, int packageOrder, string declaredClassName, EnginePhase initPhase, int priority) where T : class, IEngineSubsystem
    {
        if (m_CurrentPhase != EnginePhase.None)
            throw new InvalidOperationException("Cannot register subsystems after initialization has started");

        // B1: Check by concrete type to avoid ambiguity with interface-based checks
        if (m_Subsystems.Any(s => s.GetType() == subsystem.GetType()))
            throw new InvalidOperationException($"Subsystem of type {subsystem.GetType().Name} is already registered.");

        m_Subsystems.Add(subsystem);
        m_SubsystemInfo[subsystem] = new SubsystemRegistrationInfo(
            packageId,
            packageOrder,
            declaredClassName,
            initPhase,
            priority,
            m_NextSubsystemRegistrationOrder++);
    }

    public T? GetSubsystem<T>() where T : class, IEngineSubsystem
    {
        return m_Subsystems.OfType<T>().FirstOrDefault();
    }

    public void Initialize(EngineConfig config)
    {
        Config = config;
        KernelLog.Info("[EngineKernel] Initializing...");

        // 1. Mount packages through PackageSubsystem, the single owner of package runtime state.
        var packageSubsystem = GetSubsystem<PackageSubsystem>();
        if (packageSubsystem == null)
        {
            packageSubsystem = new PackageSubsystem();
            RegisterSubsystem(packageSubsystem, "ArisenKernel", 0, typeof(PackageSubsystem).FullName ?? nameof(PackageSubsystem));
        }

        if (config.PackageUrls.Count > 0)
        {
            KernelLog.Info("[EngineKernel] Mounting packages through PackageSubsystem...");
            packageSubsystem.MountPackages(config.PackageUrls);
            KernelLog.Info("[EngineKernel] Package mount complete.");
        }

        // 2. Sort subsystems deterministically by phase, package topological order, priority, then class name.
        m_Subsystems.Sort(CompareSubsystems);

                TransitionTo(EnginePhase.PreInit);
        TransitionTo(EnginePhase.Init);
        TransitionTo(EnginePhase.PostInit);
        TransitionTo(EnginePhase.Running);

        m_IsRunning = true;
        KernelLog.Info("[EngineKernel] Engine is now Running.");
    }

    private void TransitionTo(EnginePhase phase)
    {
        m_CurrentPhase = phase;
        KernelLog.Info($"[EngineKernel] Transitioning to phase: {phase}");

        foreach (var subsystem in m_Subsystems)
        {
            var info = GetSubsystemInfo(subsystem);
            if (info.InitPhase == phase)
            {
                KernelLog.Info($"  [Subsystem] Initializing: {info.DeclaredClassName} (Package: {info.PackageId}, Priority: {info.Priority})");
                subsystem.Initialize();
                m_InitializedSubsystems.Add(subsystem);
            }
        }
    }

    public int Run()
    {
        if (!m_IsRunning)
        {
            Initialize(Config ?? new EngineConfig());
        }

        while (m_IsRunning)
        {
            Time.Update();
            Tick(Time.deltaTime);
        }

        return 0;
    }

    public int RunForFrames(uint frameCount)
    {
        if (!m_IsRunning)
        {
            Initialize(Config ?? new EngineConfig());
        }

        KernelLog.Info($"[EngineKernel] Running bounded frame loop for {frameCount} frame(s).");
        for (uint i = 0; i < frameCount && m_IsRunning; i++)
        {
            Time.Update();
            Tick(Time.deltaTime);
        }

        RequestShutdown();
        Shutdown();
        return 0;
    }

    /// <summary>
    /// Executes a single frame of the engine.
    /// Exposing this allows external runners (like the Editor) to drive the loop.
    /// </summary>
    public void Tick(float deltaTime)
    {
        m_FrameScheduler.ExecuteFrame(deltaTime, m_Subsystems);

        // End of frame cleanup via event so kernel doesn't depend on Memory systems
        OnFrameEnd?.Invoke();

        CurrentFrameIndex++;
    }

    public void RequestShutdown()
    {
        m_IsRunning = false;
    }

    public void Shutdown()
    {
        if (m_CurrentPhase == EnginePhase.Shutdown || m_CurrentPhase == EnginePhase.PreShutdown)
            return;

        KernelLog.Info("[EngineKernel] Shutting down...");
        m_CurrentPhase = EnginePhase.PreShutdown;

        for (int i = m_InitializedSubsystems.Count - 1; i >= 0; i--)
        {
            KernelLog.Info($"  [Subsystem] Shutting down: {m_InitializedSubsystems[i].GetType().Name}");
            m_InitializedSubsystems[i].Shutdown();
        }
        m_InitializedSubsystems.Clear();

        m_CurrentPhase = EnginePhase.Shutdown;
        KernelLog.Info("[EngineKernel] Shutdown complete.");
    }

    private int CompareSubsystems(IEngineSubsystem left, IEngineSubsystem right)
    {
        var leftInfo = GetSubsystemInfo(left);
        var rightInfo = GetSubsystemInfo(right);

        int phaseCompare = leftInfo.InitPhase.CompareTo(rightInfo.InitPhase);
        if (phaseCompare != 0) return phaseCompare;

        int packageOrderCompare = leftInfo.PackageOrder.CompareTo(rightInfo.PackageOrder);
        if (packageOrderCompare != 0) return packageOrderCompare;

        int priorityCompare = leftInfo.Priority.CompareTo(rightInfo.Priority);
        if (priorityCompare != 0) return priorityCompare;

        int classCompare = string.Compare(leftInfo.DeclaredClassName, rightInfo.DeclaredClassName, StringComparison.Ordinal);
        if (classCompare != 0) return classCompare;

        return leftInfo.RegistrationOrder.CompareTo(rightInfo.RegistrationOrder);
    }

    private SubsystemRegistrationInfo GetSubsystemInfo(IEngineSubsystem subsystem)
    {
        if (m_SubsystemInfo.TryGetValue(subsystem, out var info)) return info;

        info = new SubsystemRegistrationInfo(
            PackageId: string.Empty,
            PackageOrder: int.MaxValue,
            DeclaredClassName: subsystem.GetType().FullName ?? subsystem.GetType().Name,
            InitPhase: subsystem.InitPhase,
            Priority: subsystem.Priority,
            RegistrationOrder: m_NextSubsystemRegistrationOrder++);
        m_SubsystemInfo[subsystem] = info;
        return info;
    }

    private sealed record SubsystemRegistrationInfo(
        string PackageId,
        int PackageOrder,
        string DeclaredClassName,
        EnginePhase InitPhase,
        int Priority,
        long RegistrationOrder);

    public IReadOnlyList<EngineSubsystemDiagnosticInfo> GetInitializedSubsystemDiagnostics()
    {
        return m_InitializedSubsystems
            .Select(subsystem =>
            {
                var info = GetSubsystemInfo(subsystem);
                return new EngineSubsystemDiagnosticInfo(
                    info.DeclaredClassName,
                    info.PackageId,
                    info.InitPhase,
                    info.Priority);
            })
            .ToArray();
    }

    public void Dispose()
    {
        Shutdown();
    }
}

public sealed record EngineSubsystemDiagnosticInfo(
    string ClassName,
    string PackageId,
    EnginePhase InitPhase,
    int Priority);


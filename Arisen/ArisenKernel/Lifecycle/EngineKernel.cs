using System.Collections.Generic;
using System.Diagnostics;
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
    private bool m_IsPackageGraphMounted;

    public IServiceRegistry Services { get; } = new ServiceRegistry();

    public event Action? OnFrameEnd;

    public EnginePhase CurrentPhase => m_CurrentPhase;
    public EngineConfig? Config { get; private set; }
    public uint CurrentFrameIndex { get; private set; } = 0;

    public bool IsPackageGraphMounted => m_IsPackageGraphMounted;

    public EngineKernel()
    {
    }

    public void Reset()
    {
        // Properly shutdown subsystems before clearing to avoid resource leaks
        if (m_IsPackageGraphMounted ||
            (m_CurrentPhase != EnginePhase.None && m_CurrentPhase != EnginePhase.Shutdown))
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
        m_IsPackageGraphMounted = false;
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
        KernelLog.Info("[EngineKernel] Initializing...");

        if (!m_IsPackageGraphMounted)
        {
            MountPackageGraph(config);
        }
        else if (!ReferenceEquals(Config, config))
        {
            throw new InvalidOperationException(
                "The mounted package graph cannot be initialized with a different engine configuration.");
        }

        // Sort subsystems deterministically by phase, package topological order, priority, then class name.
        m_Subsystems.Sort(CompareSubsystems);

        TransitionTo(EnginePhase.PreInit);
        TransitionTo(EnginePhase.Init);
        TransitionTo(EnginePhase.PostInit);
        TransitionTo(EnginePhase.Running);

        m_IsRunning = true;
        KernelLog.Info("[EngineKernel] Engine is now Running.");
    }

    /// <summary>
    /// Mounts package entries and services without entering subsystem phases. Build-stage hosts use
    /// this boundary to invoke package-owned tooling without creating windows, RHI devices, or live
    /// scene state.
    /// </summary>
    public void MountPackageGraph(EngineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (m_CurrentPhase != EnginePhase.None)
        {
            throw new InvalidOperationException(
                $"Packages cannot be mounted while the engine is in phase '{m_CurrentPhase}'.");
        }

        if (m_IsPackageGraphMounted)
        {
            throw new InvalidOperationException("The package graph is already mounted.");
        }

        Config = config;
        m_IsPackageGraphMounted = true;

        // PackageSubsystem remains the single owner of package runtime state.
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

    public int RunSmokeScenario(
        IRuntimeSmokeScenario scenario,
        uint maximumFrameCount,
        TimeSpan maximumDuration)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (maximumFrameCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrameCount));
        }

        if (maximumDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        }

        if (!m_IsRunning)
        {
            Initialize(Config ?? new EngineConfig());
        }

        KernelLog.Info(
            $"[EngineKernel] Running bounded smoke scenario '{scenario.Name}' for at most " +
            $"{maximumFrameCount} frame(s) and {maximumDuration.TotalSeconds:F0} second(s).");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            scenario.Start(CurrentFrameIndex);
            uint executedFrames = 0;
            while (m_IsRunning && !scenario.IsReadyForShutdown)
            {
                if (executedFrames >= maximumFrameCount)
                {
                    scenario.ReportFailure(
                        $"Scenario exceeded its {maximumFrameCount}-frame limit.");
                    break;
                }

                if (stopwatch.Elapsed >= maximumDuration)
                {
                    scenario.ReportFailure(
                        $"Scenario exceeded its {maximumDuration.TotalSeconds:F0}-second deadline.");
                    break;
                }

                uint frameIndex = CurrentFrameIndex;
                scenario.BeforeFrame(frameIndex);
                Time.Update();
                Tick(Time.deltaTime);
                scenario.AfterFrame(frameIndex);
                executedFrames++;
                Thread.Yield();
            }
        }
        catch (Exception ex)
        {
            scenario.ReportFailure($"Unhandled scenario error: {ex.Message}");
        }

        RequestShutdown();
        Shutdown();
        try
        {
            scenario.AfterShutdown();
        }
        catch (Exception ex)
        {
            scenario.ReportFailure($"Post-shutdown validation failed: {ex.Message}");
        }

        return scenario.IsComplete && scenario.Succeeded ? 0 : 1;
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

        PackageSubsystem? packageSubsystem = GetSubsystem<PackageSubsystem>();
        bool packageSubsystemWasInitialized = packageSubsystem != null &&
                                              m_InitializedSubsystems.Contains(packageSubsystem);

        for (int i = m_InitializedSubsystems.Count - 1; i >= 0; i--)
        {
            KernelLog.Info($"  [Subsystem] Shutting down: {m_InitializedSubsystems[i].GetType().Name}");
            m_InitializedSubsystems[i].Shutdown();
        }
        m_InitializedSubsystems.Clear();

        if (m_IsPackageGraphMounted && packageSubsystem != null && !packageSubsystemWasInitialized)
        {
            packageSubsystem.Shutdown();
        }

        m_IsPackageGraphMounted = false;

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


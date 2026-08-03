using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
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
    private readonly AsyncLocal<PackageSubsystemRegistration?> m_CurrentPackageRegistration = new();
    private long m_NextSubsystemRegistrationOrder;
    private EnginePhase m_CurrentPhase = EnginePhase.None;
    private bool m_IsRunning = false;
    private FrameScheduler m_FrameScheduler = new FrameScheduler();
    private bool m_IsPackageGraphMounted;
    private AggregateException? m_ShutdownFailure;

    public IServiceRegistry Services { get; } = new ServiceRegistry();
    public RenderSurfaceRegistry RenderSurfaces { get; private set; } = new();

    public event Action? OnFrameEnd;

    public EnginePhase CurrentPhase => m_CurrentPhase;
    public EngineConfig? Config { get; private set; }
    public uint CurrentFrameIndex { get; private set; } = 0;

    public bool IsPackageGraphMounted => m_IsPackageGraphMounted;

    public EngineKernel()
    {
    }

    /// <summary>
    /// Registers a service owned by the composition host rather than a package. Kernel shutdown
    /// removes all such services after package teardown and before validating the ownership baseline.
    /// </summary>
    public void RegisterKernelOwnedService<T>(T service)
    {
        using IDisposable registrationScope =
            ((ServiceRegistry)Services).BeginPackageRegistration(ServiceRegistry.KernelProviderId);
        Services.RegisterService(service);
    }

    public void Reset()
    {
        // Properly shutdown subsystems before clearing to avoid resource leaks
        if (m_IsPackageGraphMounted ||
            (m_CurrentPhase != EnginePhase.None && m_CurrentPhase != EnginePhase.Shutdown))
        {
            Shutdown();
        }

        RenderSurfaces.Dispose();
        RenderSurfaces = new RenderSurfaceRegistry();

        m_Subsystems.Clear();
        m_SubsystemInfo.Clear();
        m_InitializedSubsystems.Clear();
        m_NextSubsystemRegistrationOrder = 0;
        m_CurrentPhase = EnginePhase.None;
        m_IsRunning = false;
        Config = null;
        CurrentFrameIndex = 0;
        m_IsPackageGraphMounted = false;
        m_ShutdownFailure = null;
        m_CurrentPackageRegistration.Value = null;
        // B10: Reset ServiceRegistry to avoid duplicate registrations on re-init
        (Services as ServiceRegistry)?.Clear();
    }

    public void RegisterSubsystem<T>(T subsystem) where T : class, IEngineSubsystem
    {
        PackageSubsystemRegistration? packageRegistration = m_CurrentPackageRegistration.Value;
        RegisterSubsystem(
            subsystem,
            packageId: packageRegistration?.PackageId ?? string.Empty,
            packageOrder: packageRegistration?.PackageOrder ?? int.MaxValue,
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

    internal IDisposable BeginPackageSubsystemRegistration(string packageId, int packageOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        PackageSubsystemRegistration? previous = m_CurrentPackageRegistration.Value;
        m_CurrentPackageRegistration.Value = new PackageSubsystemRegistration(packageId, packageOrder);
        return new PackageSubsystemRegistrationScope(this, previous);
    }

    internal int UnregisterSubsystemsProvidedByPackage(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return 0;

        int removedCount = 0;
        for (int i = m_Subsystems.Count - 1; i >= 0; i--)
        {
            IEngineSubsystem subsystem = m_Subsystems[i];
            if (!m_SubsystemInfo.TryGetValue(subsystem, out SubsystemRegistrationInfo? info) ||
                !string.Equals(info.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (m_InitializedSubsystems.Contains(subsystem))
            {
                throw new InvalidOperationException(
                    $"Cannot unregister started subsystem '{info.DeclaredClassName}' before engine shutdown.");
            }

            m_Subsystems.RemoveAt(i);
            m_SubsystemInfo.Remove(subsystem);
            removedCount++;
        }

        return removedCount;
    }

    private bool UnregisterSubsystem(IEngineSubsystem subsystem)
    {
        if (m_InitializedSubsystems.Contains(subsystem))
        {
            throw new InvalidOperationException(
                $"Cannot unregister started subsystem '{subsystem.GetType().FullName}'.");
        }

        m_SubsystemInfo.Remove(subsystem);
        return m_Subsystems.Remove(subsystem);
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

        try
        {
            // Sort subsystems deterministically by phase, package topological order, priority, then class name.
            m_Subsystems.Sort(CompareSubsystems);

            TransitionTo(EnginePhase.PreInit);
            TransitionTo(EnginePhase.Init);
            TransitionTo(EnginePhase.PostInit);
            TransitionTo(EnginePhase.Running);
        }
        catch (Exception initializationError)
        {
            m_IsRunning = false;
            try
            {
                Shutdown();
            }
            catch (Exception shutdownError)
            {
                var failures = new List<Exception> { initializationError };
                AddFailure(failures, shutdownError);
                var combinedFailure = new AggregateException(
                    "Engine initialization failed and shutdown reported additional errors.",
                    failures);
                m_ShutdownFailure = combinedFailure;
                throw combinedFailure;
            }

            ExceptionDispatchInfo.Capture(initializationError).Throw();
            throw;
        }

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

        EngineConfig? previousConfig = Config;
        Config = config;

        // PackageSubsystem remains the single owner of package runtime state.
        var packageSubsystem = GetSubsystem<PackageSubsystem>();
        bool packageSubsystemWasCreated = packageSubsystem == null;
        try
        {
            if (packageSubsystem == null)
            {
                packageSubsystem = new PackageSubsystem();
                RegisterSubsystem(packageSubsystem, "ArisenKernel", 0, typeof(PackageSubsystem).FullName ?? nameof(PackageSubsystem));
            }

            if (config.PackageUrls.Count > 0)
            {
                KernelLog.Info("[EngineKernel] Mounting packages through PackageSubsystem...");
                packageSubsystem.MountPackages(
                    config.PackageUrls,
                    config.PackageRequirements);
                KernelLog.Info("[EngineKernel] Package mount complete.");
            }

            m_IsPackageGraphMounted = true;
        }
        catch
        {
            if (packageSubsystemWasCreated && packageSubsystem != null)
            {
                UnregisterSubsystem(packageSubsystem);
            }

            Config = previousConfig;
            m_IsPackageGraphMounted = false;
            throw;
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
                // A subsystem owns cleanup as soon as initialization begins, including partial setup.
                m_InitializedSubsystems.Add(subsystem);
                subsystem.Initialize();
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
        if (m_CurrentPhase == EnginePhase.Shutdown)
        {
            if (m_ShutdownFailure != null) throw m_ShutdownFailure;
            return;
        }

        if (m_CurrentPhase == EnginePhase.PreShutdown) return;

        KernelLog.Info("[EngineKernel] Shutting down...");
        m_CurrentPhase = EnginePhase.PreShutdown;
        m_IsRunning = false;
        var failures = new List<Exception>();

        PackageSubsystem? packageSubsystem = GetSubsystem<PackageSubsystem>();
        bool packageSubsystemWasStarted = false;

        for (int i = m_InitializedSubsystems.Count - 1; i >= 0; i--)
        {
            IEngineSubsystem subsystem = m_InitializedSubsystems[i];
            if (ReferenceEquals(subsystem, packageSubsystem))
            {
                packageSubsystemWasStarted = true;
                m_InitializedSubsystems.RemoveAt(i);
                continue;
            }

            KernelLog.Info($"  [Subsystem] Shutting down: {subsystem.GetType().Name}");
            try
            {
                subsystem.Shutdown();
            }
            catch (Exception error)
            {
                KernelLog.Error(
                    $"[EngineKernel] Subsystem '{subsystem.GetType().FullName}' shutdown failed: {error.Message}");
                AddFailure(failures, error);
            }
            finally
            {
                m_InitializedSubsystems.RemoveAt(i);
            }
        }

        try
        {
            RenderSurfaces.Dispose();
        }
        catch (Exception error)
        {
            KernelLog.Error(
                $"[EngineKernel] Render-surface registry disposal failed: {error.Message}");
            AddFailure(failures, error);
        }

        if (packageSubsystem != null && (m_IsPackageGraphMounted || packageSubsystemWasStarted))
        {
            KernelLog.Info("  [Subsystem] Shutting down: PackageSubsystem");
            try
            {
                packageSubsystem.Shutdown();
            }
            catch (Exception error)
            {
                KernelLog.Error($"[EngineKernel] Package shutdown failed: {error.Message}");
                AddFailure(failures, error);
            }
        }

        if (Services is ServiceRegistry serviceRegistry)
        {
            try
            {
                int removedServiceCount = serviceRegistry.UnregisterServicesProvidedByPackage(
                    ServiceRegistry.KernelProviderId);
                if (removedServiceCount > 0)
                {
                    KernelLog.Info(
                        $"[EngineKernel] Unregistered {removedServiceCount} kernel-owned service(s).");
                }
            }
            catch (Exception error)
            {
                KernelLog.Error(
                    $"[EngineKernel] Kernel-owned service shutdown failed: {error.Message}");
                AddFailure(failures, error);
            }
        }

        m_IsPackageGraphMounted = false;
        m_CurrentPhase = EnginePhase.Shutdown;
        EngineShutdownOwnershipSnapshot ownership = GetShutdownOwnershipSnapshot();
        KernelLog.Info(
            $"[EngineKernel] Shutdown baseline: packages={ownership.PackageCount}, " +
            $"contexts={ownership.ManagedLoadContextCount}, " +
            $"nativeRuntimes={ownership.NativeRuntimeCount}, " +
            $"services={ownership.ServiceCount}, " +
            $"initializedSubsystems={ownership.InitializedSubsystemCount}, " +
            $"renderSurfaces={ownership.RenderSurfaceCount}, " +
            $"surfaceRegistryDisposed={ownership.RenderSurfaceRegistryDisposed}.");
        if (!ownership.IsClean)
        {
            AddFailure(
                failures,
                new InvalidOperationException(
                    $"Engine shutdown left residual ownership: {ownership}."));
        }

        if (failures.Count > 0)
        {
            m_ShutdownFailure = new AggregateException(
                "Engine shutdown completed with one or more cleanup errors.",
                failures);
            KernelLog.Error($"[EngineKernel] Shutdown completed with {failures.Count} error(s).");
            throw m_ShutdownFailure;
        }

        m_ShutdownFailure = null;
        KernelLog.Info("[EngineKernel] Shutdown complete.");
    }

    internal EngineShutdownOwnershipSnapshot GetShutdownOwnershipSnapshot()
    {
        PackageSubsystem? packageSubsystem = GetSubsystem<PackageSubsystem>();
        return new EngineShutdownOwnershipSnapshot(
            PackageCount: packageSubsystem?.GetAllPackages().Count() ?? 0,
            ManagedLoadContextCount: packageSubsystem?.LoadedContextCount ?? 0,
            NativeRuntimeCount: packageSubsystem?.LoadedNativeRuntimeCount ?? 0,
            ServiceCount: Services.GetRegisteredServices().Count,
            InitializedSubsystemCount: m_InitializedSubsystems.Count,
            RenderSurfaceCount: RenderSurfaces.Count,
            RenderSurfaceRegistryDisposed: RenderSurfaces.IsDisposed,
            IsPackageGraphMounted: m_IsPackageGraphMounted);
    }

    private static void AddFailure(List<Exception> failures, Exception error)
    {
        if (error is AggregateException aggregate)
        {
            failures.AddRange(aggregate.Flatten().InnerExceptions);
            return;
        }

        failures.Add(error);
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

    private sealed record PackageSubsystemRegistration(string PackageId, int PackageOrder);

    private sealed class PackageSubsystemRegistrationScope : IDisposable
    {
        private readonly EngineKernel m_Kernel;
        private readonly PackageSubsystemRegistration? m_Previous;
        private bool m_Disposed;

        public PackageSubsystemRegistrationScope(
            EngineKernel kernel,
            PackageSubsystemRegistration? previous)
        {
            m_Kernel = kernel;
            m_Previous = previous;
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Kernel.m_CurrentPackageRegistration.Value = m_Previous;
            m_Disposed = true;
        }
    }

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

internal readonly record struct EngineShutdownOwnershipSnapshot(
    int PackageCount,
    int ManagedLoadContextCount,
    int NativeRuntimeCount,
    int ServiceCount,
    int InitializedSubsystemCount,
    int RenderSurfaceCount,
    bool RenderSurfaceRegistryDisposed,
    bool IsPackageGraphMounted)
{
    public bool IsClean =>
        PackageCount == 0 &&
        ManagedLoadContextCount == 0 &&
        NativeRuntimeCount == 0 &&
        ServiceCount == 0 &&
        InitializedSubsystemCount == 0 &&
        RenderSurfaceCount == 0 &&
        RenderSurfaceRegistryDisposed &&
        !IsPackageGraphMounted;
}


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
    private readonly Dictionary<IEngineSubsystem, SubsystemRegistrationInfo> m_SubsystemInfo =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<IEngineSubsystem> m_InitializedSubsystems = new();
    private readonly List<Exception> m_DeferredShutdownFailures = new();
    private readonly AsyncLocal<PackageSubsystemRegistration?> m_CurrentPackageRegistration = new();
    private long m_NextSubsystemRegistrationOrder;
    private EnginePhase m_CurrentPhase = EnginePhase.None;
    private bool m_IsRunning = false;
    private FrameScheduler m_FrameScheduler = new FrameScheduler();
    private bool m_IsPackageGraphMounted;
    private int m_ActiveLifecycleOperation;
    private int m_LifecycleOwnerThreadId;
    private AggregateException? m_ShutdownFailure;

    public IServiceRegistry Services { get; } = new ServiceRegistry();
    public RenderSurfaceRegistry RenderSurfaces { get; private set; } = new();

    public event Action? OnFrameEnd;

    public EnginePhase CurrentPhase => m_CurrentPhase;
    public EngineConfig? Config { get; private set; }
    public uint CurrentFrameIndex { get; private set; } = 0;

    public bool IsPackageGraphMounted => m_IsPackageGraphMounted;
    public bool HasPendingPackageCleanup =>
        GetSubsystem<PackageSubsystem>()?.HasPendingCleanup == true;
    internal bool HasPackageRuntimeOwnership =>
        GetSubsystem<PackageSubsystem>()?.HasOwnedRuntimeState == true;

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
        LifecycleOperationScope operationScope = BeginLifecycleOperation(
            EngineLifecycleOperation.Resetting);
        var serviceRegistry = (ServiceRegistry)Services;
        serviceRegistry.CloseRegistration();

        try
        {
            // Properly shutdown subsystems before clearing to avoid resource leaks
            if (m_IsPackageGraphMounted ||
                HasPackageRuntimeOwnership ||
                (m_CurrentPhase != EnginePhase.None && m_CurrentPhase != EnginePhase.Shutdown))
            {
                ShutdownCore();
            }

            RenderSurfaces.Dispose();
            RenderSurfaces = new RenderSurfaceRegistry();

            m_Subsystems.Clear();
            m_SubsystemInfo.Clear();
            m_InitializedSubsystems.Clear();
            m_FrameScheduler = new FrameScheduler();
            m_NextSubsystemRegistrationOrder = 0;
            m_IsRunning = false;
            Config = null;
            CurrentFrameIndex = 0;
            m_IsPackageGraphMounted = false;
            m_DeferredShutdownFailures.Clear();
            m_ShutdownFailure = null;
            m_CurrentPackageRegistration.Value = null;

            // Reopen registration only after the Resetting token is released and every
            // previous-cycle collection has been cleared.
            serviceRegistry.ClearAndReopenAfter(() =>
            {
                operationScope.Dispose();
                m_CurrentPhase = EnginePhase.None;
            });
        }
        catch
        {
            operationScope.Dispose();
            throw;
        }
    }

    public void RegisterSubsystem<T>(T subsystem) where T : class, IEngineSubsystem
    {
        using LifecycleOperationScope? registrationScope = BeginSubsystemRegistration();
        ArgumentNullException.ThrowIfNull(subsystem);
        PackageSubsystemRegistration? packageRegistration = m_CurrentPackageRegistration.Value;
        RegisterSubsystemCore(
            subsystem,
            packageId: packageRegistration?.PackageId ?? string.Empty,
            packageOrder: packageRegistration?.PackageOrder ?? int.MaxValue,
            declaredClassName: subsystem.GetType().FullName ?? subsystem.GetType().Name,
            initPhase: subsystem.InitPhase,
            priority: subsystem.Priority);
    }

    public void RegisterSubsystem<T>(T subsystem, string packageId, int packageOrder, string declaredClassName) where T : class, IEngineSubsystem
    {
        using LifecycleOperationScope? registrationScope = BeginSubsystemRegistration();
        ArgumentNullException.ThrowIfNull(subsystem);
        RegisterSubsystemCore(
            subsystem,
            packageId,
            packageOrder,
            declaredClassName,
            subsystem.InitPhase,
            subsystem.Priority);
    }

    public void RegisterSubsystem<T>(T subsystem, string packageId, int packageOrder, string declaredClassName, EnginePhase initPhase, int priority) where T : class, IEngineSubsystem
    {
        using LifecycleOperationScope? registrationScope = BeginSubsystemRegistration();
        ArgumentNullException.ThrowIfNull(subsystem);
        RegisterSubsystemCore(
            subsystem,
            packageId,
            packageOrder,
            declaredClassName,
            initPhase,
            priority);
    }

    private void RegisterSubsystemCore(
        IEngineSubsystem subsystem,
        string packageId,
        int packageOrder,
        string declaredClassName,
        EnginePhase initPhase,
        int priority)
    {
        if (m_CurrentPhase != EnginePhase.None)
            throw new InvalidOperationException("Cannot register subsystems after initialization has started");

        // B1: Check by concrete type to avoid ambiguity with interface-based checks
        if (m_Subsystems.Any(s => s.GetType() == subsystem.GetType()))
            throw new InvalidOperationException($"Subsystem of type {subsystem.GetType().Name} is already registered.");

        long registrationOrder = m_NextSubsystemRegistrationOrder;
        var registrationInfo = new SubsystemRegistrationInfo(
            packageId,
            packageOrder,
            declaredClassName,
            initPhase,
            priority,
            registrationOrder);
        m_SubsystemInfo.Add(subsystem, registrationInfo);
        try
        {
            m_Subsystems.Add(subsystem);
        }
        catch
        {
            m_SubsystemInfo.Remove(subsystem);
            throw;
        }

        m_NextSubsystemRegistrationOrder = registrationOrder + 1;
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

            if (m_InitializedSubsystems.Any(initialized => ReferenceEquals(initialized, subsystem)))
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
        if (m_InitializedSubsystems.Any(initialized => ReferenceEquals(initialized, subsystem)))
        {
            throw new InvalidOperationException(
                $"Cannot unregister started subsystem '{subsystem.GetType().FullName}'.");
        }

        m_SubsystemInfo.Remove(subsystem);
        int subsystemIndex = m_Subsystems.FindIndex(
            candidate => ReferenceEquals(candidate, subsystem));
        if (subsystemIndex < 0) return false;
        m_Subsystems.RemoveAt(subsystemIndex);
        return true;
    }

    public T? GetSubsystem<T>() where T : class, IEngineSubsystem
    {
        return m_Subsystems.OfType<T>().FirstOrDefault();
    }

    public void Initialize(EngineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Exception? initializationError = null;
        using LifecycleOperationScope operationScope = BeginLifecycleOperation(
            EngineLifecycleOperation.Initializing);
        ValidateInitializationAdmission(config);
        initializationError = TryInitializeAfterAdmission(config);

        if (initializationError != null)
        {
            operationScope.TransitionTo(EngineLifecycleOperation.ShuttingDown);
            ThrowAfterInitializationFailure(initializationError);
        }
    }

    private void ValidateInitializationAdmission(EngineConfig config)
    {
        if (m_CurrentPhase == EnginePhase.Shutdown)
        {
            throw new InvalidOperationException(
                "Engine kernel cannot initialize after shutdown; call Reset before initializing again.");
        }

        if (m_CurrentPhase != EnginePhase.None)
        {
            throw new InvalidOperationException(
                $"Engine kernel cannot initialize while the engine is in phase '{m_CurrentPhase}'.");
        }

        if (m_IsPackageGraphMounted && !ReferenceEquals(Config, config))
        {
            throw new InvalidOperationException(
                "The mounted package graph cannot be initialized with a different engine configuration.");
        }
    }

    private void ThrowAfterInitializationFailure(Exception initializationError)
    {
        try
        {
            ShutdownCore();
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
        throw new UnreachableException();
    }

    private Exception? TryInitializeAfterAdmission(EngineConfig config)
    {
        try
        {
            KernelLog.Info("[EngineKernel] Initializing...");
        }
        catch (Exception error)
        {
            m_IsRunning = false;
            return error;
        }

        if (!m_IsPackageGraphMounted)
        {
            MountPackageGraphCore(config);
        }

        try
        {
            InitializeMountedGraphCore();
            return null;
        }
        catch (Exception error)
        {
            m_IsRunning = false;
            return error;
        }
    }

    private void InitializeMountedGraphCore()
    {
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
        using IDisposable operationScope = BeginLifecycleOperation(
            EngineLifecycleOperation.MountingPackageGraph);
        MountPackageGraphCore(config);
    }

    private void MountPackageGraphCore(EngineConfig config)
    {
        if (m_CurrentPhase != EnginePhase.None)
        {
            throw new InvalidOperationException(
                $"Packages cannot be mounted while the engine is in phase '{m_CurrentPhase}'.");
        }

        if (m_IsPackageGraphMounted)
        {
            throw new InvalidOperationException("The package graph is already mounted.");
        }

        if (HasPackageRuntimeOwnership)
        {
            throw new InvalidOperationException(
                "The package graph cannot be mounted while package cleanup remains pending.");
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
            }

            m_IsPackageGraphMounted = true;
            if (config.PackageUrls.Count > 0)
            {
                KernelLog.Info("[EngineKernel] Package mount complete.");
            }
        }
        catch (Exception mountError)
        {
            var cleanupFailures = new List<Exception>();
            if (m_IsPackageGraphMounted && packageSubsystem != null)
            {
                try
                {
                    packageSubsystem.Shutdown();
                }
                catch (Exception cleanupError)
                {
                    AddFailure(cleanupFailures, cleanupError);
                }
            }

            bool hasRetainedOwnership = packageSubsystem?.HasOwnedRuntimeState == true;
            if (packageSubsystemWasCreated && packageSubsystem != null && !hasRetainedOwnership)
            {
                UnregisterSubsystem(packageSubsystem);
            }

            if (!hasRetainedOwnership)
            {
                Config = previousConfig;
            }

            m_IsPackageGraphMounted = false;
            if (cleanupFailures.Count > 0)
            {
                cleanupFailures.Insert(0, mountError);
                throw new AggregateException(
                    "Package graph mount failed and cleanup reported additional errors.",
                    cleanupFailures);
            }

            ExceptionDispatchInfo.Capture(mountError).Throw();
            throw new UnreachableException();
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
        using LifecycleOperationScope operationScope = BeginLifecycleOperation(
            EngineLifecycleOperation.AdmittingRun);
        EnsureInitializedForRun(operationScope);
        operationScope.TransitionTo(EngineLifecycleOperation.Running);

        while (m_IsRunning)
        {
            Time.Update();
            TickCore(Time.deltaTime);
        }

        return 0;
    }

    public int RunForFrames(uint frameCount)
    {
        using LifecycleOperationScope operationScope = BeginLifecycleOperation(
            EngineLifecycleOperation.AdmittingRun);
        EnsureInitializedForRun(operationScope);
        operationScope.TransitionTo(EngineLifecycleOperation.Running);

        KernelLog.Info($"[EngineKernel] Running bounded frame loop for {frameCount} frame(s).");
        for (uint i = 0; i < frameCount && m_IsRunning; i++)
        {
            Time.Update();
            TickCore(Time.deltaTime);
        }

        RequestShutdown();
        operationScope.TransitionTo(EngineLifecycleOperation.ShuttingDown);
        ShutdownCore();
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

        using LifecycleOperationScope operationScope = BeginLifecycleOperation(
            EngineLifecycleOperation.AdmittingRun);
        EnsureInitializedForRun(operationScope);
        operationScope.TransitionTo(EngineLifecycleOperation.Running);

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
                TickCore(Time.deltaTime);
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
        operationScope.TransitionTo(EngineLifecycleOperation.ShuttingDown);
        ShutdownCore();
        operationScope.Dispose();
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

    private void EnsureInitializedForRun(LifecycleOperationScope operationScope)
    {
        if (m_IsRunning) return;

        EngineConfig config = Config ?? new EngineConfig();
        ValidateInitializationAdmission(config);
        Exception? initializationError = TryInitializeAfterAdmission(config);

        if (initializationError != null)
        {
            operationScope.TransitionTo(EngineLifecycleOperation.ShuttingDown);
            ThrowAfterInitializationFailure(initializationError);
        }
    }

    /// <summary>
    /// Executes a single frame of the engine.
    /// Exposing this allows external runners (like the Editor) to drive the loop.
    /// </summary>
    public void Tick(float deltaTime)
    {
        EnterLifecycleOperation(EngineLifecycleOperation.Running);
        try
        {
            TickCore(deltaTime);
        }
        finally
        {
            EndLifecycleOperation(EngineLifecycleOperation.Running);
        }
    }

    private void TickCore(float deltaTime)
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
        using IDisposable operationScope = BeginLifecycleOperation(
            EngineLifecycleOperation.ShuttingDown);
        if (m_CurrentPhase == EnginePhase.Shutdown)
        {
            if (m_ShutdownFailure != null) throw m_ShutdownFailure;
            return;
        }

        ShutdownCore();
    }

    private void ShutdownCore()
    {
        var oneShotFailures = new List<Exception>();
        var packageFailures = new List<Exception>();
        if (Services is ServiceRegistry registrationRegistry)
        {
            registrationRegistry.CloseRegistration();
        }

        if (m_CurrentPhase != EnginePhase.PreShutdown)
        {
            m_CurrentPhase = EnginePhase.PreShutdown;
            TryPublishShutdownDiagnostic(
                () => KernelLog.Info("[EngineKernel] Shutting down..."),
                oneShotFailures);
        }

        m_IsRunning = false;

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

            TryPublishShutdownDiagnostic(
                () => KernelLog.Info(
                    $"  [Subsystem] Shutting down: {subsystem.GetType().Name}"),
                oneShotFailures);
            try
            {
                subsystem.Shutdown();
            }
            catch (Exception error)
            {
                AddFailure(oneShotFailures, error);
                TryPublishShutdownDiagnostic(
                    () => KernelLog.Error(
                        $"[EngineKernel] Subsystem '{subsystem.GetType().FullName}' shutdown failed: {error.Message}"),
                    oneShotFailures);
            }
            finally
            {
                m_InitializedSubsystems.RemoveAt(i);
            }
        }

        if (!RenderSurfaces.IsDisposed)
        {
            try
            {
                RenderSurfaces.Dispose();
            }
            catch (Exception error)
            {
                AddFailure(oneShotFailures, error);
                TryPublishShutdownDiagnostic(
                    () => KernelLog.Error(
                        $"[EngineKernel] Render-surface registry disposal failed: {error.Message}"),
                    oneShotFailures);
            }
        }

        if (packageSubsystem != null &&
            (m_IsPackageGraphMounted || packageSubsystemWasStarted || packageSubsystem.HasOwnedRuntimeState))
        {
            TryPublishShutdownDiagnostic(
                () => KernelLog.Info("  [Subsystem] Shutting down: PackageSubsystem"),
                oneShotFailures);
            try
            {
                packageSubsystem.Shutdown();
            }
            catch (Exception error)
            {
                AddFailure(packageFailures, error);
                TryPublishShutdownDiagnostic(
                    () => KernelLog.Error(
                        $"[EngineKernel] Package shutdown failed: {error.Message}"),
                    oneShotFailures);
            }
        }

        if (packageSubsystem?.HasOwnedRuntimeState == true)
        {
            TryPublishShutdownDiagnostic(
                () => KernelLog.Error(
                    $"[EngineKernel] Shutdown paused with retained package ownership."),
                oneShotFailures);
            m_DeferredShutdownFailures.AddRange(oneShotFailures);
            var attemptFailures = new List<Exception>(m_DeferredShutdownFailures);
            attemptFailures.AddRange(packageFailures);
            var incompleteFailure = new AggregateException(
                "Engine shutdown remains incomplete because package ownership is still pending.",
                attemptFailures);
            throw incompleteFailure;
        }

        m_DeferredShutdownFailures.AddRange(oneShotFailures);
        var failures = new List<Exception>(m_DeferredShutdownFailures);
        failures.AddRange(packageFailures);

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
                AddFailure(failures, error);
                TryPublishShutdownDiagnostic(
                    () => KernelLog.Error(
                        $"[EngineKernel] Kernel-owned service shutdown failed: {error.Message}"),
                    failures);
            }
        }

        m_IsPackageGraphMounted = false;
        m_CurrentPhase = EnginePhase.Shutdown;
        EngineShutdownOwnershipSnapshot ownership = GetShutdownOwnershipSnapshot();
        TryPublishShutdownDiagnostic(
            () => KernelLog.Info(
                $"[EngineKernel] Shutdown baseline: packages={ownership.PackageCount}, " +
                $"contexts={ownership.ManagedLoadContextCount}, " +
                $"nativeRuntimes={ownership.NativeRuntimeCount}, " +
                $"services={ownership.ServiceCount}, " +
                $"initializedSubsystems={ownership.InitializedSubsystemCount}, " +
                $"renderSurfaces={ownership.RenderSurfaceCount}, " +
                $"surfaceRegistryDisposed={ownership.RenderSurfaceRegistryDisposed}."),
            failures);
        if (!ownership.IsClean)
        {
            AddFailure(
                failures,
                new InvalidOperationException(
                    $"Engine shutdown left residual ownership: {ownership}."));
        }

        if (failures.Count > 0)
        {
            TryPublishShutdownDiagnostic(
                () => KernelLog.Error(
                    $"[EngineKernel] Shutdown completed with cleanup errors."),
                failures);
            m_ShutdownFailure = new AggregateException(
                "Engine shutdown completed with one or more cleanup errors.",
                failures);
            throw m_ShutdownFailure;
        }

        m_DeferredShutdownFailures.Clear();
        m_ShutdownFailure = null;
        var completionDiagnosticFailures = new List<Exception>();
        TryPublishShutdownDiagnostic(
            () => KernelLog.Info("[EngineKernel] Shutdown complete."),
            completionDiagnosticFailures);
        if (completionDiagnosticFailures.Count > 0)
        {
            m_ShutdownFailure = new AggregateException(
                "Engine shutdown completed but its final diagnostic failed.",
                completionDiagnosticFailures);
            throw m_ShutdownFailure;
        }
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

    private static void TryPublishShutdownDiagnostic(
        Action publish,
        List<Exception> failures)
    {
        try
        {
            publish();
        }
        catch (Exception error)
        {
            failures.Add(new InvalidOperationException(
                "Engine shutdown failed to publish a diagnostic.",
                error));
        }
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

    private LifecycleOperationScope BeginLifecycleOperation(EngineLifecycleOperation operation)
    {
        EnterLifecycleOperation(operation);
        return new LifecycleOperationScope(this, operation);
    }

    private void EnterLifecycleOperation(EngineLifecycleOperation operation)
    {
        int activeValue = Interlocked.CompareExchange(
            ref m_ActiveLifecycleOperation,
            (int)operation,
            (int)EngineLifecycleOperation.None);
        if (activeValue != (int)EngineLifecycleOperation.None)
        {
            var activeOperation = (EngineLifecycleOperation)activeValue;
            throw new InvalidOperationException(
                $"Engine kernel cannot {GetRequestedOperationName(operation)} while " +
                $"{GetActiveOperationName(activeOperation)} is in progress.");
        }

        Volatile.Write(ref m_LifecycleOwnerThreadId, Environment.CurrentManagedThreadId);
    }

    private void EndLifecycleOperation(EngineLifecycleOperation operation)
    {
        Volatile.Write(ref m_LifecycleOwnerThreadId, 0);
        int activeValue = Interlocked.CompareExchange(
            ref m_ActiveLifecycleOperation,
            (int)EngineLifecycleOperation.None,
            (int)operation);
        if (activeValue != (int)operation)
        {
            throw new InvalidOperationException(
                $"Engine kernel lifecycle operation '{operation}' lost its admission ownership.");
        }
    }

    private LifecycleOperationScope? BeginSubsystemRegistration()
    {
        var activeOperation = (EngineLifecycleOperation)Volatile.Read(
            ref m_ActiveLifecycleOperation);
        int ownerThreadId = Volatile.Read(ref m_LifecycleOwnerThreadId);
        bool isOwningSetupThread =
            ownerThreadId == Environment.CurrentManagedThreadId &&
            activeOperation is EngineLifecycleOperation.MountingPackageGraph or
                EngineLifecycleOperation.Initializing or
                EngineLifecycleOperation.AdmittingRun;
        if (isOwningSetupThread)
        {
            return null;
        }

        return BeginLifecycleOperation(EngineLifecycleOperation.RegisteringSubsystem);
    }

    private void TransitionLifecycleOperation(
        EngineLifecycleOperation expected,
        EngineLifecycleOperation replacement)
    {
        int activeValue = Interlocked.CompareExchange(
            ref m_ActiveLifecycleOperation,
            (int)replacement,
            (int)expected);
        if (activeValue != (int)expected)
        {
            throw new InvalidOperationException(
                $"Engine kernel lifecycle operation '{expected}' could not transition to " +
                $"'{replacement}'.");
        }
    }

    private static string GetRequestedOperationName(EngineLifecycleOperation operation) =>
        operation switch
        {
            EngineLifecycleOperation.MountingPackageGraph => "mount packages",
            EngineLifecycleOperation.Initializing => "initialize",
            EngineLifecycleOperation.ShuttingDown => "shutdown",
            EngineLifecycleOperation.Resetting => "reset",
            EngineLifecycleOperation.AdmittingRun => "run the engine",
            EngineLifecycleOperation.Running => "run the engine",
            EngineLifecycleOperation.RegisteringSubsystem => "register a subsystem",
            _ => "mutate lifecycle state"
        };

    private static string GetActiveOperationName(EngineLifecycleOperation operation) =>
        operation switch
        {
            EngineLifecycleOperation.MountingPackageGraph => "package graph mounting",
            EngineLifecycleOperation.Initializing => "initialization",
            EngineLifecycleOperation.ShuttingDown => "shutdown",
            EngineLifecycleOperation.Resetting => "reset",
            EngineLifecycleOperation.AdmittingRun => "engine run admission",
            EngineLifecycleOperation.Running => "engine run loop",
            EngineLifecycleOperation.RegisteringSubsystem => "subsystem registration",
            _ => "another lifecycle operation"
        };

    private sealed record SubsystemRegistrationInfo(
        string PackageId,
        int PackageOrder,
        string DeclaredClassName,
        EnginePhase InitPhase,
        int Priority,
        long RegistrationOrder);

    private sealed record PackageSubsystemRegistration(string PackageId, int PackageOrder);

    private enum EngineLifecycleOperation
    {
        None,
        MountingPackageGraph,
        Initializing,
        ShuttingDown,
        Resetting,
        AdmittingRun,
        Running,
        RegisteringSubsystem
    }

    private sealed class LifecycleOperationScope : IDisposable
    {
        private EngineKernel? m_Kernel;
        private EngineLifecycleOperation m_Operation;

        public LifecycleOperationScope(
            EngineKernel kernel,
            EngineLifecycleOperation operation)
        {
            m_Kernel = kernel;
            m_Operation = operation;
        }

        public void TransitionTo(EngineLifecycleOperation operation)
        {
            EngineKernel kernel = m_Kernel ?? throw new ObjectDisposedException(
                nameof(LifecycleOperationScope));
            kernel.TransitionLifecycleOperation(m_Operation, operation);
            m_Operation = operation;
        }

        public void Dispose()
        {
            EngineKernel? kernel = Interlocked.Exchange(ref m_Kernel, null);
            kernel?.EndLifecycleOperation(m_Operation);
        }
    }

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

    public bool IsStrictProgressFrom(EngineShutdownOwnershipSnapshot previous)
    {
        if (PackageCount > previous.PackageCount ||
            ManagedLoadContextCount > previous.ManagedLoadContextCount ||
            NativeRuntimeCount > previous.NativeRuntimeCount ||
            ServiceCount > previous.ServiceCount ||
            InitializedSubsystemCount > previous.InitializedSubsystemCount ||
            RenderSurfaceCount > previous.RenderSurfaceCount ||
            (previous.RenderSurfaceRegistryDisposed && !RenderSurfaceRegistryDisposed) ||
            (!previous.IsPackageGraphMounted && IsPackageGraphMounted))
        {
            return false;
        }

        return PackageCount < previous.PackageCount ||
            ManagedLoadContextCount < previous.ManagedLoadContextCount ||
            NativeRuntimeCount < previous.NativeRuntimeCount ||
            ServiceCount < previous.ServiceCount ||
            InitializedSubsystemCount < previous.InitializedSubsystemCount ||
            RenderSurfaceCount < previous.RenderSurfaceCount ||
            (!previous.RenderSurfaceRegistryDisposed && RenderSurfaceRegistryDisposed) ||
            (previous.IsPackageGraphMounted && !IsPackageGraphMounted);
    }
}


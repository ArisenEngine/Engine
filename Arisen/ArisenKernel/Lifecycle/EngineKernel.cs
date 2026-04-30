using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ArisenKernel.Services;
using ArisenKernel.Diagnostics;
using ArisenKernel.Contracts;
using ArisenKernel.Packages;

namespace ArisenKernel.Lifecycle;

public sealed class EngineKernel : IDisposable
{
    private static readonly Lazy<EngineKernel> s_Instance = new(() => new EngineKernel());
    public static EngineKernel Instance => s_Instance.Value;
    /// <summary>B13: Allows checking if the kernel has been instantiated without triggering Lazy creation.</summary>
    public static bool IsCreated => s_Instance.IsValueCreated;

    private readonly List<IEngineSubsystem> m_Subsystems = new();
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
        m_CurrentPhase = EnginePhase.None;
        m_IsRunning = false;
        Config = null;
        CurrentFrameIndex = 0;
        // B10: Reset ServiceRegistry to avoid duplicate registrations on re-init
        (Services as ServiceRegistry)?.Clear();
    }

    public void RegisterSubsystem<T>(T subsystem) where T : class, IEngineSubsystem
    {
        if (m_CurrentPhase != EnginePhase.None)
            throw new InvalidOperationException("Cannot register subsystems after initialization has started");

        // B1: Check by concrete type to avoid ambiguity with interface-based checks
        if (m_Subsystems.Any(s => s.GetType() == subsystem.GetType()))
            throw new InvalidOperationException($"Subsystem of type {subsystem.GetType().Name} is already registered.");

        m_Subsystems.Add(subsystem);
    }

    public T? GetSubsystem<T>() where T : class, IEngineSubsystem
    {
        return m_Subsystems.OfType<T>().FirstOrDefault();
    }

    public void Initialize(EngineConfig config)
    {
        Config = config;
        KernelLog.Info("[EngineKernel] Initializing...");

        // 1. Mount Packages (Moved from Host for single-source-of-truth)
        if (config.PackageUrls.Count > 0)
        {
            LoadPackages(config.PackageUrls);
        }

        // 2. Sort subsystems by priority
        m_Subsystems.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        TransitionTo(EnginePhase.PreInit);
        TransitionTo(EnginePhase.Init);
        TransitionTo(EnginePhase.PostInit);

        m_IsRunning = true;
        m_CurrentPhase = EnginePhase.Running;
        KernelLog.Info("[EngineKernel] Engine is now Running.");
    }

    private void LoadPackages(List<string> packageUrls)
    {
        KernelLog.Info("[EngineKernel] Loading Packages...");
        
        var packageSubsystem = GetSubsystem<PackageSubsystem>();
        if (packageSubsystem == null)
        {
            packageSubsystem = new PackageSubsystem();
            m_Subsystems.Add(packageSubsystem);
        }

        foreach (var pUrl in packageUrls)
        {
            string pJsonPath = Path.Combine(pUrl, "package.json");
            if (!File.Exists(pJsonPath)) continue;

            try
            {
                var pDoc = JsonDocument.Parse(File.ReadAllText(pJsonPath));
                var root = pDoc.RootElement;
                string id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : root.GetProperty("name").GetString();
                
                if (root.TryGetProperty("entry", out var entryObj) && entryObj.ValueKind == JsonValueKind.Object)
                {
                    string asmName = entryObj.TryGetProperty("assembly", out var asmProp) ? asmProp.GetString() ?? "" : "";
                    string entryClassName = entryObj.TryGetProperty("class", out var clsProp) ? clsProp.GetString() ?? "" : "";
                    
                    if (!string.IsNullOrEmpty(asmName))
                    {
                        // Prefer central bin folder (where Host is)
                        string binDir = AppContext.BaseDirectory;
                        string fullPath = Path.Combine(binDir, asmName);
                        
                        if (!File.Exists(fullPath))
                            fullPath = Path.Combine(pUrl, "Managed", asmName);

                        if (File.Exists(fullPath))
                        {
                            Assembly asm = Assembly.LoadFrom(fullPath);
                            object? entryInstance = null;
                            if (!string.IsNullOrEmpty(entryClassName))
                            {
                                Type t = asm.GetType(entryClassName);
                                if (t != null)
                                {
                                    entryInstance = Activator.CreateInstance(t);
                                    var onLoadMethod = t.GetMethod("OnLoad");
                                    if (onLoadMethod != null) onLoadMethod.Invoke(entryInstance, new object[] { Services });
                                }
                            }

                            var pkgInfo = new ArisenPackageInfo
                            {
                                Id = id,
                                Name = root.TryGetProperty("name", out var n) ? n.GetString() ?? id : id,
                                Version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "1.0.0" : "1.0.0",
                                Type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "managed" : "managed",
                                RootPath = pUrl,
                                Assembly = asm,
                                EntryInstance = entryInstance
                            };
                            packageSubsystem.RegisterLoadedPackage(pkgInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                KernelLog.Error($"[EngineKernel] Failed to load package at {pUrl}: {ex.Message}");
            }
        }
        KernelLog.Info("[EngineKernel] Package Loading Complete.");
    }

    private void TransitionTo(EnginePhase phase)
    {
        m_CurrentPhase = phase;
        KernelLog.Info($"[EngineKernel] Transitioning to phase: {phase}");

        foreach (var subsystem in m_Subsystems)
        {
            if (subsystem.InitPhase == phase)
            {
                KernelLog.Info($"  [Subsystem] Initializing: {subsystem.GetType().Name}");
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

        // P3: Shutdown in reverse priority order without allocating a new list
        for (int i = m_Subsystems.Count - 1; i >= 0; i--)
        {
            KernelLog.Info($"  [Subsystem] Shutting down: {m_Subsystems[i].GetType().Name}");
            m_Subsystems[i].Shutdown();
        }

        m_CurrentPhase = EnginePhase.Shutdown;
        KernelLog.Info("[EngineKernel] Shutdown complete.");
    }

    public void Dispose()
    {
        Shutdown();
    }
}

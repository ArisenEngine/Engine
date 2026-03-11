using System;
using System.Collections.Generic;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Platform;
using ArisenEngine.Rendering;
using ArisenEngine.Core.Packages;

namespace ArisenEngine.Core.Lifecycle;

public enum RuntimePlatform
{
    Unknow,
    Windows,
    Linux,
    MacOS,
    Android,
    IOS,
    Browser,
    XBox,
    PS5
}

public class ArisenApplication
{
    public static Action AllSurfacesDestroyed;

    private static Dictionary<IntPtr, SurfaceInfo> m_RenderSurfaces = new Dictionary<IntPtr, SurfaceInfo>();

    static ArisenApplication()
    {
        AllSurfacesDestroyed += OnSurfacesAllClosed;
    }

    private static void OnSurfacesAllClosed()
    {
        // Handle all surfaces closed
    }

    #region Internal

    public static string s_StartupPath = string.Empty;
    public static string s_DataPath = string.Empty;
    public static string s_ProjectRoot = string.Empty;
    public static string s_ProjectName = string.Empty;
    public static bool s_IsRunning = false;
    public static bool s_IsInEditor = false;
    public static RuntimePlatform s_Platform = RuntimePlatform.Windows;

    internal static IEnumerable<SurfaceInfo> GetActiveSurfaces() => m_RenderSurfaces.Values;

    #endregion

    #region Public

    public static int Run(int width, int height, string name = "")
    {
        RegisterSurface(IntPtr.Zero, name, SurfaceType.GameView, width, height);
        return Run(name);
    }

    public static bool InitializeLogging(bool bindCallback = false)
    {
        return Logger.Initialize(bindCallback);
    }

    public static bool InitializeEngine(EngineConfig config)
    {
        if (!Logger.IsInitialized)
        {
            InitializeLogging(false);
        }

        // Register early subsystems
        EngineKernel.Instance.RegisterSubsystem(new PlatformSubsystem());
        EngineKernel.Instance.RegisterSubsystem(new ProjectSubsystem());
        EngineKernel.Instance.RegisterSubsystem(new PackageSubsystem());
        EngineKernel.Instance.RegisterSubsystem(new RenderSubsystem());

        // Initialize RHI and native core
        if (!NativeRuntime.Initialize())
        {
            return false;
        }

        // Use PackageSubsystem to resolve the default render pipeline
        var packageSubsystem = EngineKernel.Instance.GetSubsystem<PackageSubsystem>();
        var defaultForwardRP = packageSubsystem?.GetPackageEntry<RenderPipelineAsset>("com.arisen.builtin.forward-rp");

        if (defaultForwardRP != null)
        {
            Graphics.SetCurrentRenderPipeline(defaultForwardRP);
            Logger.Log("[ArisenApplication] Successfully loaded Fallback ForwardRP from PackageSubsystem");
        }
        else
        {
            Logger.Warning("[ArisenApplication] Failed to find default RenderPipeline package. Rendering might be disabled.");
        }

        EngineKernel.Instance.Initialize(config);
        return true;
    }

    public static int Run(string name = "")
    {
        var config = new EngineConfig
        {
            AppName = name
        };

        if (!InitializeEngine(config))
        {
            return -1;
        }

        var errorCode = 0;
        try
        {
            s_IsRunning = true;
            errorCode = EngineKernel.Instance.Run();
        }
        catch (Exception e)
        {
            // Logger.Error(e.Message);
            errorCode = -1;
        }
        finally
        {
            s_IsRunning = false;
            ShutdownEngine();
        }

        return errorCode;
    }

    public static void RegisterSurface(IntPtr host, string name, SurfaceType surfaceType, int width = 0, int height = 0)
    {
        using var _ = Profiler.Zone("ArisenApplication.RegisterSurface");
        if (!m_RenderSurfaces.ContainsKey(host))
        {
            var surface = new RenderSurface(host, name, width, height);
            m_RenderSurfaces.Add(host, new SurfaceInfo()
            {
                Name = name,
                Parent = host,
                Surface = surface,
                SurfaceType = surfaceType
            });

            return;
        }

        throw new Exception($"Same host : {host} already added");
    }

    public static void ResizeSurface(IntPtr host, int width, int height)
    {
        if (m_RenderSurfaces.TryGetValue(host, out var surface))
        {
            // NativeHAL.RenderWindowAPI.ResizeRenderSurface(surface.Surface.SurfaceId, (uint)width, (uint)height);
        }
    }

    public static void UnregisterSurface(IntPtr host)
    {
        if (m_RenderSurfaces.TryGetValue(host, out var surfaceInfo))
        {
            surfaceInfo.Surface.DisposeSurface();
            m_RenderSurfaces.Remove(host);

            return;
        }

        throw new Exception($"Surface of host {host} not exists");
    }

    private static bool s_HasShutdown = false;

    public static void RequestExit()
    {
        EngineKernel.Instance.RequestShutdown();
    }

    public static void ShutdownEngine()
    {
        if (s_HasShutdown) return;
        s_HasShutdown = true;

        EngineKernel.Instance.Dispose();
        NativeRuntime.Shutdown();
        Logger.Dispose();
    }

    #endregion
}
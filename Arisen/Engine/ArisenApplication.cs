using System;
using System.Collections.Generic;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Platform;
using ArisenEngine.Rendering;
using ArisenKernel.Packages;
using ArisenKernel.Lifecycle;

namespace ArisenEngine.Core.Lifecycle;

public class ArisenApplication
{
    public static bool s_IsRunning = false;

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
        EngineKernel.Instance.RegisterSubsystem(new EnvironmentSubsystem());
        EngineKernel.Instance.RegisterSubsystem(new PlatformSubsystem());
        EngineKernel.Instance.RegisterSubsystem(new ProjectSubsystem());
        EngineKernel.Instance.RegisterSubsystem(new PackageSubsystem());
        EngineKernel.Instance.RegisterSubsystem(new SceneSubsystem());
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
        EngineKernel.Instance.GetSubsystem<RenderSubsystem>()?.RegisterSurface(host, name, surfaceType, width, height);
    }

    public static void ResizeSurface(IntPtr host, int width, int height)
    {
        EngineKernel.Instance.GetSubsystem<RenderSubsystem>()?.ResizeSurface(host, width, height);
    }

    public static void UnregisterSurface(IntPtr host)
    {
        EngineKernel.Instance.GetSubsystem<RenderSubsystem>()?.UnregisterSurface(host);
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

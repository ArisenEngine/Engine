using System;
using System.Collections.Generic;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Platform;
using ArisenEngine.Rendering;

namespace ArisenEngine.Core.Lifecycle;

internal static class EngineInstance
{
    internal static Action AllSurfacesDestroyed;

    private static Dictionary<IntPtr, SurfaceInfo> m_RenderSurfaces = new Dictionary<IntPtr, SurfaceInfo>();

    internal static void RegisterSurface(IntPtr host, string name, SurfaceType surfaceType, int width = 0,
        int height = 0)
    {
        using var _ = Profiler.Zone("EngineInstance.RegisterSurface");
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

    internal static void ResizeSurface(IntPtr host, int width, int height)
    {
        if (m_RenderSurfaces.TryGetValue(host, out var surface))
        {
            // NativeHAL.RenderWindowAPI.ResizeRenderSurface(surface.Surface.SurfaceId, (uint)width, (uint)height);
        }
    }

    internal static void UnregisterSurface(IntPtr host)
    {
        if (m_RenderSurfaces.TryGetValue(host, out var surfaceInfo))
        {
            surfaceInfo.Surface.DisposeSurface();
            m_RenderSurfaces.Remove(host);

            return;
        }

        throw new Exception($"Surface of host {host} not exists");
    }

    internal static int Run(string instanceName = "")
    {
        var config = new EngineConfig
        {
            AppName = instanceName
        };

        // Register early subsystems
        EngineKernel.Instance.RegisterSubsystem(new PlatformSubsystem());
        EngineKernel.Instance.RegisterSubsystem(new RenderSubsystem());

        // Initialize Native Logger/Core here if needed
        // Bootstrap.Initialize(); 

        var errorCode = 0;
        try
        {
            EngineKernel.Instance.Initialize(config);
            errorCode = EngineKernel.Instance.Run();
        }
        catch (Exception e)
        {
            // Logger.Error(e.Message);
            errorCode = -1;
        }
        finally
        {
            Dispose();
        }

        return errorCode;
    }

    internal static void End()
    {
        EngineKernel.Instance.RequestShutdown();
    }

    private static void Dispose()
    {
        EngineKernel.Instance.Dispose();
        Bootstrap.Shutdown();
    }
}
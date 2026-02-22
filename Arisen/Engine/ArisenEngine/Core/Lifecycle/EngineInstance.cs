using System.Diagnostics;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Platform;
using ArisenEngine.Rendering;
using Arisen.Native.Core;

namespace ArisenEngine.Core.Lifecycle;

internal static class EngineInstance
{
    internal static Action AllSurfacesDestroyed;
    
    private static Dictionary<IntPtr, SurfaceInfo> m_RenderSurfaces = new Dictionary<IntPtr, SurfaceInfo>();
    private static string m_Name;
    private static bool m_IsRunning;

    // Platform specific message handler
    private static IMessageHandler m_MessageHandler;

    private static bool Initialize()
    {
        // EngineInit.Initialize() covers logger and other core native systems
        bool isInitializeDone = Bootstrap.Initialize();

        if (isInitializeDone)
        {
            // TODO: Move to Platform factory
            switch (ArisenApplication.s_Platform)
            {
                case RuntimePlatform.Windows:
                    // This will need adjustment after HAL move
                    // m_MessageHandler = new WindowsMessageHandle();
                    break;
                default:
                    isInitializeDone = false;
                    // Arisen.Native.Diagnostics.Logger could be used here if needed
                    break;
            }
        }
        
        return isInitializeDone;
    }
    
    internal static void RegisterSurface(IntPtr host, string name, SurfaceType surfaceType, int width = 0, int height = 0)
    {
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
            NativeHAL.RenderWindowAPI.ResizeRenderSurface(surface.Surface.SurfaceId, (uint)width, (uint)height);
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
        if (!Initialize())
        {
            throw new Exception($"Instance initialize failed.");
        }

        if (m_IsRunning)
        {
            throw new Exception($"Game instance: {m_Name} is already running.");
        }

        m_Name = instanceName;
        m_IsRunning = true;

        var errorCode = 0;
        try
        {
            while (m_IsRunning)
            {
                if (m_MessageHandler != null)
                {
                    while (m_MessageHandler.NextFrame())
                    {
                        RenderPipelineManager.DoRenderLoop(Graphics.currentRenderPipelineAsset);
                    }
                }
                else
                {
                    // Fallback or headless mode
                    break;
                }
            }
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
        m_IsRunning = false;
    }

    private static void Dispose()
    {
        Bootstrap.Shutdown();
    }
}

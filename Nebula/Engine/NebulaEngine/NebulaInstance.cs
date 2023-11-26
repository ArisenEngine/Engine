using System.Diagnostics;
using NebulaEngine.Debugger;
using NebulaEngine.Rendering;
using NebulaEngine.Platforms;

namespace NebulaEngine;

internal static class NebulaInstance
{
    internal static Action AllSurfacesDestroyed;
    /// <summary>
    /// Surface is bound to window
    /// </summary>
    private static Dictionary<IntPtr, SurfaceInfo> m_RenderSurfaces = new Dictionary<IntPtr, SurfaceInfo>();
    private static string m_Name;

    private static bool m_IsRunning;

    private static MessageHandler m_MessageHandler;
    private static bool Initialize()
    {
        bool isInitializeDone = true;
        switch (NebulaApplication.s_Platform)
        {
            case RuntimePlatform.Windows:
                m_MessageHandler = new WindowsMessageHandle();
                break;
            default:
                isInitializeDone = false;
                throw new Exception($"Unsupported platform type:{NebulaApplication.s_Platform}");
        }

        isInitializeDone &= Logger.Initialize();
        
        return isInitializeDone;
    }
    
    internal static void RegisterSurface(IntPtr host, string name, SurfaceType surfaceType, int width = 0, int height = 0)
    {
        if (!m_RenderSurfaces.ContainsKey(host))
        {
            m_RenderSurfaces.Add(host, new SurfaceInfo()
            {
                Name = name,
                Parent = host,
                Surface = new RenderSurface(host, name, width, height),
                SurfaceType = surfaceType
            });
            
            return;
        }

        throw new Exception($"Same host : {host} already added");
    }

    internal static void UnregisterSurface(IntPtr host)
    {
        if (m_RenderSurfaces.TryGetValue(host, out var surfaceInfo))
        {
            surfaceInfo.Surface.Dispose();
            m_RenderSurfaces.Remove(host);
            
            
            return;
        }
        
        throw new Exception($"Surface of host {host} not exists");
    }

    internal static void UnregisterSurface()
    {
        UnregisterSurface(IntPtr.Zero);
    }

    internal static void RegisterSurface(string name, int width = 0, int height = 0)
    {
        RegisterSurface(IntPtr.Zero, name, SurfaceType.GameView, width, height);
    }
    
    internal static IntPtr GetNativeHandle(IntPtr host)
    {
        if (m_RenderSurfaces.TryGetValue(host, out var surfaceInfo))
        {
            return surfaceInfo.Surface.Handle;
        }
        
        return IntPtr.Zero;
    }
    
    private static bool IsValid()
    {
        return m_RenderSurfaces.Count > 0;
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
                while (m_MessageHandler.NextFrame())
                {
                    // run loop

                    RenderPipelineManager.DoRenderLoop(Graphics.currentRenderPipelineAsset);

                }


            }
        }
        catch (Exception e)
        {
            Logger.Error(e.Message);
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
        
        Logger.Log($"{m_Name} Dispose");
        Logger.Dispose();
        
    }
}
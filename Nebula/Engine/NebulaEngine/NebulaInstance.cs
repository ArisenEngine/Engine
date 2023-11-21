using System.Diagnostics;
using NebulaEngine.Debugger;
using NebulaEngine.Graphics;
using NebulaEngine.Platforms;

namespace NebulaEngine;

internal static class NebulaInstance
{
    /// <summary>
    /// Surface is bound to window
    /// </summary>
    private static Dictionary<IntPtr, SurfaceInfo> m_RenderSurfaces = new Dictionary<IntPtr, SurfaceInfo>();
    private static string m_Name;

    private static bool m_IsRunning;

    private static MessageHandler m_MessageHandler;
    private static bool Initialize()
    {
        switch (GameApplication.platform)
        {
            case RuntimePlatform.Windows:
                m_MessageHandler = new WindowsMessageHandle();
                return true;
        }
        
        throw new Exception($"Unsupported platform type:{GameApplication.platform}");
    }
    
    internal static void SetInstanceName(string name)
    {
        m_Name = name;
    }
    
    internal static void RegisterSurface(IntPtr host, string name, int width, int height, SurfaceType surfaceType)
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

    internal static void RegisterSurface(string name, int width, int height)
    {
        RegisterSurface(IntPtr.Zero, name, width, height, SurfaceType.GameView);
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

   
    internal static void Run()
    {
        if (!Initialize())
        {
            throw new Exception($"Instance initialize failed.");
        }

        if (m_IsRunning)
        {
            throw new Exception($"Game instance: {m_Name} is already running.");
        }
        
        m_IsRunning = true;
        
        while (m_IsRunning)
        {
            while (m_MessageHandler.NextFrame())
            {
                // run loop
                Logger.Log($"{m_Name} update");
                
            }
            
            
        }
        
    }

    internal static void End()
    {
        m_IsRunning = false;
    }
    internal static void Dispose()
    {
        
        
    }
}
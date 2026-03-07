using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Rendering;

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
    static ArisenApplication()
    {
        EngineInstance.AllSurfacesDestroyed += OnSurfacesAllClosed;
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

    #endregion

    #region Public

    public static int Run(int width, int height, string name = "")
    {
        EngineInstance.RegisterSurface(IntPtr.Zero, name, SurfaceType.GameView, width, height);
        return EngineInstance.Run(name);
    }

    public static int Run(string name = "")
    {
        EngineInstance.RegisterSurface(IntPtr.Zero, name, SurfaceType.GameView);
        return EngineInstance.Run(name);
    }

    public static void RegisterSurface(IntPtr host, string name, SurfaceType surfaceType, int width = 0, int height = 0)
    {
        EngineInstance.RegisterSurface(host, name, surfaceType, width, height);
    }

    public static void UnregisterSurface(IntPtr host)
    {
        EngineInstance.UnregisterSurface(host);
    }

    public static void Exit()
    {
        EngineInstance.End();
    }

    #endregion
}
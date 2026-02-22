using ArisenEngine.Core.Lifecycle;

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
    
    internal static string s_StartupPath = string.Empty;
    internal static string s_DataPath = string.Empty;
    internal static string s_ProjectRoot = string.Empty;
    internal static string s_ProjectName = string.Empty;
    internal static bool s_IsRunning = false;
    internal static bool s_IsInEditor = false;
    internal static RuntimePlatform s_Platform = RuntimePlatform.Windows;

    #endregion

    #region Public 
    
    public static int Run(int width, int height, string name = "")
    {
        EngineInstance.RegisterSurface(name, width, height);
        return EngineInstance.Run(name);
    }
    
    public static int Run(string name = "")
    {
        EngineInstance.RegisterSurface(name);
        return EngineInstance.Run(name);
    }

    public static void Exit()
    {
        EngineInstance.End();
    }

    #endregion
}

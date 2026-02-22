using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Platform;
using ArisenEngine.Platform.Desktop;

namespace ArisenEngine.Rendering;

public enum SurfaceType
{
    GameView = 0,
    SceneView,
    AssetView,
    Count
}
    
public struct SurfaceInfo
{
    public string Name;
    public IntPtr Parent;
    public RenderSurface Surface;
    public SurfaceType SurfaceType;
}

public class RenderSurface : IRenderSurface
{
    internal List<RenderSurface> Surfaces = new List<RenderSurface>();
    private IntPtr m_Host;
    private uint m_SurfaceId;
    private IntPtr m_Handle;
    private string m_Name = "RenderSurface";

    private WindowProcessor m_Processor;
    private bool m_Hosted = true;

    public IntPtr Handle => m_Handle;
    public uint SurfaceId => m_SurfaceId;

    public RenderSurface(IntPtr host, string name, int width = 0, int height = 0, bool hosted = true)
    {
        m_Name = name;
        m_Hosted = hosted;
        bool isFullScreen = (width == 0 || height == 0) && host == IntPtr.Zero;
        if (Initialize())
        {
            m_Host = host;
            // NOTE: RenderWindowAPI might be missing in AutoBinding, if so, this will error.
            // We should ensure it's generated or kept manually if needed.
            // For now, assuming it's in ArisenBinding.Arisen.HAL (or NativeHAL as per config)
            /*
            m_SurfaceId = isFullScreen
                ? ArisenBinding.Arisen.HAL.RenderWindowAPI.CreateFullScreenRenderSurface(host, m_Processor.ProcPtr)
                : ArisenBinding.Arisen.HAL.RenderWindowAPI.CreateRenderWindow(host, m_Processor.ProcPtr, width, height);
            m_Handle = ArisenBinding.Arisen.HAL.RenderWindowAPI.GetWindowHandle(m_SurfaceId);
            RenderWindowAPI.SetWindowResizeCallback(m_SurfaceId, m_Processor.ResizeCallbackPtr);
            */
            
            Surfaces.Add(this);
        }
        else
        {
            throw new Exception("Render Surface init failed.");
        }
    }
    
    private bool Initialize()
    {
        switch (ArisenApplication.s_Platform)
        {
            case RuntimePlatform.Windows:
                m_Processor = new WindowsProcHandler(this);
                return true;
        }

        throw new Exception($"Unsupported platform type: {ArisenApplication.s_Platform}");
    }
    
    public bool IsValid() => ((m_Hosted &&  m_Host != IntPtr.Zero) || !m_Hosted) && m_Handle != IntPtr.Zero;

    public void DisposeSurface()
    {
        // ArisenBinding.Arisen.HAL.RenderWindowAPI.RemoveRenderSurface(m_SurfaceId);
        Surfaces.Remove(this);
        if (Surfaces.Count <= 0)
        {
            // ArisenEngine.Core.Lifecycle.EngineInstance.AllSurfacesDestroyed?.Invoke();
        }
    }

    public IntPtr GetHandle() => m_Handle;
    public void OnCreate() { }
    public void OnResizing() => Console.WriteLine($"RenderSurface : {m_Name} resizing.");
    public void OnResized()
    {
        Console.WriteLine($"RenderSurface : {m_Name} resized.");
        Logger.Log($"RenderSurface : {m_Name} resized.");
    }
    public void OnDestroy() { }
}
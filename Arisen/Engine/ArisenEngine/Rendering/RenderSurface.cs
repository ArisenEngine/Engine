using ArisenEngine.Debug;
using ArisenEngine.Platforms;

namespace ArisenEngine.Rendering;

internal enum SurfaceType
{
    GameView = 0,
    // TODO: add Editor marco 
    // #if ARISEN_EDITOR
    SceneView,
    AssetView,
    // #endif
    Count
}
    
internal struct SurfaceInfo
{
    public string Name;
    public IntPtr Parent;
    public RenderSurface Surface;
    public SurfaceType SurfaceType;
}


internal class RenderSurface : IRenderSurface
{
    internal List<RenderSurface> Surfaces = new List<RenderSurface>();
    private IntPtr m_Host;
    private uint m_SurfaceId;
    private IntPtr m_Handle;
    private string m_Name = "RenderSurface";

    private WindowProcessor m_Processor;

    private bool m_Hosted = true;

    public IntPtr Handle => m_Handle;

    public RenderSurface(IntPtr host, string name, int width = 0, int height = 0, bool hosted = true)
    {
        m_Name = name;
        m_Hosted = hosted;
        bool isFullScreen = (width == 0 || height == 0) && host == IntPtr.Zero;
        if (Initialize())
        {
            m_Host = host;
            System.Diagnostics.Debug.Assert(m_Processor != null, nameof(m_Processor) + " != null");
            m_SurfaceId = isFullScreen
                ? RenderWindowAPI.CreateFullScreenRenderSurface(host, m_Processor.ProcPtr)
                : RenderWindowAPI.CreateRenderWindow(host, m_Processor.ProcPtr, width, height);
            m_Handle = RenderWindowAPI.GetWindowHandle(m_SurfaceId);
            Surfaces.Add(this);
            
            RenderWindowAPI.SetWindowResizeCallback(m_SurfaceId, m_Processor.ResizeCallbackPtr);
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
    
    public bool IsValid()
    {
        return ((m_Hosted &&  m_Host != IntPtr.Zero) || !m_Hosted) && m_Handle != IntPtr.Zero;
    }

    public void DisposeSurface()
    {
        RenderWindowAPI.RemoveRenderSurface(m_SurfaceId);
        Surfaces.Remove(this);
        if (Surfaces.Count <= 0)
        {
            ArisenInstance.AllSurfacesDestroyed?.Invoke();
        }
    }

    public IntPtr GetHandle()
    {
        return m_Handle;
    }

    public void OnCreate()
    {
        throw new NotImplementedException();
    }

    public void OnResizing()
    {
        
    }

    public void OnResized()
    {
        Console.WriteLine($"RenderSurface : {m_Name} resized.");
        Logger.Log($"RenderSurface : {m_Name} resized.");
    }

   

    public void OnDestroy()
    {
        
    }
}
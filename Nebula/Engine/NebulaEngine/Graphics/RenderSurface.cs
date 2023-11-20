using NebulaEngine.API;
using NebulaEngine.Debugger;
using NebulaEngine.Platforms;

namespace NebulaEngine.Graphics;

internal class RenderSurface : IDisposable, IRenderSurface
{
    internal List<RenderSurface> Surfaces = new List<RenderSurface>();
    private IntPtr m_Host;
    private int m_SurfaceId;
    private IntPtr m_Handle;
    private string m_Name = "RenderSurface";

    private MessageHandler m_MessageHandler;

    private bool m_Hosted = true;

    public IntPtr Handle => m_Handle;

    public RenderSurface(IntPtr host, string name, int width, int height, bool hosted = true)
    {
        m_Name = name;
        m_Hosted = hosted;
        if (Initialize())
        {
            m_Host = host;
            m_SurfaceId = PlatformAPI.CreateRenderSurface(host, m_MessageHandler.CallbackPtr, width, height);
            m_Handle = PlatformAPI.GetWindowHandle(m_SurfaceId);
            Surfaces.Add(this);
        }
        else
        {
            throw new Exception("Render Surface init failed.");
        }
    }

    public MessageHandler MessageHandler => m_MessageHandler;
    private bool Initialize()
    {
        switch (GameApplication.platform)
        {
            case RuntimePlatform.Windows:
                m_MessageHandler = new WindowsMessageHandle(this);
                return true;
        }

        throw new Exception($"Unsupported platform type: {GameApplication.platform}");
    }
    
    public bool IsValid()
    {
        return ((m_Hosted &&  m_Host != IntPtr.Zero) || !m_Hosted) && m_Handle != IntPtr.Zero;
    }

    public void Dispose()
    {
        PlatformAPI.RemoveRenderSurface(m_SurfaceId);
        Surfaces.Remove(this);
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
        Logger.Log($"RenderSurface : {m_Name} resized.");
    }

   

    public void OnDestroy()
    {
        throw new NotImplementedException();
    }
}
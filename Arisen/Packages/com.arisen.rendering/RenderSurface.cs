
using ArisenKernel.Contracts;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Automation;
using ArisenEngine.Rendering;
using ArisenKernel.Lifecycle;
using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.Diagnostics;

namespace ArisenEngine.Rendering;



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
            m_SurfaceId = isFullScreen
                ? NativeHAL.RenderWindowAPI.CreateFullScreenRenderSurface(host, m_Processor.ProcPtr)
                : NativeHAL.RenderWindowAPI.CreateRenderWindow(host, m_Processor.ProcPtr, width, height);

            m_Handle = NativeHAL.RenderWindowAPI.GetWindowHandle(m_SurfaceId);
            NativeHAL.RenderWindowAPI.SetWindowResizeCallback(m_SurfaceId, m_Processor.ResizeCallbackPtr);

            // TODO: Per-surface device creation ï¿?CreateLogicDevice and GetLogicalDevice
            // are pure virtual methods in C++ RHIInstance that CppSharp cannot bind.
            // Device creation is handled by Graphics.Initialize() ï¿?InitLogicDevices() instead.
            // var instance = RHIGraphics.Instance;
            // if (instance != null)
            // {
            //     instance.CreateLogicDevice(m_SurfaceId);
            //     var device = instance.GetLogicalDevice(m_SurfaceId);
            //     if (device != null)
            //         RHIGraphics.SetLogicDevice(device);
            // }

            Surfaces.Add(this);
        }
        else
        {
            throw new Exception("Render Surface init failed.");
        }
    }

    private bool Initialize()
    {
        if (EngineKernel.Instance.Services.TryGetService<IWindowProvider>(out var provider))
        {
            m_Processor = provider.CreateWindowProcessor(this);
            return true;
        }

        throw new System.Exception($"No IWindowProvider registered! Cannot create RenderSurface for {m_Name}");
    }

    public bool IsValid() => ((m_Hosted && m_Host != IntPtr.Zero) || !m_Hosted) && m_Handle != IntPtr.Zero;

    public void DisposeSurface()
    {
        NativeHAL.RenderWindowAPI.RemoveRenderSurface(m_SurfaceId);
        Surfaces.Remove(this);
        if (Surfaces.Count <= 0)
        {
            // ArisenEngine.Core.Lifecycle.ArisenApplication.AllSurfacesDestroyed?.Invoke();
        }
    }

    public IntPtr GetHandle() => m_Handle;

    public void OnCreate()
    {
    }

    public void OnResizing() => Console.WriteLine($"RenderSurface : {m_Name} resizing.");

    public void OnResized()
    {
        Console.WriteLine($"RenderSurface : {m_Name} resized.");
        Logger.Log($"RenderSurface : {m_Name} resized.");
    }

    public void OnDestroy()
    {
    }
}


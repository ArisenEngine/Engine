using System;
using System.Threading.Tasks;

namespace ArisenKernel.Contracts;

public enum SurfaceType
{
    Window,
    SharedHandle,
    SceneView,
    GameView
}

public readonly record struct RenderSurfaceRegistration(IntPtr Host, ulong Generation)
{
    public bool IsValid => Host != IntPtr.Zero && Generation != 0;
}

public readonly struct SurfaceInfo
{
    public SurfaceInfo(
        RenderSurfaceRegistration registration,
        string name,
        IRenderSurface surface,
        SurfaceType surfaceType)
    {
        Registration = registration;
        Name = name;
        Surface = surface;
        SurfaceType = surfaceType;
    }

    public RenderSurfaceRegistration Registration { get; }
    public string Name { get; }
    public IntPtr Parent => Registration.Host;
    public IRenderSurface Surface { get; }
    public SurfaceType SurfaceType { get; }
    public uint SurfaceId => Surface.SurfaceId;
}

public struct RenderOutputInfo
{
    public ulong Ticket;
    public uint FrameIndex;
    public uint ResizeGeneration;
    public IntPtr SharedHandle;
    public ulong MemorySize;
    public IntPtr WaitSemaphoreHandle;
    public IntPtr SignalSemaphoreHandle;
    public uint Width;
    public uint Height;
}

public interface IRenderSurface : IDisposable
{
    string Name { get; }
    IntPtr Handle { get; }
    uint SurfaceId { get; }
    uint Width { get; }
    uint Height { get; }

    void Resize(uint width, uint height);
    void DisposeSurface();
    IntPtr GetHandle();
    IntPtr GetSharedHandle(uint frameIndex);
    ulong GetSharedMemorySize(uint frameIndex);
    IntPtr GetRenderFinishedSemaphoreHandle(uint frameIndex);
    IntPtr CreateConsumedSemaphoreHandle(uint frameIndex);
    void CompleteConsumedSemaphoreHandle(IntPtr handle);
    void ReleaseConsumedSemaphoreHandle(IntPtr handle);
    ulong GetLastRenderTicket();
    uint GetLastRenderFrameIndex();
    Task WaitForRenderTicketAsync(ulong ticket);
    RenderOutputInfo GetOutputInfo();
    void ReportConsumedFrameIndex(uint frameIndex);
    uint GetLastConsumedFrameIndex();
    
    // Lifecycle hooks
    void OnCreate();
    void OnResizing();
    void OnResized();
    void OnDestroy();
}

using System;
using System.Threading.Tasks;

namespace ArisenKernel.Contracts;

public enum SurfaceType
{
    Window,
    SharedHandle,
    SceneView
}

public struct SurfaceInfo
{
    public string Name;
    public IntPtr Parent;
    public IRenderSurface Surface;
    public SurfaceType SurfaceType;
}

public struct RenderOutputInfo
{
    public ulong Ticket;
    public uint FrameIndex;
    public IntPtr SharedHandle;
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

using Arisen.Native.RHI;

namespace ArisenEngine.Core.RHI;

public class RHISurface
{
    internal IntPtr Handle { get; }

    public RHISurface(IntPtr handle)
    {
        Handle = handle;
    }

    public RHISwapChain GetSwapChain()
    {
        RHISurfaceAPI.RHISurface_InitSwapChain(Handle);
        var scHandle = RHISurfaceAPI.RHISurface_GetSwapChain(Handle);
        return new RHISwapChain(scHandle);
    }
}
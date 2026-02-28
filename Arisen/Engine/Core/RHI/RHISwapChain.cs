using Arisen.Native.RHI;

namespace ArisenEngine.Core.RHI;

public class RHISwapChain
{
    internal IntPtr Handle { get; }

    public RHISwapChain(IntPtr handle)
    {
        Handle = handle;
    }

    public RHIImageHandle BeginFrame(uint frameIndex)
    {
        ulong handleValue = RHISwapChainAPI.RHISwapChain_BeginFrame(Handle, frameIndex);
        return new RHIImageHandle { Index = (uint)(handleValue & 0xFFFFFFFF), Generation = (uint)(handleValue >> 32) };
    }

    public void EndFrame(uint frameIndex)
    {
        RHISwapChainAPI.RHISwapChain_EndFrame(Handle, frameIndex);
    }
}

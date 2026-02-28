using Arisen.Native.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.RHI;

public readonly struct RHIDevice
{
    internal IntPtr Handle { get; }

    public bool IsValid => Handle != IntPtr.Zero;

    public RHIPipelineCache PipelineCache => new RHIPipelineCache(RHIDeviceAPI.RHIDevice_GetPipelineCache(Handle));

    public RHIDevice(IntPtr handle)
    {
        Handle = handle;
    }

    public RHIFactory GetFactory()
    {
        var factoryHandle = RHIDeviceAPI.RHIDevice_GetFactory(Handle);
        return new RHIFactory(factoryHandle);
    }

    public RHIInstance GetInstance()
    {
        var instHandle = RHIDeviceAPI.RHIDevice_GetInstance(Handle);
        return new RHIInstance(instHandle);
    }

    public void WaitIdle()
    {
        RHIDeviceAPI.RHIDevice_DeviceWaitIdle(Handle);
    }

    public RHICommandBufferPool GetCommandBufferPool(RHICommandBufferPoolHandle handle)
    {
        var poolPtr = RHIDeviceAPI.RHIDevice_GetCommandBufferPool(Handle, handle.Index, handle.Generation);
        return new RHICommandBufferPool(poolPtr, handle);
    }

    public ulong Submit(RHICommandBuffer cb, RHISwapChain? waitSC = null, RHISwapChain? signalSC = null)
    {
        if (!waitSC.HasValue && !signalSC.HasValue)
        {
            return RHIDeviceAPI.RHIDevice_Submit(Handle, cb.RHIHandle.Index, cb.RHIHandle.Generation, IntPtr.Zero);
        }

        // We temporarily pass the desc as an array of 6 ulongs/IntPtrs which represents the struct layout
        // in C++: RHISwapChain*, RHISwapChain*, const uint64_t*, uint32_t, const uint64_t*, uint32_t
        // This avoids heap allocations until the struct auto-generator is fully implemented.
        unsafe
        {
            IntPtr* descArray = stackalloc IntPtr[6];
            descArray[0] = waitSC?.Handle ?? IntPtr.Zero;
            descArray[1] = signalSC?.Handle ?? IntPtr.Zero;
            descArray[2] = IntPtr.Zero;
            descArray[3] = IntPtr.Zero; // 0 count
            descArray[4] = IntPtr.Zero;
            descArray[5] = IntPtr.Zero; // 0 count
            
            return RHIDeviceAPI.RHIDevice_Submit(Handle, cb.RHIHandle.Index, cb.RHIHandle.Generation, (IntPtr)descArray);
        }
    }

    public void WaitQueueTicket(ulong ticket)
    {
        RHIDeviceAPI.RHIDevice_WaitQueueTicket(Handle, ticket);
    }

    public RHISurface GetSurface()
    {
        var surfacePtr = RHIDeviceAPI.RHIDevice_GetSurface(Handle);
        return new RHISurface(surfacePtr);
    }
}

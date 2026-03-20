using Arisen.Native.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.RHI;

public readonly struct RHIDevice
{
    internal IntPtr Handle { get; }

    public bool IsValid => Handle != IntPtr.Zero;

    public RHIPipelineCache PipelineCache => new RHIPipelineCache(RHIDeviceAPI.RHIDevice_GetPipelineCache(Handle));

    public RHIDescriptorPool DescriptorPool => new RHIDescriptorPool(RHIDeviceAPI.RHIDevice_GetDescriptorPool(Handle));

    public unsafe RHIDescriptorPoolHandle DescriptorPoolHandle
    {
        get
        {
            ulong packed = RHIDeviceAPI.RHIDevice_GetDescriptorPoolHandle(Handle);
            return *(RHIDescriptorPoolHandle*)&packed;
        }
    }

    public RHIDevice(IntPtr handle)
    {
        Handle = handle;
    }

    public RHIFactory GetFactory()
    {
        var factoryHandle = RHIDeviceAPI.RHIDevice_GetFactory(Handle);
        return new RHIFactory(factoryHandle, Handle);
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
        return new RHICommandBufferPool(poolPtr, Handle, handle);
    }

    public RHIQueue GetQueue(RHIQueueType queueType)
    {
        var queuePtr = RHIDeviceAPI.RHIDevice_GetQueue(Handle, (int)queueType);
        return new RHIQueue(queuePtr);
    }

    public unsafe ulong Submit(RHICommandBuffer cb, RHISwapChain? waitSC = null, RHISwapChain? signalSC = null)
    {
        if (!waitSC.HasValue && !signalSC.HasValue)
        {
            return RHIDeviceAPI.RHIDevice_Submit(Handle, cb.RHIHandle.Index, cb.RHIHandle.Generation, IntPtr.Zero);
        }

        var bridgeDesc = new RHISubmitDescriptor_Bridge
        {
            WaitSwapChain = waitSC?.Handle ?? IntPtr.Zero,
            SignalSwapChain = signalSC?.Handle ?? IntPtr.Zero,
            PWaitSemaphores = IntPtr.Zero,
            WaitSemaphoreCount = 0,
            PSignalSemaphores = IntPtr.Zero,
            SignalSemaphoreCount = 0
        };

        return RHIDeviceAPI.RHIDevice_Submit(Handle, cb.RHIHandle.Index, cb.RHIHandle.Generation,
            (IntPtr)(&bridgeDesc));
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
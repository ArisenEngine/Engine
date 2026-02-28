using Arisen.Native.RHI;

namespace ArisenEngine.Core.RHI;

public class RHIDevice
{
    internal IntPtr Handle { get; }

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

    public RHICommandBuffer GetCommandBuffer(RHICommandBufferHandle handle)
    {
        var cbPtr = RHIDeviceAPI.RHIDevice_GetCommandBuffer(Handle, handle.Index, handle.Generation);
        return new RHICommandBuffer(cbPtr, handle);
    }

    public RHICommandBufferPool GetCommandBufferPool(RHICommandBufferPoolHandle handle)
    {
        var poolPtr = RHIDeviceAPI.RHIDevice_GetCommandBufferPool(Handle, handle.Index, handle.Generation);
        return new RHICommandBufferPool(poolPtr, handle);
    }

    public ulong Submit(RHICommandBuffer cb)
    {
        return RHIDeviceAPI.RHIDevice_Submit(Handle, cb.RHIHandle.Index, cb.RHIHandle.Generation);
    }

    public void WaitQueueTicket(ulong ticket)
    {
        RHIDeviceAPI.RHIDevice_WaitQueueTicket(Handle, ticket);
    }
}

using Arisen.Native.RHI;
using System;

namespace ArisenEngine.Core.RHI;

public readonly struct RHICommandBufferPool
{
    internal IntPtr Handle { get; }
    internal IntPtr DeviceHandle { get; }
    public RHICommandBufferPoolHandle RHIHandle { get; }

    public bool IsValid => Handle != IntPtr.Zero;

    internal RHICommandBufferPool(IntPtr handle, IntPtr deviceHandle, RHICommandBufferPoolHandle rhiHandle)
    {
        Handle = handle;
        DeviceHandle = deviceHandle;
        RHIHandle = rhiHandle;
    }

    public unsafe RHICommandBuffer GetCommandBuffer(uint currentFrameIndex,
        ECommandBufferLevel level = ECommandBufferLevel.COMMAND_BUFFER_LEVEL_PRIMARY)
    {
        uint index = 0;
        uint gen = 0;
        RHICommandBufferPoolAPI.RHICommandBufferPool_GetCommandBuffer(Handle, currentFrameIndex, (int)level,
            (IntPtr)(&index), (IntPtr)(&gen));

        var handle = new RHICommandBufferHandle { Index = index, Generation = gen };
        var cmdPtr = RHIDeviceAPI.RHIDevice_GetCommandBuffer(DeviceHandle, index, gen);
        return new RHICommandBuffer(currentFrameIndex, cmdPtr, handle);
    }

    public void ReleaseCommandBuffer(uint currentFrameIndex, RHICommandBufferHandle cbHandle)
    {
        RHICommandBufferPoolAPI.RHICommandBufferPool_ReleaseCommandBuffer(Handle, currentFrameIndex, cbHandle.Index,
            cbHandle.Generation);
    }
}
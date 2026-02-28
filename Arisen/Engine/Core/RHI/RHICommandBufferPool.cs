using Arisen.Native.RHI;
using System;

namespace ArisenEngine.Core.RHI;

public class RHICommandBufferPool
{
    internal IntPtr Handle { get; }
    public RHICommandBufferPoolHandle RHIHandle { get; }

    internal RHICommandBufferPool(IntPtr handle, RHICommandBufferPoolHandle rhiHandle)
    {
        Handle = handle;
        RHIHandle = rhiHandle;
    }

    public unsafe RHICommandBufferHandle GetCommandBuffer(uint currentFrameIndex, ECommandBufferLevel level = ECommandBufferLevel.COMMAND_BUFFER_LEVEL_PRIMARY)
    {
        uint index = 0;
        uint gen = 0;
        RHICommandBufferPoolAPI.RHICommandBufferPool_GetCommandBuffer(Handle, currentFrameIndex, (int)level, (IntPtr)(&index), (IntPtr)(&gen));
        return new RHICommandBufferHandle { Index = index, Generation = gen };
    }

    public void ReleaseCommandBuffer(uint currentFrameIndex, RHICommandBufferHandle cbHandle)
    {
        RHICommandBufferPoolAPI.RHICommandBufferPool_ReleaseCommandBuffer(Handle, currentFrameIndex, cbHandle.Index, cbHandle.Generation);
    }
}

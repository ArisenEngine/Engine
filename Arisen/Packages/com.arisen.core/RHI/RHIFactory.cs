using Arisen.Native.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.RHI;

public readonly struct RHIFactory
{
    internal IntPtr Handle { get; }
    internal IntPtr DeviceHandle { get; }

    public bool IsValid => Handle != IntPtr.Zero;

    public RHIFactory(IntPtr handle, IntPtr deviceHandle)
    {
        Handle = handle;
        DeviceHandle = deviceHandle;
    }

    public unsafe RHIBufferHandle CreateBuffer(ulong size, uint usage, ESharingMode sharingMode,
        ERHIMemoryUsage memoryUsage, string name)
    {
        uint index = 0;
        uint gen = 0;

        RHIFactoryAPI.RHIFactory_CreateBuffer(Handle, 0, size, usage, (int)sharingMode, 0, (int)memoryUsage, name,
            (IntPtr)(&index), (IntPtr)(&gen));

        return new RHIBufferHandle { Index = index, Generation = gen };
    }

    public void ReleaseBuffer(RHIBufferHandle handle)
    {
        RHIFactoryAPI.RHIFactory_ReleaseBuffer(Handle, handle.Index, handle.Generation);
    }

    public unsafe RHIImageHandle CreateImage(uint width, uint height, uint depth, uint mipLevels, uint arrayLayers,
        EFormat format, string name)
    {
        uint index = 0;
        uint gen = 0;

        // imageType 1 = 2D, tiling 0 = Optimal, layout 0 = Undefined, samples 1 = 1x
        RHIFactoryAPI.RHIFactory_CreateImage(Handle, 1, width, height, depth, mipLevels, arrayLayers, (int)format, 0, 0,
            0, 1, 0, 0, name, (IntPtr)(&index), (IntPtr)(&gen));

        return new RHIImageHandle { Index = index, Generation = gen };
    }

    public void ReleaseImage(RHIImageHandle handle)
    {
        RHIFactoryAPI.RHIFactory_ReleaseImage(Handle, handle.Index, handle.Generation);
    }

    public IntPtr MapBuffer(RHIBufferHandle handle)
    {
        return RHIFactoryAPI.RHIFactory_MapBuffer(Handle, handle.Index, handle.Generation);
    }

    public void UnmapBuffer(RHIBufferHandle handle)
    {
        RHIFactoryAPI.RHIFactory_UnmapBuffer(Handle, handle.Index, handle.Generation);
    }

    public unsafe RHICommandBufferPool CreateCommandBufferPool(RHIQueueType queueType)
    {
        uint index = 0;
        uint gen = 0;
        RHIFactoryAPI.RHIFactory_CreateCommandBufferPool(Handle, (int)queueType, (IntPtr)(&index), (IntPtr)(&gen));

        var poolHandle = new RHICommandBufferPoolHandle { Index = index, Generation = gen };
        var poolPtr = RHIDeviceAPI.RHIDevice_GetCommandBufferPool(DeviceHandle, index, gen);
        return new RHICommandBufferPool(poolPtr, DeviceHandle, poolHandle);
    }

    public void ReleaseCommandBufferPool(RHICommandBufferPoolHandle handle)
    {
        RHIFactoryAPI.RHIFactory_ReleaseCommandBufferPool(Handle, handle.Index, handle.Generation);
    }

    public unsafe RHIRenderPassHandle CreateRenderPass()
    {
        uint index = 0;
        uint gen = 0;
        RHIFactoryAPI.RHIFactory_CreateRenderPass(Handle, (IntPtr)(&index), (IntPtr)(&gen));
        return new RHIRenderPassHandle { Index = index, Generation = gen };
    }

    public unsafe RHIFrameBufferHandle CreateFrameBuffer()
    {
        uint index = 0;
        uint gen = 0;
        RHIFactoryAPI.RHIFactory_CreateFrameBuffer(Handle, (IntPtr)(&index), (IntPtr)(&gen));
        return new RHIFrameBufferHandle { Index = index, Generation = gen };
    }

    public unsafe RHISamplerHandle CreateSampler(EFilter magFilter, EFilter minFilter, ESamplerMipmapMode mipmapMode,
        ESamplerAddressMode addressMode)
    {
        uint index = 0;
        uint gen = 0;
        RHIFactoryAPI.RHIFactory_CreateSampler(Handle, (int)magFilter, (int)minFilter, (int)mipmapMode,
            (int)addressMode, (int)addressMode, (int)addressMode, 0, 0, 1.0f, 0, 0, 0, 1.0f, 0, (IntPtr)(&index),
            (IntPtr)(&gen));
        return new RHISamplerHandle { Index = index, Generation = gen };
    }

    public unsafe RHIShaderProgramHandle CreateGPUProgram()
    {
        uint index = 0;
        uint gen = 0;
        RHIFactoryAPI.RHIFactory_CreateGPUProgram(Handle, (IntPtr)(&index), (IntPtr)(&gen));
        return new RHIShaderProgramHandle { Index = index, Generation = gen };
    }

    public void ReleaseGPUProgram(RHIShaderProgramHandle handle)
    {
        RHIFactoryAPI.RHIFactory_ReleaseGPUProgram(Handle, handle.Index, handle.Generation);
    }

    public EFormat GetImageViewFormat(RHIImageViewHandle handle)
    {
        return (EFormat)RHIFactoryAPI.RHIFactory_GetImageViewFormat(Handle, handle.Index, handle.Generation);
    }

    public bool AttachProgramByteCode(RHIShaderProgramHandle handle, EShaderStage stage, byte[] code, string entryPoint)
    {
        unsafe
        {
            fixed (byte* pCode = code)
            {
                return RHIFactoryAPI.RHIFactory_AttachProgramByteCode(Handle, handle.Index, handle.Generation, (int)stage,
                    (IntPtr)pCode, (ulong)code.Length, entryPoint) != 0;
            }
        }
    }
}
using Arisen.Native.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.RHI;

public class RHIFactory
{
    internal IntPtr Handle { get; }

    public RHIFactory(IntPtr handle)
    {
        Handle = handle;
    }

    public unsafe RHIBufferHandle CreateBuffer(ulong size, uint usage, ESharingMode sharingMode, ERHIMemoryUsage memoryUsage, string name)
    {
        uint index = 0;
        uint gen = 0;
        
        RHIFactoryAPI.RHIFactory_CreateBuffer(Handle, 0, size, usage, (int)sharingMode, 0, (int)memoryUsage, name, (IntPtr)(&index), (IntPtr)(&gen));

        return new RHIBufferHandle { Index = index, Generation = gen };
    }

    public void ReleaseBuffer(RHIBufferHandle handle)
    {
        RHIFactoryAPI.RHIFactory_ReleaseBuffer(Handle, handle.Index, handle.Generation);
    }

    public unsafe RHIImageHandle CreateImage(uint width, uint height, uint depth, uint mipLevels, uint arrayLayers, EFormat format, string name)
    {
        uint index = 0;
        uint gen = 0;

        // imageType 1 = 2D, tiling 0 = Optimal, layout 0 = Undefined, samples 1 = 1x
        RHIFactoryAPI.RHIFactory_CreateImage(Handle, 1, width, height, depth, mipLevels, arrayLayers, (int)format, 0, 0, 0, 1, 0, 0, name, (IntPtr)(&index), (IntPtr)(&gen));

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

    public unsafe RHICommandBufferPoolHandle CreateCommandBufferPool(RHIQueueType queueType)
    {
        uint index = 0;
        uint gen = 0;
        RHIFactoryAPI.RHIFactory_CreateCommandBufferPool(Handle, (int)queueType, (IntPtr)(&index), (IntPtr)(&gen));
        return new RHICommandBufferPoolHandle { Index = index, Generation = gen };
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

    public unsafe RHISamplerHandle CreateSampler(EFilter magFilter, EFilter minFilter, ESamplerMipmapMode mipmapMode, ESamplerAddressMode addressMode)
    {
        uint index = 0;
        uint gen = 0;
        RHIFactoryAPI.RHIFactory_CreateSampler(Handle, (int)magFilter, (int)minFilter, (int)mipmapMode, (int)addressMode, (int)addressMode, (int)addressMode, 0, 0, 1.0f, 0, 0, 0, 1.0f, 0, (IntPtr)(&index), (IntPtr)(&gen));
        return new RHISamplerHandle { Index = index, Generation = gen };
    }

    public unsafe RHIShaderProgramHandle CreateGPUProgram()
    {
        uint index = 0;
        uint gen = 0;
        RHIFactoryAPI.RHIFactory_CreateGPUProgram(Handle, (IntPtr)(&index), (IntPtr)(&gen));
        return new RHIShaderProgramHandle { Index = index, Generation = gen };
    }

    public bool AttachProgramByteCode(RHIShaderProgramHandle handle, int stage, byte[] code, string entryPoint)
    {
        unsafe
        {
            fixed (byte* pCode = code)
            {
                return RHIFactoryAPI.RHIFactory_AttachProgramByteCode(Handle, handle.Index, handle.Generation, stage, (IntPtr)pCode, (ulong)code.Length, entryPoint) != 0;
            }
        }
    }
}

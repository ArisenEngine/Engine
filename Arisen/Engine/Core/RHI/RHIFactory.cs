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
}

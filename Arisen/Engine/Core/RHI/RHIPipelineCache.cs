using Arisen.Native.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.RHI;

public class RHIPipelineCache
{
    internal IntPtr NativePtr { get; }
    internal RHIPipelineCache(IntPtr ptr) => NativePtr = ptr;

    public unsafe RHIPipelineHandle GetGraphicsPipeline(RHIPipelineState pso)
    {
        IntPtr result = RHIPipelineAPI.RHIPipelineCache_GetGraphicsPipeline(NativePtr, pso.NativePtr);
        ulong u = (ulong)result;
        return *(RHIPipelineHandle*)(&u);
    }

    public RHIPipelineState GetPipelineState()
    {
        IntPtr ptr = RHIPipelineAPI.RHIPipelineCache_GetPipelineState(NativePtr);
        return new RHIPipelineState(ptr);
    }
}
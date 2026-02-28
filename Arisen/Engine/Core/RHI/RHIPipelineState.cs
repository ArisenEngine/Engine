using Arisen.Native.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.RHI;

public class RHIPipelineState : IDisposable
{
    internal IntPtr NativePtr { get; }
    internal RHIPipelineState(IntPtr ptr) => NativePtr = ptr;

    public void AddProgram(RHIShaderProgramHandle handle)
    {
        RHIPipelineAPI.RHIPipelineState_AddProgram(NativePtr, handle.Index, handle.Generation);
    }

    public void SetBindPoint(EPipelineBindPoint bindPoint)
    {
        RHIPipelineAPI.RHIPipelineState_SetBindPoint(NativePtr, (int)bindPoint);
    }

    public void SetInputAssemblyState(EPrimitiveTopology topology, bool primitiveRestart = false)
    {
        RHIPipelineAPI.RHIPipelineState_SetInputAssemblyState(NativePtr, (int)topology, primitiveRestart ? 1 : 0);
    }

    public void SetRasterizationState(EPolygonMode polygonMode, ECullModeFlagBits cullMode, EFrontFace frontFace)
    {
        RHIPipelineAPI.RHIPipelineState_SetRasterizationState(NativePtr, (int)polygonMode, (int)cullMode, (int)frontFace);
    }

    public unsafe void SetRenderingFormats(EFormat[] colorFormats, EFormat depthFormat)
    {
        fixed (EFormat* pFormats = colorFormats)
        {
            RHIPipelineAPI.RHIPipelineState_SetRenderingFormats(NativePtr, (IntPtr)pFormats, (uint)colorFormats.Length, (int)depthFormat);
        }
    }

    public void Dispose()
    {
        RHIPipelineAPI.RHIPipelineState_Delete(NativePtr);
        GC.SuppressFinalize(this);
    }

    ~RHIPipelineState()
    {
        // Note: Real implementation should be careful about disposing native objects on finalizer thread
    }
}

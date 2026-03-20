using Arisen.Native.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.RHI;

public readonly struct RHIPipelineState
{
    internal IntPtr NativePtr { get; }
    public bool IsValid => NativePtr != IntPtr.Zero;

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
        RHIPipelineAPI.RHIPipelineState_SetRasterizationState(NativePtr, (int)polygonMode, (int)cullMode,
            (int)frontFace);
    }

    public unsafe void SetRenderingFormats(EFormat[] colorFormats, EFormat depthFormat)
    {
        fixed (EFormat* pFormats = colorFormats)
        {
            RHIPipelineAPI.RHIPipelineState_SetRenderingFormats(NativePtr, (IntPtr)pFormats, (uint)colorFormats.Length,
                (int)depthFormat);
        }
    }

    public void SetColorBlendState(bool blendEnable = false, EBlendFactor srcColor = EBlendFactor.BLEND_FACTOR_ZERO,
        EBlendFactor dstColor = EBlendFactor.BLEND_FACTOR_ZERO, EBlendOp colorOp = EBlendOp.BLEND_OP_ADD)
    {
        RHIPipelineAPI.RHIPipelineState_SetColorBlendState(NativePtr, blendEnable ? 1 : 0, (int)srcColor, (int)dstColor,
            (int)colorOp);
    }

    public void SetDynamicStateMask(ulong mask)
    {
        RHIPipelineAPI.RHIPipelineState_SetDynamicStateMask(NativePtr, mask);
    }

    public void Release()
    {
        RHIPipelineAPI.RHIPipelineState_Delete(NativePtr);
    }

    public unsafe void UpdateDescriptorSet(uint layoutIndex, uint binding, RHIBufferHandle[] bufferHandles)
    {
        uint[] indices = new uint[bufferHandles.Length];
        uint[] generations = new uint[bufferHandles.Length];
        for (int i = 0; i < bufferHandles.Length; i++)
        {
            indices[i] = bufferHandles[i].Index;
            generations[i] = bufferHandles[i].Generation;
        }

        fixed (uint* pIndices = indices)
        fixed (uint* pGenerations = generations)
        {
            RHIPipelineAPI.RHIPipelineState_UpdateDescriptorSetBuffer(NativePtr, layoutIndex, binding, (IntPtr)pIndices,
                (IntPtr)pGenerations, (uint)bufferHandles.Length);
        }
    }

    public void BuildDescriptorSetLayout()
    {
        RHIPipelineAPI.RHIPipelineState_BuildDescriptorSetLayout(NativePtr);
    }
}
using Arisen.Native.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.RHI;

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct RHIClearValue
{
    [FieldOffset(0)]
    public float R;
    [FieldOffset(4)]
    public float G;
    [FieldOffset(8)]
    public float B;
    [FieldOffset(12)]
    public float A;

    [FieldOffset(0)]
    public float Depth;
    [FieldOffset(4)]
    public uint Stencil;

    public static RHIClearValue Color(float r, float g, float b, float a) 
        => new RHIClearValue { R = r, G = g, B = b, A = a };
    
    public static RHIClearValue DepthStencil(float depth, uint stencil) 
        => new RHIClearValue { Depth = depth, Stencil = stencil };
}

public readonly struct RHICommandBuffer
{
    private readonly uint _frameIndex;

    internal IntPtr NativePtr { get; }
    public RHICommandBufferHandle RHIHandle { get; }
    
    public bool IsValid => NativePtr != IntPtr.Zero;

    internal RHICommandBuffer(uint frameIndex, IntPtr nativePtr, RHICommandBufferHandle handle)
    {
        _frameIndex = frameIndex;
        NativePtr = nativePtr;
        RHIHandle = handle;
    }

    public void Begin()
    {
        RHICommandBufferAPI.RHICommandBuffer_Begin(NativePtr, _frameIndex);
    }

    public void End()
    {
        RHICommandBufferAPI.RHICommandBuffer_End(NativePtr);
    }

    public unsafe void BeginRenderPass(RHIRenderPassHandle renderPass, RHIFrameBufferHandle frameBuffer, ESubpassContents contents, RHIClearValue[] clearValues)
    {
        fixed (RHIClearValue* pValues = clearValues)
        {
            RHICommandBufferAPI.RHICommandBuffer_BeginRenderPass(NativePtr, renderPass, frameBuffer, (int)contents, (uint)clearValues.Length, (IntPtr)pValues);
        }
    }

    public void EndRenderPass()
    {
        RHICommandBufferAPI.RHICommandBuffer_EndRenderPass(NativePtr);
    }

    public void BindPipeline(RHIPipelineHandle pipeline)
    {
        RHICommandBufferAPI.RHICommandBuffer_BindPipeline(NativePtr, pipeline);
    }

    public void SetViewport(float x, float y, float width, float height, float minDepth = 0.0f, float maxDepth = 1.0f)
    {
        RHICommandBufferAPI.RHICommandBuffer_SetViewport(NativePtr, x, y, width, height, minDepth, maxDepth);
    }

    public void SetScissor(uint offsetX, uint offsetY, uint width, uint height)
    {
        RHICommandBufferAPI.RHICommandBuffer_SetScissor(NativePtr, offsetX, offsetY, width, height);
    }

    public void BindVertexBuffers(RHIBufferHandle buffer, ulong offset = 0)
    {
        RHICommandBufferAPI.RHICommandBuffer_BindVertexBuffers(NativePtr, buffer, offset);
    }

    public void BindIndexBuffer(RHIBufferHandle buffer, ulong offset, EIndexType indexType)
    {
        RHICommandBufferAPI.RHICommandBuffer_BindIndexBuffer(NativePtr, buffer, offset, (int)indexType);
    }

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0, uint firstBinding = 0)
    {
        RHICommandBufferAPI.RHICommandBuffer_Draw(NativePtr, vertexCount, instanceCount, firstVertex, firstInstance, firstBinding);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0, uint firstBinding = 0)
    {
        RHICommandBufferAPI.RHICommandBuffer_DrawIndexed(NativePtr, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance, firstBinding);
    }

    public void TransitionImageLayout(RHIImageHandle image, EImageLayout targetLayout)
    {
        RHICommandBufferAPI.RHICommandBuffer_TransitionImageLayout(NativePtr, image, (int)targetLayout);
    }

    public void TransitionImageLayout(RHIImageHandle image, EImageLayout oldLayout, EImageLayout targetLayout)
    {
        RHICommandBufferAPI.RHICommandBuffer_TransitionImageLayoutExplicit(NativePtr, image, (int)oldLayout, (int)targetLayout);
    }

    public void BindDescriptorSets(EPipelineBindPoint bindPoint, uint firstSet, RHIDescriptorPoolHandle poolHandle, uint poolId)
    {
        RHICommandBufferAPI.RHICommandBuffer_BindDescriptorSets(NativePtr, (int)bindPoint, firstSet, poolHandle, poolId);
    }

    public unsafe void PushConstants(uint offset, uint size, IntPtr data, uint stageFlags)
    {
        RHICommandBufferAPI.RHICommandBuffer_PushConstants(NativePtr, offset, size, data, stageFlags);
    }

    public void CopyBuffer(RHIBufferHandle src, ulong srcOffset, RHIBufferHandle dst, ulong dstOffset, ulong size)
    {
        RHICommandBufferAPI.RHICommandBuffer_CopyBuffer(NativePtr, src, srcOffset, dst, dstOffset, size);
    }

}

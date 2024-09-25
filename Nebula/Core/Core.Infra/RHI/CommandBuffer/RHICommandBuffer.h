#pragma once
#include "../../Common/CommandHeaders.h"
#include "../Program/GPURenderPass.h"
#include "../Surfaces/FrameBuffer.h"
#include "RHI/Devices/Device.h"
#include "RHI/Enums/Pipeline/ECommandBufferUsageFlagBits.h"
#include "RHI/Enums/Pipeline/EIndexType.h"
#include "RHI/Enums/Pipeline/EPipelineStageFlag.h"
#include "RHI/Enums/Subpass/ESubpassContents.h"
#include "RHI/Handles/BufferHandle.h"

namespace NebulaEngine::RHI
{
    class RHIFence;
}

namespace NebulaEngine::RHI
{
    class RHISemaphore;
}

namespace NebulaEngine::RHI
{
    class GPUPipeline;
    class Device;
    class RHICommandBufferPool;
    class FrameBuffer;
    class Viewport;
    class ImageHandle;
    class BufferHandle;
    class GPUPipelineManager;

    typedef struct RenderPassBeginDesc
    {
        GPURenderPass* renderPass;
        FrameBuffer* frameBuffer;
        ESubpassContents subpassContents;
    } RenderPassBeginDesc;
    
    class RHICommandBuffer
    {
       
        
    public:
        enum class ECommandState : u8
        {
            ReadyForBegin,
            IsInsideBegin,
            IsInsideRenderPass,
            ReadyForSubmit,
            NotAllocated,
            NeedReset,
        };
        
        NO_COPY_NO_MOVE_NO_DEFAULT(RHICommandBuffer)

        RHICommandBuffer(Device* device, RHICommandBufferPool* pool):
        m_CommandBufferPool(pool), m_Device(device), m_State(ECommandState::NotAllocated)
        {
            
        }
        
        virtual ~RHICommandBuffer()
        {
            m_CommandBufferPool = nullptr;
            m_Device = nullptr;
        }
        
        RHICommandBufferPool* GetOwner() const
        {
            return m_CommandBufferPool;
        };
        
        virtual void* GetHandle() const = 0;
        virtual void* GetHandlerPointer() = 0;

        // Command Interface
        virtual void BeginRenderPass(u32 frameIndex, RenderPassBeginDesc&& desc) = 0;
        virtual void EndRenderPass() = 0;
        
        virtual void Begin(u32 frameIndex) = 0;
        virtual void Begin(u32 frameIndex, u32 commandBufferUsage) = 0;
        virtual void End() = 0;
        
        virtual void SetViewport(f32 x, f32 y, f32 width, f32 height, f32 minDepth, f32 maxDepth) = 0;
        virtual void SetViewport(f32 x, f32 y, f32 width, f32 height) = 0;
        virtual void SetScissor(u32 offsetX, u32 offsetY, u32 width, u32 height) = 0;

        virtual void BindPipeline(u32 frameIndex, GPUPipeline* pipeline) = 0;
        virtual void Draw(u32 vertexCount, u32 instanceCount, u32 firstVertex, u32 firstInstance, u32 firstBinding) = 0;
        virtual void DrawIndexed(u32 indexCount, u32 instanceCount, u32 firstIndex, u32 vertexOffset, u32 firstInstance,  u32 firstBinding) = 0;
        virtual void BindVertexBuffers(BufferHandle* buffer, u64 offset) = 0;
        virtual void BindIndexBuffer(BufferHandle* indexBuffer, u64 offset, EIndexType type) = 0;
        
        virtual void WaitSemaphore(RHISemaphore* semaphore, EPipelineStageFlag stage) = 0;
        virtual void SignalSemaphore(RHISemaphore* semaphore) = 0;
        virtual void InjectFence(RHIFence* fence) = 0;

        virtual void CopyBuffer(BufferHandle const * src, u64 srcOffset, BufferHandle const * dst, u64 dstOffset, u64 size) = 0;
        
        
        virtual void WaitForFence(u32 frameIndex) = 0;
    public:

        const bool ReadyForSubmit() const;

    protected:
        friend RHICommandBufferPool;
        virtual void Reset() = 0;
        virtual void Release() = 0;
        virtual void ReadyForBegin(u32 frameIndex) = 0;
        virtual void DoBegin() = 0;
        RHICommandBufferPool* m_CommandBufferPool;
        Device* m_Device;
        ECommandState m_State;
        
    };

    inline const bool RHICommandBuffer::ReadyForSubmit() const
    {
        return m_State == ECommandState::ReadyForSubmit;
    }
}

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
        enum class ECommandState : UInt8
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
        virtual void BeginRenderPass(UInt32 frameIndex, RenderPassBeginDesc&& desc) = 0;
        virtual void EndRenderPass() = 0;
        
        virtual void Begin(UInt32 frameIndex) = 0;
        virtual void Begin(UInt32 frameIndex, UInt32 commandBufferUsage) = 0;
        virtual void End() = 0;
        
        virtual void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth, Float32 maxDepth) = 0;
        virtual void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height) = 0;
        virtual void SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height) = 0;

        virtual void BindPipeline(UInt32 frameIndex, GPUPipeline* pipeline) = 0;
        virtual void Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance, UInt32 firstBinding) = 0;
        virtual void DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset, UInt32 firstInstance,  UInt32 firstBinding) = 0;
        virtual void BindVertexBuffers(BufferHandle* buffer, UInt64 offset) = 0;
        virtual void BindIndexBuffer(BufferHandle* indexBuffer, UInt64 offset, EIndexType type) = 0;
        
        virtual void WaitSemaphore(RHISemaphore* semaphore, EPipelineStageFlag stage) = 0;
        virtual void SignalSemaphore(RHISemaphore* semaphore) = 0;
        virtual void InjectFence(RHIFence* fence) = 0;

        virtual void CopyBuffer(BufferHandle const * src, UInt64 srcOffset, BufferHandle const * dst, UInt64 dstOffset, UInt64 size) = 0;
        
        
        virtual void WaitForFence(UInt32 frameIndex) = 0;
    public:

        const bool ReadyForSubmit() const;

    protected:
        friend RHICommandBufferPool;
        virtual void Reset() = 0;
        virtual void Release() = 0;
        virtual void ReadyForBegin(UInt32 frameIndex) = 0;
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

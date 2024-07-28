#pragma once
#include "RHICommandBufferPool.h"
#include "../../Common/CommandHeaders.h"
#include "../Enums/ESubpassContents.h"
#include "../Program/GPURenderPass.h"
#include "../Surfaces/FrameBuffer.h"
#include "../Surfaces/Viewport.h"
#include "RHI/Enums/AttachmentLoadOp.h"
#include "RHI/Enums/AttachmentStoreOp.h"

namespace NebulaEngine::RHI
{
    class RHICommandBufferPool;
    class FrameBuffer;
    class Viewport;
    class ImageHandle;
    class GPUPipeline;

    typedef struct RenderPassBeginDesc
    {
        GPURenderPass* renderPass;
        FrameBuffer* frameBuffer;
        Viewport* viewport;
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
            ReadyForEnd,
            HasEnded,
            ReadyForSubmit,
            Submitted,
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

        // Command Interface
        virtual void BeginRenderPass(RenderPassBeginDesc&& desc) = 0;
        virtual void EndRenderPass() = 0;
        
        virtual void Clear() = 0;
        virtual void Begin() = 0;
        virtual void End() = 0;

        virtual void BindPipeline(GPUPipeline* pipeline) = 0;
        
    protected:
        RHICommandBufferPool* m_CommandBufferPool;
        Device* m_Device;
        ECommandState m_State;
    };
}

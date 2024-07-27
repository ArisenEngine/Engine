#pragma once
#include "RHICommandBufferPool.h"
#include "../../Common/CommandHeaders.h"
#include "../Enums/ESubpassContents.h"
#include "../Program/GPURenderPass.h"
#include "../Surfaces/FrameBuffer.h"
#include "../Surfaces/Viewport.h"

namespace NebulaEngine::RHI
{
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
        NO_COPY_NO_MOVE_NO_DEFAULT(RHICommandBuffer)

        RHICommandBuffer(Device* device, RHICommandBufferPool* pool):
        m_CommandBufferPool(pool), m_Device(device)
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
        
        
    protected:
        RHICommandBufferPool* m_CommandBufferPool;
        Device* m_Device;
    };
}

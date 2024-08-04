#pragma once
#include "../../Common/CommandHeaders.h"
#include "../Program/GPURenderPass.h"
#include "../Surfaces/FrameBuffer.h"
#include "RHI/Devices/Device.h"
#include "RHI/Enums/Subpass/ESubpassContents.h"

namespace NebulaEngine::RHI
{
    class GPUPipeline;
    class Device;
    class RHICommandBufferPool;
    class FrameBuffer;
    class Viewport;
    class ImageHandle;
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
            ReadyForEnd,
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
        virtual void BeginRenderPass(RenderPassBeginDesc&& desc) = 0;
        virtual void EndRenderPass() = 0;
        
        virtual void Clear() = 0;
        virtual void Begin() = 0;
        virtual void End() = 0;
        
        virtual void SetViewport(f32 x, f32 y, f32 width, f32 height, f32 minDepth, f32 maxDepth) = 0;
        virtual void SetViewport(f32 x, f32 y, f32 width, f32 height) = 0;
        virtual void SetScissor(u32 offsetX, u32 offsetY, u32 width, u32 height) = 0;

        virtual void BindPipeline(GPUPipeline* pipeline) = 0;
        virtual void Draw(u32 vertexCount, u32 instanceCount, u32 firstVertex, u32 firstInstance) = 0;

    public:

        const bool ReadyForSubmit() const { return m_State == ECommandState::ReadyForSubmit; }
        
    protected:
        RHICommandBufferPool* m_CommandBufferPool;
        Device* m_Device;
        ECommandState m_State;
    };
}

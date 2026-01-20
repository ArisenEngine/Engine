#include "RHIVkFactory.h"
#include "RHIVkDevice.h"
#include "../Program/RHIVkGPUProgram.h"
#include "../CommandBuffer/RHIVkCommandBufferPool.h"
#include "../Program/RHIVkGPURenderPass.h"
#include "../Surfaces/RHIVkFrameBuffer.h"
#include "../Handles/RHIVkBufferHandle.h"
#include "../Handles/RHIVkImageHandle.h"
#include "../Program/RHIVkSampler.h"
#include "RHI/RHIInstance.h"

namespace ArisenEngine::RHI
{
    RHIVkFactory::RHIVkFactory(RHIVkDevice* device) : m_Device(device) {}

    GPUProgram* RHIVkFactory::CreateGPUProgram()
    {
        return new RHIVkGPUProgram((VkDevice)m_Device->GetHandle());
    }

    void RHIVkFactory::ReleaseGPUProgram(GPUProgram* program)
    {
        if (program)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [program]()
            {
                delete program;
            });
        }
    }

    bool RHIVkFactory::AttachProgramByteCode(GPUProgram* program, GPUProgramDesc&& desc)
    {
        if (program)
        {
            return program->AttachProgramByteCode(std::move(desc));
        }
        return false;
    }

    RHICommandBufferPool* RHIVkFactory::CreateCommandBufferPool()
    {
        return new RHIVkCommandBufferPool(m_Device, m_Device->GetInstance()->GetMaxFramesInFlight());
    }

    void RHIVkFactory::ReleaseCommandBufferPool(RHICommandBufferPool* pool)
    {
        if (pool)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [pool]()
            {
                delete pool;
            });
        }
    }

    GPURenderPass* RHIVkFactory::CreateRenderPass()
    {
        return new RHIVkGPURenderPass(m_Device, m_Device->GetInstance()->GetMaxFramesInFlight());
    }

    void RHIVkFactory::ReleaseRenderPass(GPURenderPass* renderPass)
    {
        if (renderPass)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [renderPass]()
            {
                delete renderPass;
            });
        }
    }

    FrameBuffer* RHIVkFactory::CreateFrameBuffer()
    {
        return new RHIVkFrameBuffer(m_Device, m_Device->GetInstance()->GetMaxFramesInFlight());
    }

    void RHIVkFactory::ReleaseFrameBuffer(FrameBuffer* frameBuffer)
    {
        if (frameBuffer)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [frameBuffer]()
            {
                delete frameBuffer;
            });
        }
    }

    BufferHandle* RHIVkFactory::CreateBuffer(const std::string&& name)
    {
        auto* bufferHandle = new RHIVkBufferHandle(m_Device);
        bufferHandle->SetName(std::move(name));
        return bufferHandle;
    }

    void RHIVkFactory::ReleaseBuffer(BufferHandle* bufferHandle)
    {
        if (bufferHandle)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [bufferHandle]()
            {
                delete bufferHandle;
            });
        }
    }

    ImageHandle* RHIVkFactory::CreateImage(const std::string&& name)
    {
        auto* imageHandle = new RHIVkImageHandle(m_Device);
        imageHandle->SetName(std::move(name));
        return imageHandle;
    }

    void RHIVkFactory::ReleaseImage(ImageHandle* imageHandle)
    {
        if (imageHandle)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [imageHandle]()
            {
                delete imageHandle;
            });
        }
    }

    RHISampler* RHIVkFactory::CreateSampler(RHISamplerDesc&& desc)
    {
        return new RHIVkSampler(m_Device, std::move(desc));
    }

    void RHIVkFactory::ReleaseSampler(RHISampler* sampler)
    {
        if (sampler)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [sampler]()
            {
                delete sampler;
            });
        }
    }
}

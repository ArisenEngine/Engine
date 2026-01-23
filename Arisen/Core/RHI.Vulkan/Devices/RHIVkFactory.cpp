#include "RHIVkFactory.h"
#include "RHIVkDevice.h"
#include "../Program/RHIVkGPUProgram.h"
#include "../CommandBuffer/RHIVkCommandBufferPool.h"
#include "../Program/RHIVkGPURenderPass.h"
// #include "../Surfaces/RHIVkFrameBufferPoolItem.h"
#include "../Handles/RHIVkResourcePools.h"
#include "RHI/RHIInstance.h"
#include "../VkInitializer.h"

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

    RHIRenderPassHandle RHIVkFactory::CreateRenderPass()
    {
        auto* rp = new RHIVkRenderPassPoolItem();
        auto* rpObj = new RHIVkGPURenderPass(m_Device, m_Device->GetMaxFramesInFlight());
        rp->renderPassObj = rpObj;
        
        // Register for deferred deletion
        struct DeferredGPURenderPass {
            RHIVkGPURenderPass* obj;
            ~DeferredGPURenderPass() { delete obj; }
        };
        rp->registryHandle = m_Device->GetResourceRegistry()->Create(MakeDeferredDeleteItem(new DeferredGPURenderPass{rpObj}));

        return m_Device->GetRenderPassPool()->Allocate(rp);
    }

    void RHIVkFactory::ReleaseRenderPass(RHIRenderPassHandle renderPass)
    {
        m_Device->ReleaseRenderPass(renderPass);
    }

    RHIFrameBufferHandle RHIVkFactory::CreateFrameBuffer()
    {
        auto* fb = new RHIVkFrameBufferPoolItem();
        return m_Device->GetFrameBufferPool()->Allocate(fb);
    }

    void RHIVkFactory::ReleaseFrameBuffer(RHIFrameBufferHandle frameBuffer)
    {
        m_Device->ReleaseFrameBuffer(frameBuffer);
    }

    ArisenEngine::RHI::RHIBufferHandle ArisenEngine::RHI::RHIVkFactory::CreateBuffer(ArisenEngine::RHI::BufferDescriptor&& desc, const std::string&& name)
    {
        auto handle = m_Device->GetBufferPool()->Allocate(new ArisenEngine::RHI::RHIVkBufferPoolItem());
        auto* buffer = m_Device->GetBufferPool()->Get(handle);
        buffer->name = name; // Changed from move to copy or just assignment

        if (!m_Device->AllocBuffer(handle, std::move(desc)))
        {
            m_Device->ReleaseBuffer(handle);
            return ArisenEngine::RHI::RHIBufferHandle::Invalid();
        }

        if (!m_Device->AllocBufferDeviceMemory(handle, desc.memoryPropertyFlags))
        {
            m_Device->ReleaseBuffer(handle);
            return ArisenEngine::RHI::RHIBufferHandle::Invalid();
        }

        return handle;
    }

    void RHIVkFactory::ReleaseBuffer(RHIBufferHandle bufferHandle)
    {
        m_Device->ReleaseBuffer(bufferHandle);
    }

    ArisenEngine::RHI::RHIImageHandle ArisenEngine::RHI::RHIVkFactory::CreateImage(ArisenEngine::RHI::ImageDescriptor&& desc, const std::string&& name)
    {
        auto handle = m_Device->GetImagePool()->Allocate(new ArisenEngine::RHI::RHIVkImagePoolItem());
        auto* image = m_Device->GetImagePool()->Get(handle);
        image->name = name;

        if (!m_Device->AllocImage(handle, std::move(desc)))
        {
            m_Device->ReleaseImage(handle);
            return ArisenEngine::RHI::RHIImageHandle::Invalid();
        }

        if (!m_Device->AllocImageDeviceMemory(handle, desc.memoryPropertyFlags))
        {
            m_Device->ReleaseImage(handle);
            return ArisenEngine::RHI::RHIImageHandle::Invalid();
        }

        return handle;
    }

    void RHIVkFactory::ReleaseImage(RHIImageHandle imageHandle)
    {
        m_Device->ReleaseImage(imageHandle);
    }

    ArisenEngine::RHI::RHIImageViewHandle ArisenEngine::RHI::RHIVkFactory::CreateImageView(ArisenEngine::RHI::RHIImageHandle imageHandle, ArisenEngine::RHI::ImageViewDesc&& desc)
    {
        auto handle = m_Device->GetImageViewPool()->Allocate(new ArisenEngine::RHI::RHIVkImageViewPoolItem());
        if (!m_Device->AllocImageView(handle, imageHandle, std::move(desc)))
        {
            m_Device->ReleaseImageView(handle);
            return ArisenEngine::RHI::RHIImageViewHandle::Invalid();
        }
        return handle;
    }

    void RHIVkFactory::ReleaseImageView(RHIImageViewHandle imageViewHandle)
    {
        m_Device->ReleaseImageView(imageViewHandle);
    }

    RHISamplerHandle RHIVkFactory::CreateSampler(RHISamplerDesc&& desc)
    {
        auto* sampler = new RHIVkSamplerPoolItem();
        auto samplerInfo = SamplerCreateInfo(std::move(desc));
        if (vkCreateSampler(static_cast<VkDevice>(m_Device->GetHandle()), &samplerInfo, nullptr, &sampler->sampler) != VK_SUCCESS)
        {
            LOG_ERROR("[RHIVkFactory::CreateSampler]: failed to create texture sampler!");
        }

        struct DeferredVkSampler {
            VkDevice device;
            VkSampler sampler;
            ~DeferredVkSampler() {
                if (device != VK_NULL_HANDLE && sampler != VK_NULL_HANDLE) {
                    vkDestroySampler(device, sampler, nullptr);
                }
            }
        };
        auto* deferred = new DeferredVkSampler{ static_cast<VkDevice>(m_Device->GetHandle()), sampler->sampler };
        sampler->registryHandle = m_Device->GetResourceRegistry()->Create(MakeDeferredDeleteItem(deferred));

        return m_Device->GetSamplerPool()->Allocate(sampler);
    }

    void RHIVkFactory::ReleaseSampler(RHISamplerHandle samplerHandle)
    {
        m_Device->ReleaseSampler(samplerHandle);
    }

    RHISemaphoreHandle RHIVkFactory::CreateSemaphore()
    {
        auto* sem = new RHIVkSemaphorePoolItem();
        VkSemaphoreCreateInfo createInfo{};
        createInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO;
        
        if (vkCreateSemaphore(static_cast<VkDevice>(m_Device->GetHandle()), &createInfo, nullptr, &sem->semaphore) != VK_SUCCESS)
        {
            LOG_ERROR("[RHIVkFactory::CreateSemaphore]: failed to create semaphore!");
        }

        struct DeferredVkSemaphore {
            VkDevice device;
            VkSemaphore semaphore;
            ~DeferredVkSemaphore() {
                if (device != VK_NULL_HANDLE && semaphore != VK_NULL_HANDLE) {
                    vkDestroySemaphore(device, semaphore, nullptr);
                }
            }
        };
        auto* deferred = new DeferredVkSemaphore{ static_cast<VkDevice>(m_Device->GetHandle()), sem->semaphore };
        sem->registryHandle = m_Device->GetResourceRegistry()->Create(MakeDeferredDeleteItem(deferred));

        return m_Device->GetSemaphorePool()->Allocate(sem);
    }

    void RHIVkFactory::ReleaseSemaphore(RHISemaphoreHandle semaphoreHandle)
    {
        m_Device->ReleaseSemaphore(semaphoreHandle);
    }

    RHIFenceHandle RHIVkFactory::CreateFence(bool signaled)
    {
        auto* fence = new RHIVkFencePoolItem();
        VkFenceCreateInfo createInfo{};
        createInfo.sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
        if (signaled) createInfo.flags = VK_FENCE_CREATE_SIGNALED_BIT;

        if (vkCreateFence(static_cast<VkDevice>(m_Device->GetHandle()), &createInfo, nullptr, &fence->fence) != VK_SUCCESS)
        {
            LOG_ERROR("[RHIVkFactory::CreateFence]: failed to create fence!");
        }

        struct DeferredVkFence {
            VkDevice device;
            VkFence fence;
            ~DeferredVkFence() {
                if (device != VK_NULL_HANDLE && fence != VK_NULL_HANDLE) {
                    vkDestroyFence(device, fence, nullptr);
                }
            }
        };
        auto* deferred = new DeferredVkFence{ static_cast<VkDevice>(m_Device->GetHandle()), fence->fence };
        fence->registryHandle = m_Device->GetResourceRegistry()->Create(MakeDeferredDeleteItem(deferred));

        return m_Device->GetFencePool()->Allocate(fence);
    }

    void RHIVkFactory::ReleaseFence(RHIFenceHandle fenceHandle)
    {
        m_Device->ReleaseFence(fenceHandle);
    }
}

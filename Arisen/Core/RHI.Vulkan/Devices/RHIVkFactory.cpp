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
        // Note: Actual VkRenderPass is created in Alloc later (if applicable)
        // or we can keep existing logic if we move it.
        return m_Device->GetRenderPassPool()->Allocate(rp);
    }

    void RHIVkFactory::ReleaseRenderPass(RHIRenderPassHandle renderPass)
    {
        auto* rp = m_Device->GetRenderPassPool()->Deallocate(renderPass);
        if (rp)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetCompletedSubmitId(), [rp]()
            {
                // TODO: Cleanup internal rp->renderPass if it exists
                delete rp;
            });
        }
    }

    RHIFrameBufferHandle RHIVkFactory::CreateFrameBuffer()
    {
        auto* fb = new RHIVkFrameBufferPoolItem();
        return m_Device->GetFrameBufferPool()->Allocate(fb);
    }

    void RHIVkFactory::ReleaseFrameBuffer(RHIFrameBufferHandle frameBuffer)
    {
        auto* fb = m_Device->GetFrameBufferPool()->Deallocate(frameBuffer);
        if (fb)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetCompletedSubmitId(), [fb]()
            {
                delete fb;
            });
        }
    }

    RHIBufferHandle RHIVkFactory::CreateBuffer(const std::string&& name)
    {
        auto* buffer = new RHIVkBufferPoolItem();
        buffer->name = std::move(name);
        return m_Device->GetBufferPool()->Allocate(buffer);
    }

    void RHIVkFactory::ReleaseBuffer(RHIBufferHandle bufferHandle)
    {
        m_Device->FreeBuffer(bufferHandle);
        auto* buffer = m_Device->GetBufferPool()->Deallocate(bufferHandle);
        if (buffer)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetCompletedSubmitId(), [buffer]()
            {
                delete buffer;
            });
        }
    }

    RHIImageHandle RHIVkFactory::CreateImage(const std::string&& name)
    {
        auto* image = new RHIVkImagePoolItem();
        image->name = std::move(name);
        return m_Device->GetImagePool()->Allocate(image);
    }

    void RHIVkFactory::ReleaseImage(RHIImageHandle imageHandle)
    {
        m_Device->FreeImage(imageHandle);
        auto* image = m_Device->GetImagePool()->Deallocate(imageHandle);
        if (image)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetCompletedSubmitId(), [image]()
            {
                delete image;
            });
        }
    }

    RHIImageViewHandle RHIVkFactory::CreateImageView()
    {
        auto* view = new RHIVkImageViewPoolItem();
        return m_Device->GetImageViewPool()->Allocate(view);
    }

    void RHIVkFactory::ReleaseImageView(RHIImageViewHandle imageViewHandle)
    {
        m_Device->FreeImageView(imageViewHandle);
        auto* view = m_Device->GetImageViewPool()->Deallocate(imageViewHandle);
        if (view)
        {
            m_Device->EnqueueDeferredDestroy(m_Device->GetCompletedSubmitId(), [view]()
            {
                delete view;
            });
        }
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
        auto* sampler = m_Device->GetSamplerPool()->Deallocate(samplerHandle);
        if (sampler)
        {
            if (sampler->sampler != VK_NULL_HANDLE)
            {
                m_Device->GetResourceRegistry()->Release(sampler->registryHandle, RHIQueueType::Graphics, m_Device->GetCompletedSubmitId());
            }

            m_Device->EnqueueDeferredDestroy(m_Device->GetCompletedSubmitId(), [sampler]()
            {
                delete sampler;
            });
        }
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
        auto* sem = m_Device->GetSemaphorePool()->Deallocate(semaphoreHandle);
        if (sem)
        {
            if (sem->semaphore != VK_NULL_HANDLE)
            {
                m_Device->GetResourceRegistry()->Release(sem->registryHandle, RHIQueueType::Graphics, m_Device->GetCompletedSubmitId());
            }

            m_Device->EnqueueDeferredDestroy(m_Device->GetCompletedSubmitId(), [sem]()
            {
                delete sem;
            });
        }
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
        auto* f = m_Device->GetFencePool()->Deallocate(fenceHandle);
        if (f)
        {
            if (f->fence != VK_NULL_HANDLE)
            {
                m_Device->GetResourceRegistry()->Release(f->registryHandle, RHIQueueType::Graphics, m_Device->GetCompletedSubmitId());
            }

            m_Device->EnqueueDeferredDestroy(m_Device->GetCompletedSubmitId(), [f]()
            {
                delete f;
            });
        }
    }
}

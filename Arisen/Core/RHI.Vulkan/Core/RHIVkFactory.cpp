#include "Core/RHIVkFactory.h"
#include "Core/RHIVkDevice.h"
#include "Pipeline/RHIVkGPUProgram.h"
#include "Commands/RHIVkCommandBufferPool.h"
#include "RenderPass/RHIVkGPURenderPass.h"
#include "Presentation/RHIVkFrameBuffer.h"
#include "Handles/RHIVkResourcePools.h"
#include "RHI/Core/RHIInstance.h"
#include "Utils/RHIVkInitializer.h"

namespace ArisenEngine::RHI
{
    RHIVkFactory::RHIVkFactory(RHIVkDevice* device) : m_Device(device)
    {
    }

    RHIShaderProgramHandle RHIVkFactory::CreateGPUProgram()
    {
        return m_Device->GetGPUProgramPool()->Allocate([this](RHIVkGPUProgramPoolItem* item)
        {
            // Reset item to default state
            *item = RHIVkGPUProgramPoolItem();
            item->program = new RHIVkGPUProgram((VkDevice)m_Device->GetHandle());

            // Register for deferred deletion (of the program object itself)
            struct DeferredGPUProgram
            {
                RHIShaderProgram* prog;
                ~DeferredGPUProgram() { delete prog; }
            };
            item->registryHandle = m_Device->GetResourceRegistry()->Create(
                MakeDeferredDeleteItem(new DeferredGPUProgram{item->program}));
        });
    }

    void RHIVkFactory::ReleaseGPUProgram(RHIShaderProgramHandle handle)
    {
        m_Device->ReleaseGPUProgram(handle);
    }

    bool RHIVkFactory::AttachProgramByteCode(RHIShaderProgramHandle handle, RHIShaderProgramDesc&& desc)
    {
        auto* item = m_Device->GetGPUProgramPool()->Get(handle);
        if (item && item->program)
        {
            return item->program->AttachProgramByteCode(std::move(desc));
        }
        return false;
    }

    RHICommandBufferPoolHandle RHIVkFactory::CreateCommandBufferPool()
    {
        return m_Device->GetCommandBufferPoolPool()->Allocate([this](RHIVkCommandBufferPoolItem* item)
        {
            *item = RHIVkCommandBufferPoolItem();
            item->pool = new RHIVkCommandBufferPool(
                m_Device, m_Device->GetInstance()->GetMaxFramesInFlight());

            struct DeferredCmdPool
            {
                RHICommandBufferPool* p;
                ~DeferredCmdPool() { delete p; }
            };
            item->registryHandle = m_Device->GetResourceRegistry()->Create(
                MakeDeferredDeleteItem(new DeferredCmdPool{item->pool}));
        });
    }

    void RHIVkFactory::ReleaseCommandBufferPool(RHICommandBufferPoolHandle handle)
    {
        m_Device->ReleaseCommandBufferPool(handle);
    }

    RHIRenderPassHandle RHIVkFactory::CreateRenderPass()
    {
        return m_Device->GetRenderPassPool()->Allocate([this](RHIVkRenderPassPoolItem* rp)
        {
            *rp = RHIVkRenderPassPoolItem();
            auto* rpObj = new RHIVkGPURenderPass(m_Device, m_Device->GetMaxFramesInFlight());
            rp->renderPassObj = rpObj;

            // Register for deferred deletion
            struct DeferredGPURenderPass
            {
                RHIVkGPURenderPass* obj;
                ~DeferredGPURenderPass() { delete obj; }
            };
            rp->registryHandle = m_Device->GetResourceRegistry()->Create(
                MakeDeferredDeleteItem(new DeferredGPURenderPass{rpObj}));
        });
    }

    void RHIVkFactory::ReleaseRenderPass(RHIRenderPassHandle renderPass)
    {
        m_Device->ReleaseRenderPass(renderPass);
    }

    RHIFrameBufferHandle RHIVkFactory::CreateFrameBuffer()
    {
        return m_Device->GetFrameBufferPool()->Allocate([this](RHIVkFrameBufferPoolItem* fb)
        {
            *fb = RHIVkFrameBufferPoolItem();
            auto* fbObj = new RHIVkFrameBuffer(m_Device, m_Device->GetMaxFramesInFlight());
            fb->frameBufferObj = fbObj;

            // Register for deferred deletion
            struct DeferredGPUFrameBuffer
            {
                RHIVkFrameBuffer* obj;
                ~DeferredGPUFrameBuffer() { delete obj; }
            };
            fb->registryHandle = m_Device->GetResourceRegistry()->Create(
                MakeDeferredDeleteItem(new DeferredGPUFrameBuffer{fbObj}));
        });
    }

    void RHIVkFactory::ReleaseFrameBuffer(RHIFrameBufferHandle RHIFrameBuffer)
    {
        m_Device->ReleaseFrameBuffer(RHIFrameBuffer);
    }

    ArisenEngine::RHI::RHIBufferHandle ArisenEngine::RHI::RHIVkFactory::CreateBuffer(
        ArisenEngine::RHI::RHIBufferDescriptor&& desc, const String& name)
    {
        auto handle = m_Device->GetBufferPool()->Allocate([&name](ArisenEngine::RHI::RHIVkBufferPoolItem* item)
        {
            *item = ArisenEngine::RHI::RHIVkBufferPoolItem();
            item->name = name;
        });

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

    ArisenEngine::RHI::RHIImageHandle ArisenEngine::RHI::RHIVkFactory::CreateImage(
        ArisenEngine::RHI::RHIImageDescriptor&& desc, const String& name)
    {
        auto handle = m_Device->GetImagePool()->Allocate([&name](ArisenEngine::RHI::RHIVkImagePoolItem* item)
        {
            *item = ArisenEngine::RHI::RHIVkImagePoolItem();
            item->name = name;
        });

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

    ArisenEngine::RHI::RHIImageViewHandle ArisenEngine::RHI::RHIVkFactory::CreateImageView(
        ArisenEngine::RHI::RHIImageHandle imageHandle, ArisenEngine::RHI::RHIImageViewDesc&& desc)
    {
        auto handle = m_Device->GetImageViewPool()->Allocate([](ArisenEngine::RHI::RHIVkImageViewPoolItem* item)
        {
            *item = ArisenEngine::RHI::RHIVkImageViewPoolItem();
        });
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
        return m_Device->GetSamplerPool()->Allocate([this, &desc](RHIVkSamplerPoolItem* sampler)
        {
            *sampler = RHIVkSamplerPoolItem();
            auto samplerInfo = SamplerCreateInfo(std::move(desc));
            if (vkCreateSampler(static_cast<VkDevice>(m_Device->GetHandle()),
                                &samplerInfo, nullptr,
                                &sampler->sampler) != VK_SUCCESS)
            {
                LOG_ERROR("[RHIVkFactory::CreateSampler]: failed to create texture "
                    "sampler!");
            }

            struct DeferredVkSampler
            {
                VkDevice device;
                VkSampler sampler;

                ~DeferredVkSampler()
                {
                    if (device != VK_NULL_HANDLE && sampler != VK_NULL_HANDLE)
                    {
                        vkDestroySampler(device, sampler, nullptr);
                    }
                }
            };
            auto* deferred = new DeferredVkSampler{
                static_cast<VkDevice>(m_Device->GetHandle()), sampler->sampler
            };
            sampler->registryHandle = m_Device->GetResourceRegistry()->Create(
                MakeDeferredDeleteItem(deferred));
        });
    }

    void RHIVkFactory::ReleaseSampler(RHISamplerHandle samplerHandle)
    {
        m_Device->ReleaseSampler(samplerHandle);
    }

    RHISemaphoreHandle RHIVkFactory::CreateSemaphore()
    {
        return m_Device->GetSemaphorePool()->Allocate([this](RHIVkSemaphorePoolItem* sem)
        {
            *sem = RHIVkSemaphorePoolItem();
            VkSemaphoreCreateInfo createInfo{};
            createInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO;

            if (vkCreateSemaphore(static_cast<VkDevice>(m_Device->GetHandle()),
                                  &createInfo, nullptr,
                                  &sem->semaphore) != VK_SUCCESS)
            {
                LOG_ERROR("[RHIVkFactory::CreateSemaphore]: failed to create "
                    "semaphore!");
            }

            struct DeferredVkSemaphore
            {
                VkDevice device;
                VkSemaphore semaphore;

                ~DeferredVkSemaphore()
                {
                    if (device != VK_NULL_HANDLE && semaphore != VK_NULL_HANDLE)
                    {
                        vkDestroySemaphore(device, semaphore, nullptr);
                    }
                }
            };
            auto* deferred = new DeferredVkSemaphore{
                static_cast<VkDevice>(m_Device->GetHandle()), sem->semaphore
            };
            sem->registryHandle = m_Device->GetResourceRegistry()->Create(
                MakeDeferredDeleteItem(deferred));
        });
    }

    void RHIVkFactory::ReleaseSemaphore(RHISemaphoreHandle semaphoreHandle)
    {
        m_Device->ReleaseSemaphore(semaphoreHandle);
    }

    RHIFenceHandle RHIVkFactory::CreateFence(bool signaled)
    {
        return m_Device->GetFencePool()->Allocate([this, signaled](RHIVkFencePoolItem* fence)
        {
            *fence = RHIVkFencePoolItem();
            VkFenceCreateInfo createInfo{};
            createInfo.sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
            if (signaled)
                createInfo.flags = VK_FENCE_CREATE_SIGNALED_BIT;

            if (vkCreateFence(static_cast<VkDevice>(m_Device->GetHandle()),
                              &createInfo, nullptr, &fence->fence) != VK_SUCCESS)
            {
                LOG_ERROR("[RHIVkFactory::CreateFence]: failed to create fence!");
            }

            struct DeferredVkFence
            {
                VkDevice device;
                VkFence fence;

                ~DeferredVkFence()
                {
                    if (device != VK_NULL_HANDLE && fence != VK_NULL_HANDLE)
                    {
                        vkDestroyFence(device, fence, nullptr);
                    }
                }
            };
            auto* deferred = new DeferredVkFence{
                static_cast<VkDevice>(m_Device->GetHandle()), fence->fence
            };
            fence->registryHandle = m_Device->GetResourceRegistry()->Create(
                MakeDeferredDeleteItem(deferred));
        });
    }

    void RHIVkFactory::ReleaseFence(RHIFenceHandle fenceHandle)
    {
        m_Device->ReleaseFence(fenceHandle);
    }
}





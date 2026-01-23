#include "RHIVkDevice.h"

#include "RHIVkFactory.h"
#include "Logger/Logger.h"
#include "Windows/RenderWindowAPI.h"
#include "../Utils/RHIVkDeferredDeletion.h"
#include "../Queues/RHIVkQueue.h"
#include "../Handles/RHIVkResourcePools.h"
#include "../Memory/RHIVkMemoryAllocator.h"
#include "../RHIVkInstance.h"
#include "../VkInitializer.h"
#include "../Program/RHIVkBindlessManager.h"
#include "../Program/RHIVkGPURenderPass.h"
using namespace ArisenEngine::RHI;


ArisenEngine::RHI::RHIVkDevice::RHIVkDevice(RHIInstance* instance, Surface* surface, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device, VkPhysicalDeviceMemoryProperties memoryProperties, UInt32 graphicsFamilyIndex)
: RHIDevice(instance, surface), m_VkGraphicQueue(graphicQueue), m_VkPresentQueue(presentQueue), m_VkDevice(device), m_GraphicsFamilyIndex(graphicsFamilyIndex), m_VkPhysicalDeviceMemoryProperties(memoryProperties)
{
    std::cout << "[DEBUG] RHIVkDevice::RHIVkDevice START" << std::endl;
    m_GPUPipelineManager = new RHIVkGPUPipelineManager(this, m_Instance->GetMaxFramesInFlight());
    m_DescriptorPool = new RHIVkDescriptorPool(this);
    
    // Cache function pointers for Sync 2.0 and Dynamic Rendering
    vkCmdPipelineBarrier2KHR = (PFN_vkCmdPipelineBarrier2KHR)vkGetDeviceProcAddr(m_VkDevice, "vkCmdPipelineBarrier2KHR");
    vkCmdBeginRenderingKHR = (PFN_vkCmdBeginRenderingKHR)vkGetDeviceProcAddr(m_VkDevice, "vkCmdBeginRenderingKHR");
    vkCmdEndRenderingKHR = (PFN_vkCmdEndRenderingKHR)vkGetDeviceProcAddr(m_VkDevice, "vkCmdEndRenderingKHR");

    auto* vkInstance = static_cast<RHIVkInstance*>(m_Instance);
    m_MemoryAllocator = new RHIVkMemoryAllocator(this, vkInstance->GetVkInstance(), vkInstance->GetPhysicalDevice(), m_VkDevice, VK_API_VERSION_1_2);
    
    m_BindlessManager = new RHIVkBindlessManager(this);
    m_BindlessManager->Initialize();

    m_Factory = new RHIVkFactory(this);
    m_DeferredDeletion = std::make_unique<RHIVkDeferredDeletion>(m_Instance->GetMaxFramesInFlight());
    m_ResourceRegistry = std::make_unique<RHIResourceRegistry>(m_DeferredDeletion.get());
    m_GraphicsQueue = std::make_unique<RHIVkQueue>(m_VkDevice, m_VkGraphicQueue, RHIQueueType::Graphics, m_DeferredDeletion.get(), m_ResourceRegistry.get());

    const UInt32 maxFramesInFlight = m_Instance->GetMaxFramesInFlight();
    m_FrameSync = std::make_unique<FrameSyncTracker>(maxFramesInFlight);

    m_BufferPool = std::make_unique<RHIResourcePool<RHIBufferHandle, RHIVkBufferPoolItem>>();
    m_ImagePool = std::make_unique<RHIResourcePool<RHIImageHandle, RHIVkImagePoolItem>>();
    m_ImageViewPool = std::make_unique<RHIResourcePool<RHIImageViewHandle, RHIVkImageViewPoolItem>>();
    m_SamplerPool = std::make_unique<RHIResourcePool<RHISamplerHandle, RHIVkSamplerPoolItem>>();
    m_RenderPassPool = std::make_unique<RHIResourcePool<RHIRenderPassHandle, RHIVkRenderPassPoolItem>>();
    m_FrameBufferPool = std::make_unique<RHIResourcePool<RHIFrameBufferHandle, RHIVkFrameBufferPoolItem>>();
    m_SemaphorePool = std::make_unique<RHIResourcePool<RHISemaphoreHandle, RHIVkSemaphorePoolItem>>();
    m_PipelinePool = std::make_unique<RHIResourcePool<RHIPipelineHandle, RHIVkPipelinePoolItem>>();
    m_FencePool = std::make_unique<RHIResourcePool<RHIFenceHandle, RHIVkFencePoolItem>>();
    m_GPUProgramPool = std::make_unique<RHIResourcePool<RHIGPUProgramHandle, RHIVkGPUProgramPoolItem>>();
    m_CommandBufferPoolPool = std::make_unique<RHIResourcePool<RHICommandBufferPoolHandle, RHIVkCommandBufferPoolItem>>();
}

ArisenEngine::RHI::RHIFactory* ArisenEngine::RHI::RHIVkDevice::GetFactory() const
{
    return m_Factory;
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::GetMaxFramesInFlight() const
{
    return m_Instance->GetMaxFramesInFlight();
}

ArisenEngine::RHI::RHIMemoryAllocator* ArisenEngine::RHI::RHIVkDevice::GetMemoryAllocator() const
{
    return m_MemoryAllocator;
}

void ArisenEngine::RHI::RHIVkDevice::DeviceWaitIdle() const
{
    vkDeviceWaitIdle(m_VkDevice);
}

void ArisenEngine::RHI::RHIVkDevice::GraphicQueueWaitIdle() const
{
    vkQueueWaitIdle(m_VkGraphicQueue);
}


void ArisenEngine::RHI::RHIVkDevice::EnqueueDeferredDestroy(RHIGpuTicket ticket, RHIDeferredDeleteItem item)
{
    if (m_DeferredDeletion)
    {
        m_DeferredDeletion->Enqueue(RHIQueueType::Graphics, ticket, item);
    }
}

namespace
{
    struct DeferredCallItem
    {
        std::function<void()> fn;
    };
    static void DeferredCallDeleter(void* p)
    {
        auto* item = static_cast<DeferredCallItem*>(p);
        if (item && item->fn) item->fn();
        delete item;
    }
}

void ArisenEngine::RHI::RHIVkDevice::EnqueueDeferredDestroy(RHIGpuTicket ticket, std::function<void()>&& fn)
{
    auto* item = new DeferredCallItem{ std::move(fn) };
    EnqueueDeferredDestroy(ticket, RHIDeferredDeleteItem{ item, &DeferredCallDeleter });
}

void ArisenEngine::RHI::RHIVkDevice::FlushDeferredDestroys(RHIGpuTicket ticket)
{
    if (m_DeferredDeletion)
    {
        m_DeferredDeletion->Flush(RHIQueueType::Graphics, ticket);
    }
}



ArisenEngine::RHI::RHIGpuTicket ArisenEngine::RHI::RHIVkDevice::GetCompletedSubmitTicket() const
{
    return m_GraphicsQueue ? m_GraphicsQueue->GetCompletedTicket() : 0;
}


RHIGpuTicket ArisenEngine::RHI::RHIVkDevice::Submit(RHICommandBuffer* commandBuffer, UInt32 frameIndex)
{
    ASSERT(commandBuffer->ReadyForSubmit());

    std::lock_guard<std::mutex> lock(m_SubmitMutex);
    m_CurrentFrameIndex.store(frameIndex, std::memory_order_release);
    if (m_GraphicsQueue)
    {
        RHIGpuTicket submitTicket = 0;
        if (auto* vkQueue = dynamic_cast<RHIVkQueue*>(m_GraphicsQueue.get()))
        {
            submitTicket = vkQueue->Submit(commandBuffer);
        }
        else
        {
             // Fallback: queue-managed fence via IRHIQueue.
             submitTicket = m_GraphicsQueue->Submit(commandBuffer);
        }

        if (m_FrameSync)
        {
            m_FrameSync->OnSubmit(frameIndex, submitTicket);
        }
        return submitTicket;
    }
    else
    {
        LOG_FATAL_AND_THROW("[RHIVkDevice::Submit]: graphics queue not initialized!");
        return 0;
    }
}

ArisenEngine::RHI::RHIFenceHandle ArisenEngine::RHI::RHIVkDevice::GetFrameFence(UInt32 frameIndex)
{
    (void)frameIndex;
    // If using timeline semaphores, we might not have a traditional per-frame fence.
    // Return invalid for now or we could wrap the timeline progress as a "handle".
    return RHIFenceHandle::Invalid();
}

void ArisenEngine::RHI::RHIVkDevice::WaitFrameFence(UInt32 frameIndex)
{
    if (m_FrameSync == nullptr || m_GraphicsQueue == nullptr)
    {
        return;
    }
    m_FrameSync->Wait(frameIndex, m_GraphicsQueue.get());
}

void ArisenEngine::RHI::RHIVkDevice::WaitQueueTicket(RHIGpuTicket ticket)
{
    if (m_GraphicsQueue)
    {
        m_GraphicsQueue->WaitForTicket(ticket);
    }
}

void ArisenEngine::RHI::RHIVkDevice::ResetFrameFence(UInt32 frameIndex)
{
    (void)frameIndex;
}

ArisenEngine::RHI::IRHIQueue* ArisenEngine::RHI::RHIVkDevice::GetQueue(RHIQueueType type)
{
    if (type == RHIQueueType::Graphics)
    {
        return m_GraphicsQueue.get();
    }
    return nullptr;
}

void ArisenEngine::RHI::RHIVkDevice::DeferredDelete(RHIQueueType queue, RHIGpuTicket ticket, RHIDeferredDeleteItem item)
{
    if (m_DeferredDeletion)
    {
        m_DeferredDeletion->Enqueue(queue, ticket, item);
        return;
    }
    // Fallback (should be rare)
    if (item.deleter && item.ptr) item.deleter(item.ptr);
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::FindMemoryType(UInt32 typeFilter, UInt32 properties)
{
    for (uint32_t i = 0; i < m_VkPhysicalDeviceMemoryProperties.memoryTypeCount; ++i)
    {
        if ((typeFilter & (1 << i)) && (m_VkPhysicalDeviceMemoryProperties.memoryTypes[i].propertyFlags & properties) == properties)
        {
            return i;
        }
       
    }

    LOG_FATAL("[RHIVkDevice::FindMemoryType]: failed to find suitable memory type!");
    return -1;
}

void ArisenEngine::RHI::RHIVkDevice::SetResolution(UInt32 width, UInt32 height)
{
    m_Instance->UpdateSurfaceCapabilities(m_Surface);
    m_Surface->GetSwapChain()->SetResolution(width, height);
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::RegisterBindlessResource(RHIImageViewHandle image)
{
    return m_BindlessManager->RegisterImage(image);
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::RegisterBindlessResource(RHIBufferHandle buffer)
{
    return m_BindlessManager->RegisterBuffer(buffer);
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::RegisterBindlessResource(RHISamplerHandle sampler)
{
    return m_BindlessManager->RegisterSampler(sampler);
}

// --- Handle-based Buffer Operations ---

bool ArisenEngine::RHI::RHIVkDevice::AllocBuffer(RHIBufferHandle handle, BufferDescriptor&& desc)
{
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer) return false;

    auto bufferInfo = BufferCreateInfo(
        desc.createFlagBits,
        desc.size,
        desc.usage,
        desc.sharingMode,
        desc.queueFamilyIndexCount,
        (const uint32_t*)desc.pQueueFamilyIndices);

    buffer->size = desc.size;
    buffer->range = desc.size;

    if (vkCreateBuffer(m_VkDevice, &bufferInfo, nullptr, &buffer->buffer) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDevice::AllocBuffer]: failed to create buffer!");
        return false;
    }

    // Register for deferred deletion using a shared state object
    buffer->state = new RHIVkBufferState();
    buffer->state->device = m_VkDevice;
    buffer->state->buffer = buffer->buffer;
    buffer->state->allocator = m_MemoryAllocator->GetVmaAllocator();
    
    buffer->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(buffer->state));

    return true;
}

bool ArisenEngine::RHI::RHIVkDevice::AllocBufferDeviceMemory(RHIBufferHandle handle, UInt32 memoryPropertiesBits)
{
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer || buffer->buffer == VK_NULL_HANDLE || !buffer->state) return false;

    VmaMemoryUsage usage = VMA_MEMORY_USAGE_AUTO;
    if (memoryPropertiesBits & VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT) {
        usage = VMA_MEMORY_USAGE_GPU_ONLY;
    } else if (memoryPropertiesBits & VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT) {
        usage = VMA_MEMORY_USAGE_CPU_ONLY;
    }

    VmaAllocation newAlloc = VK_NULL_HANDLE;
    if (!m_MemoryAllocator->AllocateBufferMemory(buffer->buffer, usage, &newAlloc)) {
        return false;
    }

    // If there was an old allocation, queue it for individual deletion
    if (buffer->state->allocation != VK_NULL_HANDLE) {
        EnqueueDeferredDestroy(m_GraphicsQueue->GetLatestTicket(), [allocator = buffer->state->allocator, oldAlloc = buffer->state->allocation]() {
            if (allocator != VK_NULL_HANDLE && oldAlloc != VK_NULL_HANDLE) {
                vmaFreeMemory(allocator, oldAlloc);
            }
        });
    }

    buffer->state->allocation = newAlloc;
    buffer->allocation = newAlloc; // Sync cache
    
    return true;
}

void ArisenEngine::RHI::RHIVkDevice::FreeBufferInternal(RHIBufferHandle handle)
{
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer) return;

    if (buffer->buffer != VK_NULL_HANDLE)
    {
        m_ResourceRegistry->Release(buffer->registryHandle, RHIQueueType::Graphics, GetQueue(RHIQueueType::Graphics)->GetLatestTicket());
        
        buffer->buffer = VK_NULL_HANDLE;
        buffer->allocation = VK_NULL_HANDLE;
        buffer->state = nullptr;
        buffer->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseBuffer(RHIBufferHandle handle)
{
    FreeBufferInternal(handle);
    auto* buffer = m_BufferPool->Deallocate(handle);
    if (buffer)
    {
        EnqueueDeferredDestroy(GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [buffer]()
        {
            delete buffer;
        });
    }
}

void ArisenEngine::RHI::RHIVkDevice::BufferMemoryCopy(RHIBufferHandle handle, const void* src, UInt32 offset)
{
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer || buffer->allocation == VK_NULL_HANDLE) return;

    void* mappedData;
    if (vmaMapMemory(m_MemoryAllocator->GetVmaAllocator(), buffer->allocation, &mappedData) == VK_SUCCESS)
    {
        memcpy((uint8_t*)mappedData + offset, src, buffer->size);
        vmaUnmapMemory(m_MemoryAllocator->GetVmaAllocator(), buffer->allocation);
    }
}

ArisenEngine::UInt64 ArisenEngine::RHI::RHIVkDevice::GetBufferSize(RHIBufferHandle handle)
{
    auto* buffer = m_BufferPool->Get(handle);
    return buffer ? buffer->size : 0ULL;
}

ArisenEngine::UInt64 ArisenEngine::RHI::RHIVkDevice::GetBufferOffset(RHIBufferHandle handle)
{
    auto* buffer = m_BufferPool->Get(handle);
    return buffer ? buffer->offset : 0ULL;
}

ArisenEngine::UInt64 ArisenEngine::RHI::RHIVkDevice::GetBufferRange(RHIBufferHandle handle)
{
    auto* buffer = m_BufferPool->Get(handle);
    return buffer ? buffer->range : 0ULL;
}

bool ArisenEngine::RHI::RHIVkDevice::AllocImage(RHIImageHandle handle, ImageDescriptor&& desc)
{
    auto* image = m_ImagePool->Get(handle);
    if (!image) return false;

    auto imageInfo = ImageCreateInfo(
        desc.imageType,
        desc.width, desc.height, desc.depth,
        desc.mipLevels, desc.arrayLayers,
        desc.format, desc.tiling,
        desc.imageLayout, desc.usage,
        desc.sampleCount, desc.sharingMode,
        desc.queueFamilyIndexCount,
        (const uint32_t*)desc.pQueueFamilyIndices);

    if (vkCreateImage(m_VkDevice, &imageInfo, nullptr, &image->image) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDevice::AllocImage]: failed to create image!");
        return false;
    }

    image->needDestroy = true;

    // Register for deferred deletion using a shared state object
    image->state = new RHIVkImageState();
    image->state->device = m_VkDevice;
    image->state->image = image->image;
    image->state->allocator = m_MemoryAllocator->GetVmaAllocator();
    
    image->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(image->state));

    return true;
}

bool ArisenEngine::RHI::RHIVkDevice::AllocImageDeviceMemory(RHIImageHandle handle, UInt32 memoryPropertiesBits)
{
    auto* image = m_ImagePool->Get(handle);
    if (!image || image->image == VK_NULL_HANDLE || !image->state) return false;

    VmaMemoryUsage usage = VMA_MEMORY_USAGE_AUTO;
    if (memoryPropertiesBits & VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT) {
        usage = VMA_MEMORY_USAGE_GPU_ONLY;
    }

    VmaAllocation newAlloc = VK_NULL_HANDLE;
    if (!m_MemoryAllocator->AllocateImageMemory(image->image, usage, &newAlloc)) {
        return false;
    }

    // If there was an old allocation, queue it for individual deletion
    if (image->state->allocation != VK_NULL_HANDLE) {
        EnqueueDeferredDestroy(m_GraphicsQueue->GetLatestTicket(), [allocator = image->state->allocator, oldAlloc = image->state->allocation]() {
            if (allocator != VK_NULL_HANDLE && oldAlloc != VK_NULL_HANDLE) {
                vmaFreeMemory(allocator, oldAlloc);
            }
        });
    }

    image->state->allocation = newAlloc;
    image->allocation = newAlloc; // Sync cache
    return true;
}

void ArisenEngine::RHI::RHIVkDevice::FreeImageInternal(RHIImageHandle handle)
{
    auto* image = m_ImagePool->Get(handle);
    if (!image) return;

    if (image->image != VK_NULL_HANDLE && image->needDestroy)
    {
        m_ResourceRegistry->Release(image->registryHandle, RHIQueueType::Graphics, GetQueue(RHIQueueType::Graphics)->GetLatestTicket());
        
        image->image = VK_NULL_HANDLE;
        image->allocation = VK_NULL_HANDLE;
        image->state = nullptr;
        image->registryHandle = RHIResourceHandle::Invalid();
        image->needDestroy = false;
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseImage(RHIImageHandle handle)
{
    FreeImageInternal(handle);
    auto* image = m_ImagePool->Deallocate(handle);
    if (image)
    {
        EnqueueDeferredDestroy(GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [image]()
        {
            delete image;
        });
    }
}

bool ArisenEngine::RHI::RHIVkDevice::AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle, ImageViewDesc&& desc)
{
    auto* viewItem = m_ImageViewPool->Get(handle);
    auto* imageItem = m_ImagePool->Get(imageHandle);
    if (!viewItem || !imageItem || imageItem->image == VK_NULL_HANDLE) return false;

    auto viewInfo = ImageViewCreateInfo(
        imageItem->image, desc.viewType, desc.format, desc.aspectMask,
        desc.baseMipLevel, desc.levelCount, desc.baseArrayLayer, desc.layerCount);

    if (vkCreateImageView(m_VkDevice, &viewInfo, nullptr, &viewItem->view) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDevice::AllocImageView]: failed to create image view!");
        return false;
    }

    viewItem->format = desc.format;
    viewItem->imageHandle = imageHandle;
    viewItem->width = desc.width.value_or(0);
    viewItem->height = desc.height.value_or(0);

    // Register for deferred deletion
    struct DeferredVkImageView {
        VkDevice device;
        VkImageView view;
        ~DeferredVkImageView() {
            if (device != VK_NULL_HANDLE && view != VK_NULL_HANDLE) {
                vkDestroyImageView(device, view, nullptr);
            }
        }
    };
    auto* deferred = new DeferredVkImageView{ m_VkDevice, viewItem->view };
    viewItem->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(deferred));

    return true;
}

void ArisenEngine::RHI::RHIVkDevice::FreeImageViewInternal(RHIImageViewHandle handle)
{
    auto* viewItem = m_ImageViewPool->Get(handle);
    if (!viewItem) return;

    if (viewItem->view != VK_NULL_HANDLE)
    {
        m_ResourceRegistry->Release(viewItem->registryHandle, RHIQueueType::Graphics, GetQueue(RHIQueueType::Graphics)->GetLatestTicket());
        viewItem->view = VK_NULL_HANDLE;
        viewItem->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseImageView(RHIImageViewHandle handle)
{
    FreeImageViewInternal(handle);
    auto* view = m_ImageViewPool->Deallocate(handle);
    if (view)
    {
        EnqueueDeferredDestroy(GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [view]()
        {
            delete view;
        });
    }
}

ArisenEngine::RHI::RHIImageViewHandle ArisenEngine::RHI::RHIVkDevice::FindImageViewForImage(RHIImageHandle imageHandle)
{
    return m_ImageViewPool->FindHandle([imageHandle](const RHIVkImageViewPoolItem& item) {
        return item.imageHandle == imageHandle;
    });
}

void ArisenEngine::RHI::RHIVkDevice::FreeSamplerInternal(RHISamplerHandle handle)
{
    auto* sampler = m_SamplerPool->Get(handle);
    if (sampler && sampler->sampler != VK_NULL_HANDLE)
    {
        m_ResourceRegistry->Release(sampler->registryHandle, RHIQueueType::Graphics, GetQueue(RHIQueueType::Graphics)->GetLatestTicket());
        sampler->sampler = VK_NULL_HANDLE;
        sampler->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseSampler(RHISamplerHandle handle)
{
    FreeSamplerInternal(handle);
    auto* sampler = m_SamplerPool->Deallocate(handle);
    if (sampler)
    {
        EnqueueDeferredDestroy(GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [sampler]()
        {
            delete sampler;
        });
    }
}

void ArisenEngine::RHI::RHIVkDevice::FreeSemaphoreInternal(RHISemaphoreHandle handle)
{
    auto* sem = m_SemaphorePool->Get(handle);
    if (sem && sem->semaphore != VK_NULL_HANDLE)
    {
        m_ResourceRegistry->Release(sem->registryHandle, RHIQueueType::Graphics, GetQueue(RHIQueueType::Graphics)->GetLatestTicket());
        sem->semaphore = VK_NULL_HANDLE;
        sem->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseSemaphore(RHISemaphoreHandle handle)
{
    FreeSemaphoreInternal(handle);
    auto* sem = m_SemaphorePool->Deallocate(handle);
    if (sem)
    {
        EnqueueDeferredDestroy(GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [sem]()
        {
            delete sem;
        });
    }
}

void ArisenEngine::RHI::RHIVkDevice::FreeFenceInternal(RHIFenceHandle handle)
{
    auto* f = m_FencePool->Get(handle);
    if (f && f->fence != VK_NULL_HANDLE)
    {
        m_ResourceRegistry->Release(f->registryHandle, RHIQueueType::Graphics, GetQueue(RHIQueueType::Graphics)->GetLatestTicket());
        f->fence = VK_NULL_HANDLE;
        f->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseFence(RHIFenceHandle handle)
{
    FreeFenceInternal(handle);
    auto* f = m_FencePool->Deallocate(handle);
    if (f)
    {
        EnqueueDeferredDestroy(GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [f]()
        {
            delete f;
        });
    }
}

void ArisenEngine::RHI::RHIVkDevice::FreeRenderPassInternal(RHIRenderPassHandle handle)
{
    auto* rp = m_RenderPassPool->Get(handle);
    if (rp && rp->registryHandle.IsValid())
    {
        m_ResourceRegistry->Release(rp->registryHandle, RHIQueueType::Graphics, GetQueue(RHIQueueType::Graphics)->GetLatestTicket());
        rp->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseRenderPass(RHIRenderPassHandle handle)
{
    FreeRenderPassInternal(handle);
    auto* rp = m_RenderPassPool->Deallocate(handle);
    if (rp)
    {
        EnqueueDeferredDestroy(GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [rp]()
        {
            delete rp;
        });
    }
}

void ArisenEngine::RHI::RHIVkDevice::FreeFrameBufferInternal(RHIFrameBufferHandle handle)
{
    auto* fb = m_FrameBufferPool->Get(handle);
    if (fb && fb->registryHandle.IsValid())
    {
        m_ResourceRegistry->Release(fb->registryHandle, RHIQueueType::Graphics, GetQueue(RHIQueueType::Graphics)->GetLatestTicket());
        fb->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseFrameBuffer(RHIFrameBufferHandle handle)
{
    FreeFrameBufferInternal(handle);
    auto* fb = m_FrameBufferPool->Deallocate(handle);
    if (fb)
    {
        EnqueueDeferredDestroy(GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [fb]()
        {
            delete fb;
        });
    }
}

void ArisenEngine::RHI::RHIVkDevice::FreePipelineInternal(RHIPipelineHandle handle)
{
    auto* p = m_PipelinePool->Get(handle);
    if (p && p->registryHandle.IsValid())
    {
        m_ResourceRegistry->Release(p->registryHandle, RHIQueueType::Graphics, GetQueue(RHIQueueType::Graphics)->GetLatestTicket());
        p->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleasePipeline(RHIPipelineHandle handle)
{
    FreePipelineInternal(handle);
    auto* p = m_PipelinePool->Deallocate(handle);
    if (p)
    {
        EnqueueDeferredDestroy(GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [p]()
        {
            delete p;
        });
    }
}

ArisenEngine::RHI::RHIVkDevice::~RHIVkDevice() noexcept
{
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Start destruction");
    // 1. Wait for GPU to be idle
    DeviceWaitIdle();

    // 2. Drain FrameSync to ensure all submitted work is tracked as completed
    if (m_FrameSync && m_GraphicsQueue)
    {
        m_FrameSync->Drain(m_GraphicsQueue.get());
    }

    // 3. Destroy the Resource Registry first to ensure all remaining resources are enqueued for deferred destruction.
    // This triggers ~RHIResourceRegistry() which enqueues everything that wasn't explicitly released.
    m_ResourceRegistry.reset();
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Resource Registry destroyed, remaining resources enqueued");

    // 4. Flush all deferred deletions now that we know the GPU is idle and all tickets are completed.
    if (m_DeferredDeletion)
    {
        LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Flushing deferred deletions");
        constexpr RHIGpuTicket kAll = ~static_cast<RHIGpuTicket>(0);
        
        m_DeferredDeletion->Flush(RHIQueueType::Graphics, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Compute, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Transfer, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Present, kAll);
    }

    // 5. Destroy managers that might rely on the device still being alive
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Deleting managers");
    if (m_GPUPipelineManager) { delete m_GPUPipelineManager; m_GPUPipelineManager = nullptr; }
    if (m_BindlessManager) { delete m_BindlessManager; m_BindlessManager = nullptr; }
    if (m_DescriptorPool) { delete m_DescriptorPool; m_DescriptorPool = nullptr; }
    
    // IMPORTANT: Memory allocator must be deleted AFTER all resources that might use it are flushed.
    if (m_MemoryAllocator) { delete m_MemoryAllocator; m_MemoryAllocator = nullptr; }
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: m_MemoryAllocator deleted");
    
    if (m_Factory) { delete m_Factory; m_Factory = nullptr; }
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: m_Factory deleted");

    // 5. Clean up sync and queue objects
    m_FrameSync.reset();
    m_GraphicsQueue.reset();
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Sync and Queue objects reset");

    // 6. Finally destroy the Vulkan device
    if (m_VkDevice != VK_NULL_HANDLE)
    {
        vkDestroyDevice(m_VkDevice, nullptr);
        m_VkDevice = VK_NULL_HANDLE;
        LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: vkDestroyDevice called");
    }
    
    m_Instance = nullptr;
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Finished destruction");
}

bool ArisenEngine::RHI::RHIVkDevice::AllocFrameBuffer(RHIFrameBufferHandle handle, UInt32 frameIndex, RHIImageViewHandle viewHandle, RHIRenderPassHandle renderPassHandle)
{
    auto* fbItem = m_FrameBufferPool->Get(handle);
    auto* viewItem = m_ImageViewPool->Get(viewHandle);
    auto* rpItem = m_RenderPassPool->Get(renderPassHandle);

    if (!fbItem || !viewItem || !rpItem) return false;

    auto* rpObj = static_cast<RHIVkGPURenderPass*>(rpItem->renderPassObj);
    if (!rpObj) return false;

    VkImageView attachments[] = { viewItem->view };

    VkFramebufferCreateInfo framebufferInfo{};
    framebufferInfo.sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO;
    framebufferInfo.renderPass = static_cast<VkRenderPass>(rpObj->GetHandle(frameIndex));
    framebufferInfo.attachmentCount = 1;
    framebufferInfo.pAttachments = attachments;
    framebufferInfo.width = viewItem->width;
    framebufferInfo.height = viewItem->height;
    framebufferInfo.layers = 1;

    if (vkCreateFramebuffer(m_VkDevice, &framebufferInfo, nullptr, &fbItem->frameBuffer) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDevice::AllocFrameBuffer]: failed to create framebuffer!");
        return false;
    }

    fbItem->width = viewItem->width;
    fbItem->height = viewItem->height;

    // Register for deferred deletion
    struct DeferredVkFramebuffer {
        VkDevice device;
        VkFramebuffer framebuffer;
        ~DeferredVkFramebuffer() {
            if (device != VK_NULL_HANDLE && framebuffer != VK_NULL_HANDLE) {
                vkDestroyFramebuffer(device, framebuffer, nullptr);
            }
        }
    };
    auto* deferred = new DeferredVkFramebuffer{ m_VkDevice, fbItem->frameBuffer };
    fbItem->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(deferred));

    return true;
}

void ArisenEngine::RHI::RHIVkDevice::WaitFence(RHIFenceHandle handle)
{
    auto* f = m_FencePool->Get(handle);
    if (f && f->fence != VK_NULL_HANDLE)
    {
        vkWaitForFences(m_VkDevice, 1, &f->fence, VK_TRUE, UINT64_MAX);
    }
}

void ArisenEngine::RHI::RHIVkDevice::ResetFence(RHIFenceHandle handle)
{
    auto* f = m_FencePool->Get(handle);
    if (f && f->fence != VK_NULL_HANDLE)
    {
        vkResetFences(m_VkDevice, 1, &f->fence);
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseGPUProgram(RHIGPUProgramHandle handle)
{
    auto* item = m_GPUProgramPool->Get(handle);
    if (item)
    {
        if (item->registryHandle.IsValid())
            m_ResourceRegistry->Release(item->registryHandle, RHIQueueType::Graphics, GetQueue(RHIQueueType::Graphics)->GetLatestTicket());
            
        m_GPUProgramPool->Deallocate(handle);
        // Note: The object deletion is handled by Deferred Deleter registered during creation in Factory
        // But here we are just cleaning up the pool slot.
        EnqueueDeferredDestroy(GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [item]()
        {
            delete item;
        });
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseCommandBufferPool(RHICommandBufferPoolHandle handle)
{
    auto* item = m_CommandBufferPoolPool->Get(handle);
    if (item)
    {
        if (item->registryHandle.IsValid())
            m_ResourceRegistry->Release(item->registryHandle, RHIQueueType::Graphics, GetQueue(RHIQueueType::Graphics)->GetLatestTicket());
            
        m_CommandBufferPoolPool->Deallocate(handle);
        EnqueueDeferredDestroy(GetQueue(RHIQueueType::Graphics)->GetLatestTicket(), [item]()
        {
            delete item;
        });
    }
}




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

void ArisenEngine::RHI::RHIVkDevice::Update()
{
    if (m_GraphicsQueue)
    {
        m_GraphicsQueue->Update();
    }
}

ArisenEngine::RHI::RHIGpuTicket ArisenEngine::RHI::RHIVkDevice::GetCompletedSubmitId() const
{
    return m_GraphicsQueue ? m_GraphicsQueue->GetCompletedTicket() : 0;
}


void ArisenEngine::RHI::RHIVkDevice::Submit(RHICommandBuffer* commandBuffer, UInt32 frameIndex)
{
    ASSERT(commandBuffer->ReadyForSubmit());

    std::lock_guard<std::mutex> lock(m_SubmitMutex);
    m_CurrentFrameIndex.store(frameIndex, std::memory_order_release);
    if (m_GraphicsQueue)
    {
        if (auto* vkQueue = dynamic_cast<RHIVkQueue*>(m_GraphicsQueue.get()))
        {
            const auto submitId = vkQueue->Submit(commandBuffer);
            if (m_FrameSync)
            {
                m_FrameSync->OnSubmit(frameIndex, submitId);
            }
            return;
        }

        // Fallback: queue-managed fence via IRHIQueue.
        const auto submitId = m_GraphicsQueue->Submit(commandBuffer);
        if (m_FrameSync)
        {
            m_FrameSync->OnSubmit(frameIndex, submitId);
        }
    }
    else
    {
        LOG_FATAL_AND_THROW("[RHIVkDevice::Submit]: graphics queue not initialized!");
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

    if (vkCreateBuffer(m_VkDevice, &bufferInfo, nullptr, &buffer->buffer) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDevice::AllocBuffer]: failed to create buffer!");
        return false;
    }

    // Register for deferred deletion
    struct DeferredVkBuffer {
        VkDevice device;
        VkBuffer buffer;
        VmaAllocator allocator;
        VmaAllocation allocation;
        ~DeferredVkBuffer() {
            if (device != VK_NULL_HANDLE && buffer != VK_NULL_HANDLE) {
                vkDestroyBuffer(device, buffer, nullptr);
            }
            if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE) {
                vmaFreeMemory(allocator, allocation);
            }
        }
    };
    auto* deferred = new DeferredVkBuffer{ m_VkDevice, buffer->buffer, m_MemoryAllocator->GetVmaAllocator(), VK_NULL_HANDLE };
    buffer->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(deferred));

    return true;
}

bool ArisenEngine::RHI::RHIVkDevice::AllocBufferDeviceMemory(RHIBufferHandle handle, UInt32 memoryPropertiesBits)
{
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer || buffer->buffer == VK_NULL_HANDLE) return false;

    // If we are re-allocating memory, the OLD allocation must be part of the deferred deletion
    // However, since we currently bundle buffer + allocation in one registry entry, 
    // it's better to just update the existing deferred object if it hasn't been queued yet.
    // BUT the registry object is created at AllocBuffer.
    
    // For simplicity and safety: 
    // We should probably allow the pool item to track its current allocation, 
    // and when the registry entry is finally executed, it cleans up whatever was registered.
    
    // Re-getting the deferred object from registry is hard. 
    // Correct way: The registry entry ONLY handles the buffer. 
    // We create a separate registry entry for the allocation if needed, or bundle them better.
    
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

    // Capture the allocation to be freed later
    struct DeferredVmaAllocation {
        VmaAllocator allocator;
        VmaAllocation allocation;
        ~DeferredVmaAllocation() {
            if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE) {
                vmaFreeMemory(allocator, allocation);
            }
        }
    };
    
    // If there was an old allocation, queue it for deletion
    if (buffer->allocation != VK_NULL_HANDLE) {
        auto* oldAllocDeleter = new DeferredVmaAllocation{ m_MemoryAllocator->GetVmaAllocator(), buffer->allocation };
        m_ResourceRegistry->Create(MakeDeferredDeleteItem(oldAllocDeleter));
    }

    buffer->allocation = newAlloc;
    
    // Also track this NEW allocation for deletion when the buffer itself is released
    auto* bufAllocDeleter = new DeferredVmaAllocation{ m_MemoryAllocator->GetVmaAllocator(), buffer->allocation };
    // This is a bit redundant but ensures that even if FreeBuffer is called, it gets cleaned up.
    // Actually, let's just make sure FreeBuffer handles it.
    
    return true;
}

void ArisenEngine::RHI::RHIVkDevice::FreeBuffer(RHIBufferHandle handle)
{
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer) return;

    if (buffer->buffer != VK_NULL_HANDLE)
    {
        // Allocation cleanup
        if (buffer->allocation != VK_NULL_HANDLE) {
            struct DeferredVmaAllocation {
                VmaAllocator allocator;
                VmaAllocation allocation;
                ~DeferredVmaAllocation() {
                    if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE) {
                        vmaFreeMemory(allocator, allocation);
                    }
                }
            };
            auto* allocDeleter = new DeferredVmaAllocation{ m_MemoryAllocator->GetVmaAllocator(), buffer->allocation };
            m_ResourceRegistry->Create(MakeDeferredDeleteItem(allocDeleter));
            buffer->allocation = VK_NULL_HANDLE;
        }

        m_ResourceRegistry->Release(buffer->registryHandle, RHIQueueType::Graphics, GetCompletedSubmitId());
        buffer->buffer = VK_NULL_HANDLE;
        buffer->registryHandle = RHIResourceHandle::Invalid();
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

    // Register for deferred deletion
    struct DeferredVkImage {
        VkDevice device;
        VkImage image;
        VmaAllocator allocator;
        VmaAllocation allocation;
        ~DeferredVkImage() {
            if (device != VK_NULL_HANDLE && image != VK_NULL_HANDLE) {
                vkDestroyImage(device, image, nullptr);
            }
            if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE) {
                vmaFreeMemory(allocator, allocation);
            }
        }
    };
    auto* deferred = new DeferredVkImage{ m_VkDevice, image->image, m_MemoryAllocator->GetVmaAllocator(), VK_NULL_HANDLE };
    image->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(deferred));

    return true;
}

bool ArisenEngine::RHI::RHIVkDevice::AllocImageDeviceMemory(RHIImageHandle handle, UInt32 memoryPropertiesBits)
{
    auto* image = m_ImagePool->Get(handle);
    if (!image || image->image == VK_NULL_HANDLE) return false;

    VmaMemoryUsage usage = VMA_MEMORY_USAGE_AUTO;
    if (memoryPropertiesBits & VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT) {
        usage = VMA_MEMORY_USAGE_GPU_ONLY;
    }

    VmaAllocation newAlloc = VK_NULL_HANDLE;
    if (!m_MemoryAllocator->AllocateImageMemory(image->image, usage, &newAlloc)) {
        return false;
    }

    struct DeferredVmaAllocation {
        VmaAllocator allocator;
        VmaAllocation allocation;
        ~DeferredVmaAllocation() {
            if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE) {
                vmaFreeMemory(allocator, allocation);
            }
        }
    };

    if (image->allocation != VK_NULL_HANDLE) {
        auto* oldAllocDeleter = new DeferredVmaAllocation{ m_MemoryAllocator->GetVmaAllocator(), image->allocation };
        m_ResourceRegistry->Create(MakeDeferredDeleteItem(oldAllocDeleter));
    }

    image->allocation = newAlloc;
    return true;
}

void ArisenEngine::RHI::RHIVkDevice::FreeImage(RHIImageHandle handle)
{
    auto* image = m_ImagePool->Get(handle);
    if (!image) return;

    if (image->image != VK_NULL_HANDLE && image->needDestroy)
    {
        if (image->allocation != VK_NULL_HANDLE) {
            struct DeferredVmaAllocation {
                VmaAllocator allocator;
                VmaAllocation allocation;
                ~DeferredVmaAllocation() {
                    if (allocator != VK_NULL_HANDLE && allocation != VK_NULL_HANDLE) {
                        vmaFreeMemory(allocator, allocation);
                    }
                }
            };
            auto* allocDeleter = new DeferredVmaAllocation{ m_MemoryAllocator->GetVmaAllocator(), image->allocation };
            m_ResourceRegistry->Create(MakeDeferredDeleteItem(allocDeleter));
            image->allocation = VK_NULL_HANDLE;
        }

        m_ResourceRegistry->Release(image->registryHandle, RHIQueueType::Graphics, GetCompletedSubmitId());
        image->image = VK_NULL_HANDLE;
        image->registryHandle = RHIResourceHandle::Invalid();
        image->needDestroy = false;
    }
}

bool ArisenEngine::RHI::RHIVkDevice::AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle, ImageViewDesc&& desc)
{
    auto* viewItem = m_ImageViewPool->Get(handle);
    auto* imageItem = m_ImagePool->Get(imageHandle);
    if (!viewItem || !imageItem || imageItem->image == VK_NULL_HANDLE) return false;

    auto viewInfo = ImageViewCreateInfo(
        imageItem->image, desc.viewType, desc.format,
        desc.baseMipLevel, desc.levelCount, desc.baseArrayLayer, desc.layerCount);

    if (vkCreateImageView(m_VkDevice, &viewInfo, nullptr, &viewItem->view) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDevice::AllocImageView]: failed to create image view!");
        return false;
    }

    viewItem->format = desc.format;
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

void ArisenEngine::RHI::RHIVkDevice::FreeImageView(RHIImageViewHandle handle)
{
    auto* viewItem = m_ImageViewPool->Get(handle);
    if (!viewItem) return;

    if (viewItem->view != VK_NULL_HANDLE)
    {
        m_ResourceRegistry->Release(viewItem->registryHandle, RHIQueueType::Graphics, GetCompletedSubmitId());
        viewItem->view = VK_NULL_HANDLE;
        viewItem->registryHandle = RHIResourceHandle::Invalid();
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

    // 3. Flush all deferred deletions now that we know the GPU is idle and all tickets are completed.
    if (m_DeferredDeletion)
    {
        constexpr RHIGpuTicket kAll = ~static_cast<RHIGpuTicket>(0);
        // Pass 1: Flush to destroy handle objects (like RHIVkImageHandle).
        // These might enqueue resource destruction (like DeferredVkImage) into the same queue.
        m_DeferredDeletion->Flush(RHIQueueType::Graphics, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Compute, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Transfer, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Present, kAll);

        // Pass 2: Flush to destroy the underlying Vulkan resources enqueued during Pass 1.
        m_DeferredDeletion->Flush(RHIQueueType::Graphics, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Compute, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Transfer, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Present, kAll);
    }

    // 4. Destroy managers that might rely on the device still being alive
    // 4. Destroy managers that might rely on the device still being alive
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Deleting managers");
    if (m_GPUPipelineManager) { delete m_GPUPipelineManager; m_GPUPipelineManager = nullptr; }
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: m_GPUPipelineManager deleted");
    if (m_BindlessManager) { delete m_BindlessManager; m_BindlessManager = nullptr; }
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: m_BindlessManager deleted");
    if (m_DescriptorPool) { delete m_DescriptorPool; m_DescriptorPool = nullptr; }
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: m_DescriptorPool deleted");
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



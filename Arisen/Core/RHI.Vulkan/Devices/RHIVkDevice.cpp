#include "RHIVkDevice.h"

#include "../Handles/RHIVkBufferHandle.h"
#include "../Handles/RHIVkImageHandle.h"
#include "Logger/Logger.h"
#include "Windows/RenderWindowAPI.h"
#include "../Utils/RHIVkDeferredDeletion.h"
#include "../Queues/RHIVkQueue.h"
#include "../Program/RHIVkSampler.h"

ArisenEngine::RHI::RHIVkDevice::RHIVkDevice(RHIInstance* instance, Surface* surface, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device, VkPhysicalDeviceMemoryProperties memoryProperties)
: RHIDevice(instance, surface), m_VkGraphicQueue(graphicQueue), m_VkPresentQueue(presentQueue), m_VkDevice(device), m_VkPhysicalDeviceMemoryProperties(memoryProperties)
{
    m_GPUPipelineManager = new RHIVkGPUPipelineManager(this, m_Instance->GetMaxFramesInFlight());
    m_DescriptorPool = new RHIVkDescriptorPool(this);
    m_DeferredDeletion = std::make_unique<RHIVkDeferredDeletion>(m_Instance->GetMaxFramesInFlight());
    m_ResourceRegistry = std::make_unique<RHIResourceRegistry>(m_DeferredDeletion.get());
    m_GraphicsQueue = std::make_unique<RHIVkQueue>(m_VkDevice, m_VkGraphicQueue, RHIQueueType::Graphics, m_DeferredDeletion.get(), m_ResourceRegistry.get());

    const UInt32 maxFramesInFlight = m_Instance->GetMaxFramesInFlight();
    m_FrameSync = std::make_unique<FrameSyncTracker>(maxFramesInFlight);
}

ArisenEngine::RHI::RHISampler* ArisenEngine::RHI::RHIVkDevice::CreateSampler(RHISamplerDesc&& desc)
{
    return new RHIVkSampler(this, std::move(desc));
}

void ArisenEngine::RHI::RHIVkDevice::DeviceWaitIdle() const
{
    vkDeviceWaitIdle(m_VkDevice);
}

void ArisenEngine::RHI::RHIVkDevice::GraphicQueueWaitIdle() const
{
    vkQueueWaitIdle(m_VkGraphicQueue);
}

ArisenEngine::RHI::GPUProgram* ArisenEngine::RHI::RHIVkDevice::CreateGPUProgram()
{
    ASSERT(m_VkDevice != VK_NULL_HANDLE);
    return new RHIVkGPUProgram(m_VkDevice);
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseGPUProgram(GPUProgram* program)
{
    if (program)
    {
        EnqueueDeferredDestroy(m_GraphicsQueue->GetLatestTicket(), [program]()
        {
            delete program;
        });
        LOG_INFO("[RHIVkDevice::ReleaseGPUProgram] Enqueued destroy for Program");
    }
}

bool ArisenEngine::RHI::RHIVkDevice::AttachProgramByteCode(GPUProgram* program, GPUProgramDesc&& desc)
{
    if (program)
    {
        return program->AttachProgramByteCode(std::move(desc));
    }
    return false;
}

ArisenEngine::RHI::RHICommandBufferPool* ArisenEngine::RHI::RHIVkDevice::CreateCommandBufferPool()
{
    ASSERT(m_VkDevice != VK_NULL_HANDLE);
    return new RHIVkCommandBufferPool(this, m_Instance->GetMaxFramesInFlight());
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseCommandBufferPool(RHICommandBufferPool* pool)
{
     if (pool)
    {
        EnqueueDeferredDestroy(m_GraphicsQueue->GetLatestTicket(), [pool]()
        {
            delete pool;
        });
        LOG_INFO("[RHIVkDevice::ReleaseCommandBufferPool] Enqueued destroy for Pool");
    }
}

ArisenEngine::RHI::GPURenderPass* ArisenEngine::RHI::RHIVkDevice::GetRenderPass()
{
    return new RHIVkGPURenderPass(this, m_Instance->GetMaxFramesInFlight());
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseRenderPass(GPURenderPass* renderPass)
{
    if (renderPass)
    {
        EnqueueDeferredDestroy(m_GraphicsQueue->GetLatestTicket(), [renderPass]()
        {
            delete renderPass;
        });
        LOG_INFO("[RHIVkDevice::ReleaseRenderPass] Enqueued destroy for RenderPass");
    }
}

ArisenEngine::RHI::FrameBuffer* ArisenEngine::RHI::RHIVkDevice::GetFrameBuffer()
{
    return new RHIVkFrameBuffer(this, m_Instance->GetMaxFramesInFlight());
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseFrameBuffer(FrameBuffer* frameBuffer)
{
    if (frameBuffer)
    {
        EnqueueDeferredDestroy(m_GraphicsQueue->GetLatestTicket(), [frameBuffer]()
        {
            delete frameBuffer;
        });
        LOG_INFO("[RHIVkDevice::ReleaseFrameBuffer] Enqueued destroy for FrameBuffer");
    }
}

ArisenEngine::RHI::BufferHandle* ArisenEngine::RHI::RHIVkDevice::GetBufferHandle(const std::string&& name)
{
    auto* bufferHandle = new RHIVkBufferHandle(this);
    bufferHandle->SetName(std::move(name));
    return bufferHandle;
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseBufferHandle(BufferHandle* bufferHandle)
{
   if (bufferHandle)
   {
       EnqueueDeferredDestroy(m_GraphicsQueue->GetLatestTicket(), [bufferHandle]()
       {
           delete bufferHandle;
       });
        LOG_INFO("[RHIVkDevice::ReleaseBufferHandle] Enqueued destroy for Buffer");
   }
}

ArisenEngine::RHI::ImageHandle* ArisenEngine::RHI::RHIVkDevice::GetImageHandle(const std::string&& name)
{
    auto* imageHandle = new RHIVkImageHandle(this);
    imageHandle->SetName(std::move(name));
    return imageHandle;
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseImageHandle(ImageHandle* imageHandle)
{
   if (imageHandle)
   {
       EnqueueDeferredDestroy(m_GraphicsQueue->GetLatestTicket(), [imageHandle]()
       {
           delete imageHandle;
       });
        LOG_INFO("[RHIVkDevice::ReleaseImageHandle] Enqueued destroy for Image");
   }
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

ArisenEngine::RHI::RHIFence* ArisenEngine::RHI::RHIVkDevice::GetFrameFence(UInt32 frameIndex)
{
    (void)frameIndex;
    return nullptr;
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

ArisenEngine::RHI::RHIVkDevice::~RHIVkDevice() noexcept
{
    LOG_INFO("[RHIVkDevice::~RHIVkDevice]: Start");
    
    // 1. Wait for GPU to be idle
    DeviceWaitIdle();
    LOG_INFO("[RHIVkDevice::~RHIVkDevice]: DeviceWaitIdle Done");

    // 2. Drain FrameSync to ensure all submitted work is tracked as completed
    if (m_FrameSync && m_GraphicsQueue)
    {
        LOG_INFO("[RHIVkDevice::~RHIVkDevice]: Draining FrameSync");
        m_FrameSync->Drain(m_GraphicsQueue.get());
        LOG_INFO("[RHIVkDevice::~RHIVkDevice]: Drain Done");
    }

    // 3. Flush all deferred deletions now that we know the GPU is idle and all tickets are completed.
    if (m_DeferredDeletion)
    {
        LOG_INFO("[RHIVkDevice::~RHIVkDevice]: Flushing Deferred Deletion");
        constexpr RHIGpuTicket kAll = ~static_cast<RHIGpuTicket>(0);
        
        LOG_INFO("[RHIVkDevice::~RHIVkDevice]: Flushing Graphics");
        m_DeferredDeletion->Flush(RHIQueueType::Graphics, kAll);
        
        LOG_INFO("[RHIVkDevice::~RHIVkDevice]: Flushing Compute");
        m_DeferredDeletion->Flush(RHIQueueType::Compute, kAll);
        
        LOG_INFO("[RHIVkDevice::~RHIVkDevice]: Flushing Transfer");
        m_DeferredDeletion->Flush(RHIQueueType::Transfer, kAll);
        
        LOG_INFO("[RHIVkDevice::~RHIVkDevice]: Flushing Present");
        m_DeferredDeletion->Flush(RHIQueueType::Present, kAll);
        
        LOG_INFO("[RHIVkDevice::~RHIVkDevice]: Flush Done");
    }

    // 4. Destroy managers that might rely on the device still being alive
    delete m_GPUPipelineManager;
    delete m_DescriptorPool;

    // 5. Clean up sync and queue objects
    m_FrameSync.reset();
    m_GraphicsQueue.reset();

    // 6. Finally destroy the Vulkan device
    LOG_INFO("[RHIVkDevice::~RHIVkDevice]: Destroying VkDevice");
    vkDestroyDevice(m_VkDevice, nullptr);
    LOG_DEBUG("## Destroy Vulkan Device ##");
    
    m_Instance = nullptr;
    LOG_INFO("[RHIVkDevice::~RHIVkDevice]: ~RHIVkDevice End");
}



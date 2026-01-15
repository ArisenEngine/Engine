#include "RHIVkDevice.h"

#include "../Handles/RHIVkBufferHandle.h"
#include "Logger/Logger.h"
#include "Windows/RenderWindowAPI.h"
#include "../Utils/RHIVkDeferredDeletion.h"

ArisenEngine::RHI::RHIVkDevice::RHIVkDevice(RHIInstance* instance, Surface* surface, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device, VkPhysicalDeviceMemoryProperties memoryProperties)
: RHIDevice(instance, surface), m_VkGraphicQueue(graphicQueue), m_VkPresentQueue(presentQueue), m_VkDevice(device), m_VkPhysicalDeviceMemoryProperties(memoryProperties)
{
    m_GPUPipelineManager = new RHIVkGPUPipelineManager(this, m_Instance->GetMaxFramesInFlight());
    m_DescriptorPool = new RHIVkDescriptorPool(this);
    m_DeferredDeletion = std::make_unique<RHIVkDeferredDeletion>(m_Instance->GetMaxFramesInFlight());
    m_ResourceRegistry = std::make_unique<RHIResourceRegistry>(m_DeferredDeletion.get());
}

void ArisenEngine::RHI::RHIVkDevice::DeviceWaitIdle() const
{
    vkDeviceWaitIdle(m_VkDevice);
}

void ArisenEngine::RHI::RHIVkDevice::GraphicQueueWaitIdle() const
{
    vkQueueWaitIdle(m_VkGraphicQueue);
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::CreateGPUProgram()
{
    ASSERT(m_VkDevice != VK_NULL_HANDLE);
    UInt32 id = static_cast<UInt32>(m_GPUPrograms.size());
    m_GPUPrograms.emplace(id, std::make_unique<RHIVkGPUProgram>(m_VkDevice));
    return id;
}

ArisenEngine::RHI::GPUProgram* ArisenEngine::RHI::RHIVkDevice::GetGPUProgram(UInt32 programId)
{
    ASSERT(m_GPUPrograms[programId]);
    return m_GPUPrograms[programId].get();
}

void ArisenEngine::RHI::RHIVkDevice::DestroyGPUProgram(UInt32 programId)
{
    ASSERT(m_GPUPrograms[programId]);
    m_GPUPrograms.erase(programId);
}

bool ArisenEngine::RHI::RHIVkDevice::AttachProgramByteCode(UInt32 programId, GPUProgramDesc&& desc)
{
    ASSERT(m_GPUPrograms[programId]);
    return m_GPUPrograms[programId]->AttachProgramByteCode(std::move(desc));
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::CreateCommandBufferPool()
{
    ASSERT(m_VkDevice != VK_NULL_HANDLE);
    UInt32 id = static_cast<UInt32>(m_CommandBufferPools.size());
    m_CommandBufferPools.emplace(
        id,
        std::make_unique<RHIVkCommandBufferPool>(this, m_Instance->GetMaxFramesInFlight()));
    return id;
}

ArisenEngine::RHI::RHICommandBufferPool* ArisenEngine::RHI::RHIVkDevice::GetCommandBufferPool(UInt32 id)
{
    ASSERT(m_CommandBufferPools[id]);
    return m_CommandBufferPools[id].get();
}

std::shared_ptr<ArisenEngine::RHI::GPURenderPass> ArisenEngine::RHI::RHIVkDevice::GetRenderPass()
{
    std::shared_ptr<GPURenderPass> renderPass;
    if (m_RenderPasses.size() > 0)
    {
        renderPass = m_RenderPasses.back();
        m_RenderPasses.pop_back();
    }
    else
    {
        renderPass = std::make_shared<RHIVkGPURenderPass>(m_VkDevice, m_Instance->GetMaxFramesInFlight());
    }

    return renderPass;
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseRenderPass(std::shared_ptr<GPURenderPass> renderPass)
{
    m_RenderPasses.emplace_back(renderPass);
}

std::shared_ptr<ArisenEngine::RHI::FrameBuffer> ArisenEngine::RHI::RHIVkDevice::GetFrameBuffer()
{
    std::shared_ptr<FrameBuffer> frameBuffer;
    if (m_FrameBuffers.size() > 0)
    {
        frameBuffer = m_FrameBuffers.back();
        m_FrameBuffers.pop_back();
    }
    else
    {
        frameBuffer = std::make_shared<RHIVkFrameBuffer>(m_VkDevice, m_Instance->GetMaxFramesInFlight());
    }

    return frameBuffer;
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseFrameBuffer(std::shared_ptr<FrameBuffer> frameBuffer)
{
    m_FrameBuffers.emplace_back(frameBuffer);
}

std::shared_ptr<ArisenEngine::RHI::BufferHandle> ArisenEngine::RHI::RHIVkDevice::GetBufferHandle(const std::string&& name)
{
    std::shared_ptr<BufferHandle> bufferHandle;
    if (m_BufferHandles.size() > 0)
    {
        bufferHandle = m_BufferHandles.back();
        m_BufferHandles.pop_back();
    }
    else
    {
        bufferHandle = std::make_shared<RHIVkBufferHandle>(this);
    }

    bufferHandle->SetName(std::move(name));
    return bufferHandle;
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseBufferHandle(std::shared_ptr<BufferHandle> bufferHandle)
{
   
}

std::shared_ptr<ArisenEngine::RHI::ImageHandle> ArisenEngine::RHI::RHIVkDevice::GetImageHandle(const std::string&& name)
{
    std::shared_ptr<ImageHandle> imageHandle;
    if (m_ImageHandles.size() > 0)
    {
        imageHandle = m_ImageHandles.back();
        m_ImageHandles.pop_back();
    }
    else
    {
        imageHandle = std::make_shared<RHIVkImageHandle>(this);
    }

    imageHandle->SetName(std::move(name));
    return imageHandle;
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseImageHandle(std::shared_ptr<ImageHandle> imageHandle)
{
   
}

void ArisenEngine::RHI::RHIVkDevice::EnqueueDeferredDestroy(UInt32 frameIndex, std::function<void()>&& fn)
{
    if (m_DeferredDeletion)
    {
        // For now map frameIndex -> ticket (legacy callers). SubmitID-based callers should use Enqueue(ticket).
        m_DeferredDeletion->Enqueue(static_cast<RHIGpuTicket>(frameIndex), std::move(fn));
    }
}

void ArisenEngine::RHI::RHIVkDevice::FlushDeferredDestroys(UInt32 frameIndex)
{
    if (m_DeferredDeletion)
    {
        m_DeferredDeletion->Flush(static_cast<RHIGpuTicket>(frameIndex));
    }
}

void ArisenEngine::RHI::RHIVkDevice::Update()
{
    std::lock_guard<std::mutex> lock(m_GcMutex);

    // Advance completed submit ID by polling fences in-order.
    while (!m_InFlight.empty())
    {
        const auto& front = m_InFlight.front();
        if (front.fence == VK_NULL_HANDLE)
        {
            m_InFlight.pop_front();
            continue;
        }

        const auto status = vkGetFenceStatus(m_VkDevice, front.fence);
        if (status == VK_SUCCESS)
        {
            m_CompletedSubmitId.store(front.submitId, std::memory_order_release);
            m_InFlight.pop_front();
            continue;
        }
        break; // oldest not completed yet => stop (queue is in-order)
    }

    if (m_DeferredDeletion)
    {
        m_DeferredDeletion->Flush(m_CompletedSubmitId.load(std::memory_order_acquire));
    }
}


// TODO: move submit info to CommandBuffer

void ArisenEngine::RHI::RHIVkDevice::Submit(RHICommandBuffer* commandBuffer, UInt32 frameIndex)
{
    ASSERT(commandBuffer->ReadyForSubmit());

    std::lock_guard<std::mutex> lock(m_SubmitMutex);
    m_CurrentFrameIndex.store(frameIndex, std::memory_order_release);
    auto rhiVkCommandBuffer = static_cast<RHIVkCommandBuffer*>(commandBuffer);

    // Assign a monotonic submit ID (ticket).
    const auto submitId = m_NextSubmitId.fetch_add(1, std::memory_order_acq_rel);

    // Use the per-frame fence from the command buffer pool as the submission fence.
    VkFence submissionFence = VK_NULL_HANDLE;
    if (auto* pool = commandBuffer->GetOwner())
    {
        if (auto* fence = pool->GetFence(frameIndex))
        {
            // Wait for previous use of this fence (frame ring), then reset it for this submit.
            fence->Lock();
            fence->Unlock();
            submissionFence = static_cast<VkFence>(fence->GetHandle());
        }
    }

    VkSubmitInfo submitInfo{};
    submitInfo.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
    
    if (rhiVkCommandBuffer->GetWaitSemaphoresCount() > 0)
    {
        submitInfo.waitSemaphoreCount = rhiVkCommandBuffer->GetWaitSemaphoresCount();
        submitInfo.pWaitSemaphores = rhiVkCommandBuffer->GetWaitSemaphores();
        submitInfo.pWaitDstStageMask = rhiVkCommandBuffer->GetWaitStageMask();
    }

    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = (static_cast<VkCommandBuffer*>(commandBuffer->GetHandlerPointer()));

    // VkSemaphore signalSemaphores[] = { static_cast<VkSemaphore>(
    //         m_Surface->GetSwapChain()->GetRenderFinishSemaphore(frameIndex)->GetHandle()) };
    if (rhiVkCommandBuffer->GetSignalSemaphoresCount() > 0)
    {
        submitInfo.signalSemaphoreCount = rhiVkCommandBuffer->GetSignalSemaphoresCount();
        submitInfo.pSignalSemaphores = rhiVkCommandBuffer->GetSignalSemaphores();
    }

    if (vkQueueSubmit(m_VkGraphicQueue, 1, &submitInfo, submissionFence) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkDevice::Submit]: failed to submit draw command buffer!");
    }

    // Track in-flight submit for deferred deletion GC.
    if (submissionFence != VK_NULL_HANDLE)
    {
        std::lock_guard<std::mutex> gcLock(m_GcMutex);
        m_InFlight.push_back({ submissionFence, submitId });
    }
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
    DeviceWaitIdle();
    delete m_GPUPipelineManager;
    delete m_DescriptorPool;
    m_FrameBuffers.clear();
    m_RenderPasses.clear();
    m_GPUPrograms.clear();
    m_CommandBufferPools.clear();
    m_BufferHandles.clear();
    vkDestroyDevice(m_VkDevice, nullptr);
    LOG_DEBUG("## Destroy Vulkan Device ##");
    m_Instance = nullptr;
    LOG_INFO("[RHIVkDevice::~RHIVkDevice]: ~RHIVkDevice");
}



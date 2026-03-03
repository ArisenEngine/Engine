#include "Services/RHIVkTransferManager.h"
#include "Allocation/RHIVkStagingRingBuffer.h"
#include "Allocation/RHIVkMemoryAllocator.h"
#include "Core/RHIVkDevice.h"
#include "Queues/RHIVkQueue.h"
#include "Logger/Logger.h"
#include "Profiler.h"

#include <cstring>

using namespace ArisenEngine;
using namespace ArisenEngine::RHI;

RHIVkTransferManager::RHIVkTransferManager(RHIVkDevice* device, const RHITransferManagerConfig& config)
    : m_Device(device)
    , m_TransferQueue(nullptr)
{
    // Resolve transfer queue (fall back to graphics if no dedicated transfer queue)
    m_TransferQueue = static_cast<RHIVkQueue*>(device->GetQueue(RHIQueueType::Transfer));
    if (!m_TransferQueue)
    {
        m_TransferQueue = static_cast<RHIVkQueue*>(device->GetQueue(RHIQueueType::Graphics));
        LOG_WARN("[RHIVkTransferManager]: No dedicated transfer queue, falling back to graphics queue.");
    }

    // Create staging ring buffer
    m_RingBuffer = std::make_unique<RHIVkStagingRingBuffer>(
        static_cast<VkDevice>(device->GetHandle()),
        static_cast<RHIVkMemoryAllocator*>(device->GetMemoryAllocator()),
        config.ringBufferCapacity);

    // Create persistent command pool for transfer operations
    VkCommandPoolCreateInfo poolInfo{};
    poolInfo.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
    poolInfo.flags = VK_COMMAND_POOL_CREATE_TRANSIENT_BIT | VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT;
    poolInfo.queueFamilyIndex = (device->GetQueue(RHIQueueType::Transfer))
                                    ? device->GetTransferFamilyIndex()
                                    : device->GetGraphicsFamilyIndex();

    VkDevice vkDevice = static_cast<VkDevice>(device->GetHandle());
    if (vkCreateCommandPool(vkDevice, &poolInfo, nullptr, &m_CommandPool) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkTransferManager]: Failed to create transfer command pool!");
    }

    // Pre-allocate a single command buffer
    VkCommandBufferAllocateInfo allocInfo{};
    allocInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
    allocInfo.commandPool = m_CommandPool;
    allocInfo.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
    allocInfo.commandBufferCount = 1;

    if (vkAllocateCommandBuffers(vkDevice, &allocInfo, &m_CommandBuffer) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkTransferManager]: Failed to allocate transfer command buffer!");
    }

    LOG_INFO("[RHIVkTransferManager]: Initialized transfer manager.");
}

RHIVkTransferManager::~RHIVkTransferManager()
{
    VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());

    if (m_TransferQueue)
    {
        m_TransferQueue->WaitIdle();
    }

    if (m_CommandPool != VK_NULL_HANDLE)
    {
        vkDestroyCommandPool(vkDevice, m_CommandPool, nullptr);
        m_CommandPool = VK_NULL_HANDLE;
        m_CommandBuffer = VK_NULL_HANDLE; // implicitly freed with pool
    }

    m_RingBuffer.reset();
}

void RHIVkTransferManager::EnsureCommandBufferReady()
{
    if (m_CommandBufferRecording)
    {
        return;
    }

    VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
    vkResetCommandPool(vkDevice, m_CommandPool, 0);

    VkCommandBufferBeginInfo beginInfo{};
    beginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
    beginInfo.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;

    if (vkBeginCommandBuffer(m_CommandBuffer, &beginInfo) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkTransferManager]: Failed to begin transfer command buffer!");
        return;
    }

    m_CommandBufferRecording = true;
}

void RHIVkTransferManager::EnqueueBufferCopy(VkBuffer dstBuffer, const void* srcData,
                                              UInt64 size, UInt64 dstOffset)
{
    ARISEN_PROFILE_ZONE("TransferManager::EnqueueBufferCopy");

    if (!srcData || size == 0)
    {
        return;
    }

    // Allocate from staging ring buffer
    auto staging = m_RingBuffer->Allocate(size);
    if (!staging.has_value())
    {
        // Ring buffer is full — flush pending copies, reclaim, and retry
        LOG_WARN("[RHIVkTransferManager]: Staging ring buffer full, flushing and waiting...");
        RHIGpuTicket ticket = Flush();
        if (ticket > 0)
        {
            WaitForTicket(ticket);
        }
        Update();

        staging = m_RingBuffer->Allocate(size);
        if (!staging.has_value())
        {
            LOG_ERROR("[RHIVkTransferManager]: Staging allocation failed even after flush! "
                      "Size requested: {} bytes, ring capacity: {} bytes");
            return;
        }
    }

    // Copy CPU data into staging buffer
    std::memcpy(staging->mappedPtr, srcData, size);

    // Flush for non-coherent memory
    m_RingBuffer->FlushRegion(staging->offset, staging->size);

    // Record the copy command
    EnsureCommandBufferReady();

    VkBufferCopy copyRegion{};
    copyRegion.srcOffset = staging->offset;
    copyRegion.dstOffset = dstOffset;
    copyRegion.size = size;

    m_PendingCopies.push_back({staging->buffer, dstBuffer, copyRegion});
}

RHIGpuTicket RHIVkTransferManager::Flush()
{
    ARISEN_PROFILE_ZONE("TransferManager::Flush");

    if (m_PendingCopies.empty() || !m_CommandBufferRecording)
    {
        return 0;
    }

    // Record all pending copy commands
    for (const auto& copy : m_PendingCopies)
    {
        vkCmdCopyBuffer(m_CommandBuffer, copy.srcBuffer, copy.dstBuffer, 1, &copy.region);
    }

    // TODO(Transfer-P2): Insert queue ownership transfer barriers here when
    // transfer and graphics queue families differ. Each destination buffer
    // needs a VK_PIPELINE_STAGE_TRANSFER_BIT → VK_PIPELINE_STAGE_TRANSFER_BIT
    // release barrier on the transfer queue, and a matching acquire barrier
    // on the graphics queue before first use.

    if (vkEndCommandBuffer(m_CommandBuffer) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkTransferManager]: Failed to end transfer command buffer!");
        m_PendingCopies.clear();
        m_CommandBufferRecording = false;
        return 0;
    }

    m_CommandBufferRecording = false;

    // Submit via raw VkQueueSubmit since the transfer manager uses its own
    // command buffer (not an RHIVkCommandBuffer with tracked resources).
    // We still use the queue's timeline semaphore for synchronization.
    VkSubmitInfo submitInfo{};
    submitInfo.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = &m_CommandBuffer;

    // Use the queue's timeline semaphore for ticketing
    RHIGpuTicket ticket = m_TransferQueue->GetLatestTicket() + 1;

    VkTimelineSemaphoreSubmitInfo timelineInfo{};
    timelineInfo.sType = VK_STRUCTURE_TYPE_TIMELINE_SEMAPHORE_SUBMIT_INFO;

    // We don't directly access the queue's internal semaphore, so we
    // submit without timeline and use WaitIdle as fallback.
    // This is safe because the TransferManager is the only user of this
    // command pool/buffer pair.
    VkFenceCreateInfo fenceInfo{};
    fenceInfo.sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;

    VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
    VkFence fence;
    if (vkCreateFence(vkDevice, &fenceInfo, nullptr, &fence) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkTransferManager]: Failed to create flush fence!");
        m_PendingCopies.clear();
        return 0;
    }

    VkResult submitResult = vkQueueSubmit(m_TransferQueue->GetVkQueue(), 1, &submitInfo, fence);
    if (submitResult != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkTransferManager]: Failed to submit transfer command buffer!");
        vkDestroyFence(vkDevice, fence, nullptr);
        m_PendingCopies.clear();
        return 0;
    }

    // Store fence for WaitForTicket — for now we use a simple blocking approach.
    // The ticket is synthetic (monotonic counter) for ring buffer reclamation.
    static RHIGpuTicket s_TransferTicketCounter = 0;
    ticket = ++s_TransferTicketCounter;

    m_RingBuffer->MarkTicket(ticket);
    m_PendingCopies.clear();

    // Wait for fence immediately and destroy it (synchronous flush for now).
    // TODO(Transfer-P3): Track fences per-ticket for true async support.
    vkWaitForFences(vkDevice, 1, &fence, VK_TRUE, UINT64_MAX);
    vkDestroyFence(vkDevice, fence, nullptr);

    // Reclaim immediately after sync flush
    m_RingBuffer->ReclaimUpTo(ticket);

    return ticket;
}

void RHIVkTransferManager::WaitForTicket(RHIGpuTicket ticket)
{
    ARISEN_PROFILE_ZONE("TransferManager::WaitForTicket");
    // Current implementation: Flush() already waits synchronously.
    // TODO(Transfer-P3): When async fence tracking is added, wait on the
    // specific fence associated with this ticket.
    (void)ticket;
}

void RHIVkTransferManager::Update()
{
    ARISEN_PROFILE_ZONE("TransferManager::Update");
    // Current implementation: Flush() already reclaims synchronously.
    // TODO(Transfer-P3): When async fence tracking is added, poll fence
    // completion and call m_RingBuffer->ReclaimUpTo(completedTicket).
}

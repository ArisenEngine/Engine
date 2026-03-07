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

    for (auto& [familyIndex, pool] : m_AcquireCommandPools)
    {
        if (pool != VK_NULL_HANDLE)
        {
            vkDestroyCommandPool(vkDevice, pool, nullptr);
        }
    }
    m_AcquireCommandPools.clear();
    m_AcquireCommandBuffers.clear();

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
                                              UInt64 size, UInt64 dstOffset, uint32_t dstQueueFamilyIndex)
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

    m_PendingCopies.push_back({staging->buffer, dstBuffer, copyRegion, dstQueueFamilyIndex});
}

RHIGpuTicket RHIVkTransferManager::Flush()
{
    ARISEN_PROFILE_ZONE("TransferManager::Flush");

    if (m_PendingCopies.empty() || !m_CommandBufferRecording)
    {
        return 0;
    }

    // Record all pending copy commands
    std::vector<VkBufferMemoryBarrier2> releaseBarriers;
    std::vector<VkBufferMemoryBarrier2> acquireBarriers;

    uint32_t transferFamily = (m_Device->GetQueue(RHIQueueType::Transfer))
                                  ? m_Device->GetTransferFamilyIndex()
                                  : m_Device->GetGraphicsFamilyIndex();

    for (const auto& copy : m_PendingCopies)
    {
        vkCmdCopyBuffer(m_CommandBuffer, copy.srcBuffer, copy.dstBuffer, 1, &copy.region);

        if (copy.dstQueueFamilyIndex != ~0u && copy.dstQueueFamilyIndex != transferFamily)
        {
            VkBufferMemoryBarrier2 barrier{};
            barrier.sType = VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER_2;
            barrier.srcStageMask = VK_PIPELINE_STAGE_2_TRANSFER_BIT;
            barrier.srcAccessMask = VK_ACCESS_2_TRANSFER_WRITE_BIT;
            barrier.dstStageMask = VK_PIPELINE_STAGE_2_ALL_COMMANDS_BIT; 
            barrier.dstAccessMask = VK_ACCESS_2_MEMORY_READ_BIT | VK_ACCESS_2_MEMORY_WRITE_BIT;
            barrier.srcQueueFamilyIndex = transferFamily;
            barrier.dstQueueFamilyIndex = copy.dstQueueFamilyIndex;
            barrier.buffer = copy.dstBuffer;
            barrier.offset = 0;
            barrier.size = VK_WHOLE_SIZE;

            releaseBarriers.push_back(barrier);
            acquireBarriers.push_back(barrier);
        }
    }

    if (!releaseBarriers.empty())
    {
        VkDependencyInfo depInfo{};
        depInfo.sType = VK_STRUCTURE_TYPE_DEPENDENCY_INFO;
        depInfo.bufferMemoryBarrierCount = static_cast<uint32_t>(releaseBarriers.size());
        depInfo.pBufferMemoryBarriers = releaseBarriers.data();
        m_Device->vkCmdPipelineBarrier2KHR(m_CommandBuffer, &depInfo);
    }

    if (vkEndCommandBuffer(m_CommandBuffer) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkTransferManager]: Failed to end transfer command buffer!");
        m_PendingCopies.clear();
        m_CommandBufferRecording = false;
        return 0;
    }

    m_CommandBufferRecording = false;

    // Submit via raw VkQueueSubmit using our new SubmitRaw which handles numeric tickets
    // and timeline semaphores natively.
    RHIGpuTicket ticket = m_TransferQueue->SubmitRaw(m_CommandBuffer);
    m_RingBuffer->MarkTicket(ticket);
    m_PendingCopies.clear();

    // If there were any acquire barriers needed, submit them to their respective target Queues asynchronously.
    if (!acquireBarriers.empty())
    {
        VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
        std::unordered_map<uint32_t, std::vector<VkBufferMemoryBarrier2>> barriersByFamily;
        for (const auto& barrier : acquireBarriers)
        {
            barriersByFamily[barrier.dstQueueFamilyIndex].push_back(barrier);
        }

        for (const auto& [familyIndex, barriers] : barriersByFamily)
        {
            auto* targetQueue = static_cast<RHIVkQueue*>(m_Device->GetQueueByFamilyIndex(familyIndex));
            if (!targetQueue) continue;

            if (m_AcquireCommandPools.find(familyIndex) == m_AcquireCommandPools.end())
            {
                VkCommandPoolCreateInfo poolInfo{};
                poolInfo.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
                poolInfo.flags = VK_COMMAND_POOL_CREATE_TRANSIENT_BIT | VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT;
                poolInfo.queueFamilyIndex = familyIndex;

                vkCreateCommandPool(vkDevice, &poolInfo, nullptr, &m_AcquireCommandPools[familyIndex]);

                VkCommandBufferAllocateInfo allocInfo{};
                allocInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
                allocInfo.commandPool = m_AcquireCommandPools[familyIndex];
                allocInfo.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
                allocInfo.commandBufferCount = 1;

                vkAllocateCommandBuffers(vkDevice, &allocInfo, &m_AcquireCommandBuffers[familyIndex]);
            }

            VkCommandPool pool = m_AcquireCommandPools[familyIndex];
            VkCommandBuffer cmdBuffer = m_AcquireCommandBuffers[familyIndex];

            vkResetCommandPool(vkDevice, pool, 0);

            VkCommandBufferBeginInfo beginInfo{};
            beginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
            beginInfo.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;

            vkBeginCommandBuffer(cmdBuffer, &beginInfo);

            VkDependencyInfo depInfo{};
            depInfo.sType = VK_STRUCTURE_TYPE_DEPENDENCY_INFO;
            depInfo.bufferMemoryBarrierCount = static_cast<uint32_t>(barriers.size());
            depInfo.pBufferMemoryBarriers = barriers.data();
            m_Device->vkCmdPipelineBarrier2KHR(cmdBuffer, &depInfo);

            vkEndCommandBuffer(cmdBuffer);

            // Submit to target queue, waiting on the transfer queue's timeline semaphore
            targetQueue->SubmitRaw(cmdBuffer,
                { m_TransferQueue->GetTimelineSemaphore() },
                { VK_PIPELINE_STAGE_ALL_COMMANDS_BIT },
                { ticket }
            );
        }
    }

    return ticket;
}

void RHIVkTransferManager::WaitForTicket(RHIGpuTicket ticket)
{
    ARISEN_PROFILE_ZONE("TransferManager::WaitForTicket");
    if (m_TransferQueue)
    {
        m_TransferQueue->WaitForTicket(ticket);
    }
}

void RHIVkTransferManager::Update()
{
    ARISEN_PROFILE_ZONE("TransferManager::Update");
    if (m_TransferQueue)
    {
        m_TransferQueue->Update();
        RHIGpuTicket completedTicket = m_TransferQueue->GetCompletedTicket();
        m_RingBuffer->ReclaimUpTo(completedTicket);
    }
}

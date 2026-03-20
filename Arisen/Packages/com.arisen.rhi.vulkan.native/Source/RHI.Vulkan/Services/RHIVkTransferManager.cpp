#include "Services/RHIVkTransferManager.h"
#include "Allocation/RHIVkStagingRingBuffer.h"
#include "Allocation/RHIVkMemoryAllocator.h"
#include "Core/RHIVkDevice.h"
#include "Queues/RHIVkQueue.h"
#include "Logger/Logger.h"
#include "Profiler.h"
#include "Commands/RHIVkCommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Handles/RHIHandle.h"
#include <cstring>
#include <unordered_map>
#include <vector>

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
    m_TransferCommandPool = device->GetFactory()->CreateCommandBufferPool(
        m_TransferQueue ? m_TransferQueue->GetType() : RHIQueueType::Graphics);
    
    if (!m_TransferCommandPool.IsValid())
    {
        LOG_FATAL_AND_THROW("[RHIVkTransferManager]: Failed to create transfer command pool via factory!");
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

    if (m_TransferCommandPool.IsValid())
    {
        m_Device->GetFactory()->ReleaseCommandBufferPool(m_TransferCommandPool);
        m_TransferCommandPool.index = 0xFFFFFFFFu;
    }

    for (auto& [type, poolHandle] : m_AcquireCommandPools)
    {
        if (poolHandle.IsValid())
        {
            m_Device->GetFactory()->ReleaseCommandBufferPool(poolHandle);
        }
    }
    m_AcquireCommandPools.clear();

    m_RingBuffer.reset();
}

void RHIVkTransferManager::EnsureCommandBufferReady()
{
    if (m_CommandBufferRecording)
    {
        return;
    }

    auto* pool = m_Device->GetCommandBufferPool(m_TransferCommandPool);
    if (!pool)
    {
        LOG_FATAL_AND_THROW("[RHIVkTransferManager]: Invalid transfer command pool!");
    }

    m_CurrentCommandBuffer = pool->GetCommandBuffer(0);
    auto* commandBuffer = m_Device->GetCommandBuffer(m_CurrentCommandBuffer);

    if (!commandBuffer)
    {
        LOG_ERROR("[RHIVkTransferManager]: Failed to begin transfer command buffer!");
        return;
    }
    commandBuffer->Begin(true);

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

    auto* commandBuffer = m_Device->GetCommandBuffer(m_CurrentCommandBuffer);
    if (!commandBuffer) return 0;
    
    // Get the internal Vulkan command buffer for manual copies inside the abstraction if needed
    auto* vkCmd = static_cast<RHIVkCommandBuffer*>(commandBuffer);
    VkCommandBuffer rawCmdBuf = (VkCommandBuffer)vkCmd->GetHandle();

    for (const auto& copy : m_PendingCopies)
    {
        vkCmdCopyBuffer(rawCmdBuf, copy.srcBuffer, copy.dstBuffer, 1, &copy.region);

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
        m_Device->vkCmdPipelineBarrier2KHR(rawCmdBuf, &depInfo);
    }

    commandBuffer->End();

    m_CommandBufferRecording = false;

    RHIGpuTicket ticket = m_TransferQueue->Submit(m_CurrentCommandBuffer, nullptr);
    
    // Recycle safely
    m_Device->GetCommandBufferPool(m_TransferCommandPool)->ReleaseCommandBuffer(0, m_CurrentCommandBuffer);
    m_CurrentCommandBuffer.index = 0xFFFFFFFFu;

    m_RingBuffer->MarkTicket(ticket);
    m_PendingCopies.clear();

    // If there were any acquire barriers needed, submit them to their respective target Queues asynchronously.
    if (!acquireBarriers.empty())
    {
        std::unordered_map<uint32_t, std::vector<VkBufferMemoryBarrier2>> barriersByFamily;
        for (const auto& barrier : acquireBarriers)
        {
            barriersByFamily[barrier.dstQueueFamilyIndex].push_back(barrier);
        }

        for (const auto& [familyIndex, barriers] : barriersByFamily)
        {
            auto* targetQueue = static_cast<RHIVkQueue*>(m_Device->GetQueueByFamilyIndex(familyIndex));
            if (!targetQueue) continue;
            
            RHIQueueType targetType = targetQueue->GetType();
            uint32_t typeKey = static_cast<uint32_t>(targetType);

            if (m_AcquireCommandPools.find(typeKey) == m_AcquireCommandPools.end())
            {
                m_AcquireCommandPools[typeKey] = m_Device->GetFactory()->CreateCommandBufferPool(targetType);
            }

            auto acquirePoolHandle = m_AcquireCommandPools[typeKey];
            auto* acquirePool = m_Device->GetCommandBufferPool(acquirePoolHandle);
            if (!acquirePool) continue;

            auto acquireCmdHandle = acquirePool->GetCommandBuffer(0);
            auto* acquireCmd = m_Device->GetCommandBuffer(acquireCmdHandle);

            if (acquireCmd)
            {
                acquireCmd->Begin(true);
                auto* vkAcquireCmd = static_cast<RHIVkCommandBuffer*>(acquireCmd);
                VkCommandBuffer rawAcquireCmdBuf = (VkCommandBuffer)vkAcquireCmd->GetHandle();

                VkDependencyInfo depInfo{};
                depInfo.sType = VK_STRUCTURE_TYPE_DEPENDENCY_INFO;
                depInfo.bufferMemoryBarrierCount = static_cast<uint32_t>(barriers.size());
                depInfo.pBufferMemoryBarriers = barriers.data();
                m_Device->vkCmdPipelineBarrier2KHR(rawAcquireCmdBuf, &depInfo);

                acquireCmd->End();

                // Setup dependency waiting for the transfer via the transfer queue's timeline semaphore
                RHISubmitDescriptor waitDesc{};
                RHISemaphoreHandle transferSem = m_TransferQueue->GetTimelineSemaphoreHandle();
                const VkPipelineStageFlags waitStage = VK_PIPELINE_STAGE_ALL_COMMANDS_BIT;
                const uint64_t waitVal = ticket;

                waitDesc.pWaitSemaphores = &transferSem;
                waitDesc.pWaitDstStageMask = &waitStage;
                waitDesc.pWaitValues = &waitVal;
                waitDesc.waitSemaphoreCount = 1;

                targetQueue->Submit(acquireCmdHandle, &waitDesc);

                // Release to recycle later
                acquirePool->ReleaseCommandBuffer(0, acquireCmdHandle);
            }
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

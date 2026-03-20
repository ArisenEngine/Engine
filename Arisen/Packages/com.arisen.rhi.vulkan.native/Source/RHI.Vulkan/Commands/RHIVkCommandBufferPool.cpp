#include "Commands/RHIVkCommandBufferPool.h"
#include "Profiler.h"
#include "Logger/Logger.h"

#include "Commands/RHIVkCommandBuffer.h"
#include "Core/RHIVkDevice.h"
#include "Presentation/RHIVkSurface.h"
#include "Handles/RHIVkResourcePools.h"

using namespace ArisenEngine::RHI;

ArisenEngine::RHI::RHIVkCommandBufferPool::RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight,
                                                                  RHIQueueType queueType)
    : RHICommandBufferPool(device, maxFramesInFlight, queueType)
{
    m_VkDevice = static_cast<VkDevice>(device->GetHandle());

    (void)maxFramesInFlight;
}

ArisenEngine::RHI::RHIVkCommandBufferPool::~RHIVkCommandBufferPool() noexcept
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());

    for (auto handle : m_OwnedHandles)
    {
        vkDevice->ReleaseCommandBuffer(handle);
    }
    m_OwnedHandles.clear();

    for (size_t i = 0; i < MAX_THREADS; ++i)
    {
        auto& slot = m_Slots[i];
        if (slot.commandPool != VK_NULL_HANDLE)
        {
            vkDestroyCommandPool(m_VkDevice, slot.commandPool, nullptr);
            slot.commandPool = VK_NULL_HANDLE;
        }
    }
}

RHICommandBufferHandle ArisenEngine::RHI::RHIVkCommandBufferPool::GetCommandBuffer(
    UInt32 currentFrameIndex, ECommandBufferLevel level)
{
    ARISEN_PROFILE_ZONE("Vk::PoolGetCommandBuffer");
    auto& slot = GetCurrentThreadSlot();
    FlushPendingBuffers(slot);
    ConsumeMailbox(slot);

    // Prefer TLS first
    auto& freeList = (level == COMMAND_BUFFER_LEVEL_PRIMARY) ? slot.freePrimaryBuffers : slot.freeSecondaryBuffers;
    if (!freeList.empty())
    {
        auto* cmd = freeList.back();
        freeList.pop_back();
        return cmd->GetRHIHandle();
    }

    return RHICommandBufferPool::GetCommandBuffer(currentFrameIndex, level);
}


void ArisenEngine::RHI::RHIVkCommandBufferPool::ReleaseCommandBuffer(UInt32 currentFrameIndex,
                                                                     RHICommandBufferHandle handle)
{
    (void)currentFrameIndex;
    auto* commandBuffer = static_cast<RHIVkDevice*>(GetDevice())->GetCommandBuffer(handle);
    if (!commandBuffer) return;

    auto ticket = commandBuffer->GetLatestSubmitTicket();
    auto* queue = GetDevice()->GetQueue(m_QueueType);

    // Check if the GPU is already done with it.
    if (!queue || queue->GetCompletedTicket() >= ticket)
    {
        InternalRecycle(handle);
        return;
    }

    // Otherwise, defer recycling to the current thread's pending list.
    auto& slot = GetCurrentThreadSlot();
    slot.pendingBuffers.emplace_back(ticket, commandBuffer);
}

void ArisenEngine::RHI::RHIVkCommandBufferPool::FlushPendingBuffers(ThreadSlot& slot)
{
    // First, consume any buffers returned from other threads to this thread's mailbox
    ConsumeMailbox(slot);

    if (slot.pendingBuffers.empty()) return;

    auto* queue = GetDevice()->GetQueue(m_QueueType);
    if (queue) queue->Update();
    auto completed = queue ? queue->GetCompletedTicket() : 0;

    for (auto it = slot.pendingBuffers.begin(); it != slot.pendingBuffers.end();)
    {
        if (completed >= it->first)
        {
            auto* cmd = it->second;
            cmd->ResetInternal();
            auto& freeList = (cmd->GetLevel() == COMMAND_BUFFER_LEVEL_PRIMARY)
                                 ? slot.freePrimaryBuffers
                                 : slot.freeSecondaryBuffers;
            freeList.push_back(cmd);
            it = slot.pendingBuffers.erase(it);
        }
        else
        {
            ++it;
        }
    }
}

void ArisenEngine::RHI::RHIVkCommandBufferPool::ConsumeMailbox(ThreadSlot& slot)
{
    RHICommandBuffer* cmd = nullptr;
    while (slot.mailbox.TryPop(cmd))
    {
        if (cmd)
        {
            cmd->ResetInternal();
            auto& freeList = (cmd->GetLevel() == COMMAND_BUFFER_LEVEL_PRIMARY)
                                 ? slot.freePrimaryBuffers
                                 : slot.freeSecondaryBuffers;
            freeList.push_back(cmd);
        }
    }
}

void ArisenEngine::RHI::RHIVkCommandBufferPool::InternalRecycle(RHICommandBufferHandle handle)
{
    auto* commandBuffer = static_cast<RHIVkDevice*>(GetDevice())->GetCommandBuffer(handle);
    if (!commandBuffer) return;

    auto ticket = commandBuffer->GetLatestSubmitTicket();
    auto* queue = GetDevice()->GetQueue(m_QueueType);

    if (queue && queue->GetCompletedTicket() < ticket)
    {
        // LOG_WARN("[RHIVkCommandBufferPool::InternalRecycle]: Command buffer is still pending on GPU! Deferring recycle.");
        auto& slot = GetCurrentThreadSlot();
        slot.pendingBuffers.emplace_back(ticket, commandBuffer);
        return;
    }

    commandBuffer->ResetInternal();

    auto* vkCmd = static_cast<RHIVkCommandBuffer*>(commandBuffer);
    size_t ownerThreadIdx = vkCmd->m_OwnerThreadIndex;

    // Optimization: If we're on the same thread as the owner, 
    // we can bypass the mailbox and put it straight into TLS free list.
    if (ownerThreadIdx == ThreadRegistry::GetThreadIndex())
    {
        auto& slot = m_Slots[ownerThreadIdx];
        auto& freeList = (commandBuffer->GetLevel() == COMMAND_BUFFER_LEVEL_PRIMARY)
                             ? slot.freePrimaryBuffers
                             : slot.freeSecondaryBuffers;
        freeList.push_back(commandBuffer);
        return;
    }

    // Cross-thread recycling: Push to the owner's mailbox using lock-free stack
    if (ownerThreadIdx < MAX_THREADS)
    {
        m_Slots[ownerThreadIdx].mailbox.Push(commandBuffer);
    }
}

ArisenEngine::RHI::RHICommandBufferHandle ArisenEngine::RHI::RHIVkCommandBufferPool::CreateCommandBuffer(
    ECommandBufferLevel level)
{
    ARISEN_PROFILE_ZONE("Vk::PoolCreateCommandBuffer");
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    ASSERT(vkDevice != nullptr);

    RHICommandBufferHandle handle = vkDevice->GetCommandBufferPool()->Allocate(
        [this, vkDevice, level](RHIVkCommandBufferItem* item)
        {
            *item = RHIVkCommandBufferItem();
            item->commandBuffer = new RHIVkCommandBuffer(vkDevice, this, level);

            // Register for deferred deletion
            struct DeferredCmdBuffer
            {
                RHIVkCommandBuffer* buffer;
                ~DeferredCmdBuffer() { delete buffer; }
            };
            item->registryHandle = vkDevice->GetResourceRegistry()->Create(
                MakeDeferredDeleteItem(new DeferredCmdBuffer{item->commandBuffer}));
        });

    auto* item = vkDevice->GetCommandBufferPool()->Get(handle);
    item->commandBuffer->SetRHIHandle(handle);

    std::lock_guard<std::mutex> lock(m_PoolsMutex);
    m_OwnedHandles.emplace_back(handle);
    return handle;
}

RHIVkCommandBufferPool::ThreadSlot& ArisenEngine::RHI::RHIVkCommandBufferPool::GetCurrentThreadSlot()
{
    size_t threadIdx = ThreadRegistry::GetThreadIndex();
    auto& slot = m_Slots[threadIdx];

    bool expected = false;
    if (slot.initialized.compare_exchange_strong(expected, true))
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());

        VkCommandPoolCreateInfo poolInfo{};
        poolInfo.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
        poolInfo.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT | VK_COMMAND_POOL_CREATE_TRANSIENT_BIT;

        if (m_QueueType == RHIQueueType::Compute)
        {
            poolInfo.queueFamilyIndex = vkDevice->GetComputeFamilyIndex();
        }
        else
        {
            poolInfo.queueFamilyIndex = vkDevice->GetGraphicsFamilyIndex();
        }

        if (vkCreateCommandPool(m_VkDevice, &poolInfo, nullptr, &slot.commandPool) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW(
                "[RHIVkCommandBufferPool::GetCurrentThreadSlot]: failed to create command pool for thread!");
        }
    }

    return slot;
}

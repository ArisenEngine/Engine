#include "Commands/RHIVkCommandBufferPool.h"

#include "Commands/RHIVkCommandBuffer.h"
#include "Core/RHIVkDevice.h"
#include "Presentation/RHIVkSurface.h"
#include "Handles/RHIVkResourcePools.h"

using namespace ArisenEngine::RHI;

ArisenEngine::RHI::RHIVkCommandBufferPool::RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight)
: RHICommandBufferPool(device, maxFramesInFlight)
{
    m_VkDevice = static_cast<VkDevice>(device->GetHandle());
    
    (void)maxFramesInFlight;
}

ArisenEngine::RHI::RHIVkCommandBufferPool::~RHIVkCommandBufferPool() noexcept
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto latestTicket = vkDevice->GetQueue(RHIQueueType::Graphics)->GetLatestTicket();

    for (auto handle : m_OwnedHandles)
    {
        vkDevice->ReleaseCommandBuffer(handle);
    }
    m_OwnedHandles.clear();

    {
        std::lock_guard<std::mutex> lock(m_PoolsMutex);
        for (auto& [threadId, pool] : m_ThreadPools)
        {
            if (pool != VK_NULL_HANDLE)
            {
                vkDestroyCommandPool(m_VkDevice, pool, nullptr);
            }
        }
        m_ThreadPools.clear();
    }
}

ArisenEngine::RHI::RHICommandBuffer* ArisenEngine::RHI::RHIVkCommandBufferPool::GetCommandBuffer(UInt32 currentFrameIndex)
{
    ThreadLocalFreeList* tlsList = nullptr;
    using FreeListCache = ThreadLocalCache<RHIVkCommandBufferPool, ThreadLocalFreeList*, struct FreeListTag>;
    
    if (FreeListCache::Get(this, tlsList) && tlsList)
    {
        FlushPendingBuffers(tlsList);
        if (!tlsList->freeBuffers.empty())
        {
            RHICommandBuffer* commandBuffer = tlsList->freeBuffers.back();
            tlsList->freeBuffers.pop_back();
            return commandBuffer;
        }
    }

    // fallback to locked map or base class
    {
        std::lock_guard<std::mutex> lock(m_PoolsMutex);
        auto& freeList = m_ThreadFreeBuffers[std::this_thread::get_id()];
        if (!freeList.empty())
        {
            RHICommandBuffer* commandBuffer = freeList.back();
            freeList.pop_back();
            
            // update TLS cache for next time
            if (!tlsList)
            {
                tlsList = new ThreadLocalFreeList(); 
                FreeListCache::Set(this, tlsList);
            }
            return commandBuffer;
        }
    }

    return CreateCommandBuffer();
}

void ArisenEngine::RHI::RHIVkCommandBufferPool::ReleaseCommandBuffer(UInt32 currentFrameIndex, RHICommandBuffer* commandBuffer)
{
    (void)currentFrameIndex;
    auto ticket = commandBuffer->GetLatestSubmitTicket();
    
    // Check if the GPU is already done with it.
    if (GetDevice()->GetCompletedSubmitTicket() >= ticket)
    {
        InternalRecycle(commandBuffer);
        return;
    }

    // Otherwise, defer recycling to the current thread's pending list.
    ThreadLocalFreeList* tlsList = nullptr;
    using FreeListCache = ThreadLocalCache<RHIVkCommandBufferPool, ThreadLocalFreeList*, struct FreeListTag>;
    
    if (!FreeListCache::Get(this, tlsList) || !tlsList)
    {
        tlsList = new ThreadLocalFreeList(); 
        FreeListCache::Set(this, tlsList);
    }
    
    tlsList->pendingBuffers.emplace_back(ticket, commandBuffer);
}

void ArisenEngine::RHI::RHIVkCommandBufferPool::FlushPendingBuffers(ThreadLocalFreeList* tlsList)
{
    if (!tlsList || tlsList->pendingBuffers.empty()) return;

    auto completed = GetDevice()->GetCompletedSubmitTicket();
    
    for (auto it = tlsList->pendingBuffers.begin(); it != tlsList->pendingBuffers.end(); )
    {
        if (completed >= it->first)
        {
            auto* cmd = it->second;
            cmd->ResetInternal();
            tlsList->freeBuffers.push_back(cmd);
            it = tlsList->pendingBuffers.erase(it);
        }
        else
        {
            ++it;
        }
    }
}

void ArisenEngine::RHI::RHIVkCommandBufferPool::InternalRecycle(RHICommandBuffer* commandBuffer)
{
    commandBuffer->ResetInternal();

    auto* vkCmd = static_cast<RHIVkCommandBuffer*>(commandBuffer);
    std::thread::id ownerId = vkCmd->m_OwnerThreadId;

    // Optimization: If we're on the same thread as the owner, 
    // we can bypass the global lock and put it straight into TLS free list.
    if (ownerId == std::this_thread::get_id())
    {
        ThreadLocalFreeList* tlsList = nullptr;
        using FreeListCache = ThreadLocalCache<RHIVkCommandBufferPool, ThreadLocalFreeList*, struct FreeListTag>;
        if (FreeListCache::Get(this, tlsList) && tlsList)
        {
            tlsList->freeBuffers.push_back(commandBuffer);
            return;
        }
    }

    std::lock_guard<std::mutex> lock(m_PoolsMutex);
    m_ThreadFreeBuffers[ownerId].emplace_back(commandBuffer);
}

ArisenEngine::RHI::RHICommandBuffer* ArisenEngine::RHI::RHIVkCommandBufferPool::CreateCommandBuffer()
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    ASSERT(vkDevice != nullptr);
    
    RHICommandBufferHandle handle = vkDevice->GetCommandBufferPool()->Allocate([this, vkDevice](RHIVkCommandBufferItem* item)
    {
        *item = RHIVkCommandBufferItem();
        item->commandBuffer = new RHIVkCommandBuffer(vkDevice, this);
        
        // Register for deferred deletion (of the C++ object)
        struct DeferredCmdBuffer
        {
            RHIVkCommandBuffer* buffer;
            ~DeferredCmdBuffer() { delete buffer; }
        };
        item->registryHandle = vkDevice->GetResourceRegistry()->Create(
            MakeDeferredDeleteItem(new DeferredCmdBuffer{item->commandBuffer}));
    });

    auto* item = vkDevice->GetCommandBufferPool()->Get(handle);
    RHICommandBuffer* rawPtr = item->commandBuffer;

    std::lock_guard<std::mutex> lock(m_PoolsMutex); 
    m_OwnedHandles.emplace_back(handle);
    return rawPtr;
}

VkCommandPool ArisenEngine::RHI::RHIVkCommandBufferPool::AcquireThreadCommandPool()
{
    VkCommandPool pool = VK_NULL_HANDLE;
    using PoolCache = ThreadLocalCache<RHIVkCommandBufferPool, VkCommandPool, struct PoolTag>;

    if (PoolCache::Get(this, pool))
    {
        return pool;
    }

    std::thread::id threadId = std::this_thread::get_id();

    std::lock_guard<std::mutex> lock(m_PoolsMutex);
    auto it = m_ThreadPools.find(threadId);
    if (it != m_ThreadPools.end())
    {
        pool = it->second;
        PoolCache::Set(this, pool);
        return pool;
    }

    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());

    VkCommandPoolCreateInfo poolInfo{};
    poolInfo.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
    poolInfo.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT | VK_COMMAND_POOL_CREATE_TRANSIENT_BIT;
    poolInfo.queueFamilyIndex = vkDevice->GetGraphicsFamilyIndex();

    if (vkCreateCommandPool(m_VkDevice, &poolInfo, nullptr, &pool) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkCommandBufferPool::AcquireThreadCommandPool]: failed to create command pool for thread!");
    }

    m_ThreadPools[threadId] = pool;
    PoolCache::Set(this, pool);
    return pool;
}





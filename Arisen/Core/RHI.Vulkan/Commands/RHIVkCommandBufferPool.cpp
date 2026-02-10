#include "Commands/RHIVkCommandBufferPool.h"

#include "Commands/RHIVkCommandBuffer.h"
#include "Core/RHIVkDevice.h"
#include "Presentation/RHIVkSurface.h"
#include "Handles/RHIVkResourcePools.h"

using namespace ArisenEngine::RHI;

ArisenEngine::RHI::RHIVkCommandBufferPool::RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight, RHIQueueType queueType)
: RHICommandBufferPool(device, maxFramesInFlight),
m_QueueType(queueType)
{
    m_VkDevice = static_cast<VkDevice>(device->GetHandle());
    
    (void)maxFramesInFlight;
}

ArisenEngine::RHI::RHIVkCommandBufferPool::~RHIVkCommandBufferPool() noexcept
{
    // Clear the thread-local cache for the current thread.
    // While this only clears it for the thread destroying the pool, address reuse 
    // is most dangerous on the main thread where pools are typically created/destroyed.
    using PoolCache = ThreadLocalCache<RHIVkCommandBufferPool, VkCommandPool, struct PoolTag>;
    PoolCache::Clear();

    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
   
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

RHICommandBuffer* ArisenEngine::RHI::RHIVkCommandBufferPool::GetCommandBuffer(UInt32 currentFrameIndex, ECommandBufferLevel level)
{
    // Ensure this thread has its TLS set up and mailbox consumed
    ThreadLocalFreeList* tlsList = nullptr;
    using FreeListCache = ThreadLocalCache<RHIVkCommandBufferPool, ThreadLocalFreeList*, struct FreeListTag>;
    
    if (!FreeListCache::Get(this, tlsList) || !tlsList)
    {
        tlsList = new ThreadLocalFreeList(); 
        FreeListCache::Set(this, tlsList);
    }

    ConsumeMailbox(tlsList);

    // The base class RHICommandBufferPool handles the m_FreePrimaryCommandBuffers and m_FreeSecondaryCommandBuffers
    // which are populated by InternalRecycle in the global context, but here we prefer TLS first.
    return RHICommandBufferPool::GetCommandBuffer(currentFrameIndex, level);
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
    if (!tlsList) return;

    // First, consume any buffers returned from other threads to this thread's mailbox
    ConsumeMailbox(tlsList);

    if (tlsList->pendingBuffers.empty()) return;

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

void ArisenEngine::RHI::RHIVkCommandBufferPool::ConsumeMailbox(ThreadLocalFreeList* tlsList)
{
    size_t threadIdx = ThreadRegistry::GetThreadIndex();
    RHICommandBuffer* cmd = nullptr;
    while (m_Mailboxes[threadIdx].TryPop(cmd))
    {
        if (cmd)
        {
            cmd->ResetInternal();
            tlsList->freeBuffers.push_back(cmd);
        }
    }
}

void ArisenEngine::RHI::RHIVkCommandBufferPool::InternalRecycle(RHICommandBuffer* commandBuffer)
{
    commandBuffer->ResetInternal();

    auto* vkCmd = static_cast<RHIVkCommandBuffer*>(commandBuffer);
    std::thread::id ownerId = vkCmd->m_OwnerThreadId;
    size_t ownerThreadIdx = vkCmd->m_OwnerThreadIndex;

    // Optimization: If we're on the same thread as the owner, 
    // we can bypass the mailbox and put it straight into TLS free list.
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

    // Cross-thread recycling: Push to the owner's mailbox using lock-free stack
    if (ownerThreadIdx < MAX_THREADS)
    {
        m_Mailboxes[ownerThreadIdx].Push(commandBuffer);
    }
    else
    {
        // Fallback or error: Thread index out of range
        LOG_ERROR("[RHIVkCommandBufferPool::InternalRecycle]: Thread index out of range!");
    }
}

ArisenEngine::RHI::RHICommandBuffer* ArisenEngine::RHI::RHIVkCommandBufferPool::CreateCommandBuffer(ECommandBufferLevel level)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    ASSERT(vkDevice != nullptr);
    
    RHICommandBufferHandle handle = vkDevice->GetCommandBufferPool()->Allocate([this, vkDevice, level](RHIVkCommandBufferItem* item)
    {
        *item = RHIVkCommandBufferItem();
        item->commandBuffer = new RHIVkCommandBuffer(vkDevice, this, level);
        
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

    // Setup TLS cleanup registration if not already done for this thread
    ThreadLocalFreeList* tlsList = nullptr;
    using FreeListCache = ThreadLocalCache<RHIVkCommandBufferPool, ThreadLocalFreeList*, struct FreeListTag>;
    if (!FreeListCache::Get(this, tlsList) || !tlsList)
    {
        tlsList = new ThreadLocalFreeList();
        FreeListCache::Set(this, tlsList);
    }

    if (!tlsList->registeredCleanup)
    {
        ThreadRegistry::RegisterOnExit([this, threadId, tlsList]() {
            // This lambda runs when the thread exits
            VkCommandPool threadPool = VK_NULL_HANDLE;
            {
                std::lock_guard<std::mutex> lock(m_PoolsMutex);
                auto it = m_ThreadPools.find(threadId);
                if (it != m_ThreadPools.end())
                {
                    threadPool = it->second;
                    m_ThreadPools.erase(it);
                }
            }

            if (threadPool != VK_NULL_HANDLE)
            {
                vkDestroyCommandPool(m_VkDevice, threadPool, nullptr);
            }

            // Clean up the ThreadLocalFreeList object itself
            delete tlsList;
            
            // Clear the TLS caches for this thread
            using FreeListCacheInner = ThreadLocalCache<RHIVkCommandBufferPool, ThreadLocalFreeList*, struct FreeListTag>;
            using PoolCacheInner = ThreadLocalCache<RHIVkCommandBufferPool, VkCommandPool, struct PoolTag>;
            FreeListCacheInner::Clear();
            PoolCacheInner::Clear();
        });
        tlsList->registeredCleanup = true;
    }

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
    
    if (m_QueueType == RHIQueueType::Compute)
    {
        poolInfo.queueFamilyIndex = vkDevice->GetComputeFamilyIndex();
    }
    else
    {
        poolInfo.queueFamilyIndex = vkDevice->GetGraphicsFamilyIndex();
    }

    if (vkCreateCommandPool(m_VkDevice, &poolInfo, nullptr, &pool) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkCommandBufferPool::AcquireThreadCommandPool]: failed to create command pool for thread!");
    }

    m_ThreadPools[threadId] = pool;
    PoolCache::Set(this, pool);
    return pool;
}





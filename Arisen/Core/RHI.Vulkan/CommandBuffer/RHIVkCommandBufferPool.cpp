#include "RHIVkCommandBufferPool.h"

#include "RHIVkCommandBuffer.h"
#include "../Devices/RHIVkDevice.h"
#include "../Surfaces/RHIVkSurface.h"

ArisenEngine::RHI::RHIVkCommandBufferPool::RHIVkCommandBufferPool(RHIVkDevice* device, UInt32 maxFramesInFlight)
: RHICommandBufferPool(device, maxFramesInFlight)
{
    m_VkDevice = static_cast<VkDevice>(device->GetHandle());
    
    (void)maxFramesInFlight;
}

ArisenEngine::RHI::RHIVkCommandBufferPool::~RHIVkCommandBufferPool() noexcept
{
    m_OwnedCommandBuffers.clear();
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
    
    if (FreeListCache::Get(this, tlsList) && tlsList && !tlsList->freeBuffers.empty())
    {
        RHICommandBuffer* commandBuffer = tlsList->freeBuffers.back();
        tlsList->freeBuffers.pop_back();
        return commandBuffer;
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
    RHICommandBufferPool::ReleaseCommandBuffer(currentFrameIndex, commandBuffer);
}

void ArisenEngine::RHI::RHIVkCommandBufferPool::InternalRecycle(RHICommandBuffer* commandBuffer)
{
    commandBuffer->ResetInternal();

    auto* vkCmd = static_cast<RHIVkCommandBuffer*>(commandBuffer);
    std::thread::id ownerId = vkCmd->m_OwnerThreadId;

    std::lock_guard<std::mutex> lock(m_PoolsMutex);
    m_ThreadFreeBuffers[ownerId].emplace_back(commandBuffer);
}

ArisenEngine::RHI::RHICommandBuffer* ArisenEngine::RHI::RHIVkCommandBufferPool::CreateCommandBuffer()
{
    auto* vkDevice = dynamic_cast<RHIVkDevice*>(m_Device);
    ASSERT(vkDevice != nullptr);
    std::lock_guard<std::mutex> lock(m_PoolsMutex); // Protect m_OwnedCommandBuffers
    m_OwnedCommandBuffers.emplace_back(std::make_unique<RHIVkCommandBuffer>(vkDevice, this));
    return m_OwnedCommandBuffers.back().get();
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

    auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);

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

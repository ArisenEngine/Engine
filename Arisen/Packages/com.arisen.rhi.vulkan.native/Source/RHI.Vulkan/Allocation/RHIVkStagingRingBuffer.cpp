#include "Allocation/RHIVkStagingRingBuffer.h"
#include "Allocation/RHIVkMemoryAllocator.h"
#include "Logger/Logger.h"
#include "Profiler.h"

using namespace ArisenEngine;
using namespace ArisenEngine::RHI;

RHIVkStagingRingBuffer::RHIVkStagingRingBuffer(VkDevice device, RHIVkMemoryAllocator* allocator, UInt64 capacity)
    : m_Device(device)
    , m_VmaAllocator(allocator->GetVmaAllocator())
    , m_RingAllocator(capacity)
{
    // Create a single large host-visible buffer for staging
    VkBufferCreateInfo bufferInfo{};
    bufferInfo.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
    bufferInfo.size = capacity;
    bufferInfo.usage = VK_BUFFER_USAGE_TRANSFER_SRC_BIT;
    bufferInfo.sharingMode = VK_SHARING_MODE_EXCLUSIVE;

    VmaAllocationCreateInfo allocCreateInfo{};
    allocCreateInfo.usage = VMA_MEMORY_USAGE_CPU_TO_GPU;
    allocCreateInfo.flags = VMA_ALLOCATION_CREATE_MAPPED_BIT
                          | VMA_ALLOCATION_CREATE_HOST_ACCESS_SEQUENTIAL_WRITE_BIT;

    VmaAllocationInfo allocInfo{};
    VkResult result = vmaCreateBuffer(m_VmaAllocator, &bufferInfo, &allocCreateInfo,
                                       &m_Buffer, &m_Allocation, &allocInfo);
    if (result != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkStagingRingBuffer]: Failed to create staging ring buffer!");
    }

    m_MappedBase = allocInfo.pMappedData;
    if (!m_MappedBase)
    {
        LOG_FATAL_AND_THROW("[RHIVkStagingRingBuffer]: Staging buffer not persistently mapped!");
    }

    LOG_INFOF("[RHIVkStagingRingBuffer]: Created staging ring buffer, capacity: {0} MB",
              (UInt32)(capacity / (1024 * 1024)));
}

RHIVkStagingRingBuffer::~RHIVkStagingRingBuffer()
{
    if (m_Buffer != VK_NULL_HANDLE && m_Allocation != VK_NULL_HANDLE)
    {
        vmaDestroyBuffer(m_VmaAllocator, m_Buffer, m_Allocation);
        m_Buffer = VK_NULL_HANDLE;
        m_Allocation = VK_NULL_HANDLE;
        m_MappedBase = nullptr;
    }
}

std::optional<RHIVkStagingRingBuffer::Allocation> RHIVkStagingRingBuffer::Allocate(UInt64 size, UInt64 alignment)
{
    ARISEN_PROFILE_ZONE("StagingRingBuffer::Allocate");

    auto result = m_RingAllocator.Allocate(size, alignment);
    if (!result.has_value())
    {
        return std::nullopt;
    }

    return Allocation{
        m_Buffer,
        result->offset,
        result->size,
        static_cast<uint8_t*>(m_MappedBase) + result->offset
    };
}

void RHIVkStagingRingBuffer::FlushRegion(UInt64 offset, UInt64 size)
{
    vmaFlushAllocation(m_VmaAllocator, m_Allocation, offset, size);
}

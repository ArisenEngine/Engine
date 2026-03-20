#pragma once

#include "RHI/Allocation/RingAllocator.h"
#include <vulkan/vulkan_core.h>
#include <vma/vk_mem_alloc.h>
#include <optional>
#include <memory>

namespace ArisenEngine::RHI
{
    class RHIVkMemoryAllocator;

    /**
     * @brief Vulkan-specific staging ring buffer for GPU uploads.
     * 
     * Owns a single persistently mapped VkBuffer backed by VMA,
     * delegates offset management to a RingAllocator.
     */
    class RHIVkStagingRingBuffer
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkStagingRingBuffer)

        RHIVkStagingRingBuffer(VkDevice device, RHIVkMemoryAllocator* allocator, UInt64 capacity);
        ~RHIVkStagingRingBuffer();

        struct Allocation
        {
            VkBuffer buffer;     // The ring buffer's VkBuffer
            UInt64   offset;     // Offset within the ring buffer
            UInt64   size;       // Allocated size
            void*    mappedPtr;  // CPU-writable pointer (already mapped + offset applied)
        };

        /**
         * @brief Allocate staging space and return a writable pointer.
         * @return Allocation with buffer, offset, and mapped pointer, or nullopt if full.
         */
        std::optional<Allocation> Allocate(UInt64 size, UInt64 alignment = 256);

        /**
         * @brief Flush the written region for non-coherent memory.
         * Call after memcpy to ensure GPU visibility.
         */
        void FlushRegion(UInt64 offset, UInt64 size);

        void MarkTicket(RHIGpuTicket ticket) { m_RingAllocator.MarkTicket(ticket); }
        void ReclaimUpTo(RHIGpuTicket completedTicket) { m_RingAllocator.ReclaimUpTo(completedTicket); }

        UInt64 GetCapacity() const { return m_RingAllocator.GetCapacity(); }
        UInt64 GetAvailableSpace() const { return m_RingAllocator.GetAvailableSpace(); }

    private:
        VkDevice m_Device{VK_NULL_HANDLE};
        VmaAllocator m_VmaAllocator{VK_NULL_HANDLE};

        VkBuffer m_Buffer{VK_NULL_HANDLE};
        VmaAllocation m_Allocation{VK_NULL_HANDLE};
        void* m_MappedBase{nullptr};

        RingAllocator m_RingAllocator;
    };
}

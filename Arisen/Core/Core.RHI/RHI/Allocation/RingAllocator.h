#pragma once

#include "Base/FoundationMinimal.h"
#include "RHI/Resources/RHIDeferredDeletionQueue.h" // RHIGpuTicket

// Renamed GetFreeSpace to GetAvailableSpace to avoid Windows macro collisions

#include <optional>
#include <vector>

#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    /**
     * @brief Backend-agnostic circular ring allocator for staging uploads.
     * 
     * Pure CPU-side offset math — no GPU objects. Tracks write/read offsets
     * and per-ticket watermarks for reclamation.
     * 
     * Designed to be composed into backend-specific staging buffers
     * (e.g., RHIVkStagingRingBuffer wraps a VkBuffer + RingAllocator).
     */
    class RHI_DLL RingAllocator
    {
    public:
        explicit RingAllocator(UInt64 capacity);

        struct Allocation
        {
            UInt64 offset;
            UInt64 size;
        };

        /**
         * @brief Bump-allocate from the ring.
         * @param size      Number of bytes to allocate.
         * @param alignment Required alignment (default 256, Vulkan min texel buffer alignment).
         * @return The allocation with offset and size, or nullopt if the ring is full.
         */
        std::optional<Allocation> Allocate(UInt64 size, UInt64 alignment = 256);

        /**
         * @brief Record the ticket associated with the current write position.
         * Must be called after a batch of allocations, before GPU submission.
         */
        void MarkTicket(RHIGpuTicket ticket);

        /**
         * @brief Advance the read pointer to reclaim space used by completed GPU work.
         * @param completedTicket The latest ticket confirmed completed by the GPU.
         */
        void ReclaimUpTo(RHIGpuTicket completedTicket);

        UInt64 GetCapacity() const;
        UInt64 GetUsedSpace() const;
        UInt64 GetAvailableSpace() const;

    private:
        static UInt64 AlignUp(UInt64 value, UInt64 alignment);

        UInt64 m_Capacity;
        UInt64 m_WriteOffset{0};
        UInt64 m_ReadOffset{0};
        UInt64 m_UsedSpace{0};

        // Per-ticket watermarks: (ticket, writeOffset at time of mark)
        struct Watermark
        {
            RHIGpuTicket ticket;
            UInt64 offset;
        };

        std::vector<Watermark> m_Watermarks;
    };
}

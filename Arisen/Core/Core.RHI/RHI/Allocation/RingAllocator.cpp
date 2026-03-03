#include "RHI/Allocation/RingAllocator.h"

#include <algorithm>

using namespace ArisenEngine;
using namespace ArisenEngine::RHI;

RingAllocator::RingAllocator(UInt64 capacity)
    : m_Capacity(capacity)
{
}

UInt64 RingAllocator::AlignUp(UInt64 value, UInt64 alignment)
{
    return (value + alignment - 1) & ~(alignment - 1);
}

UInt64 RingAllocator::GetCapacity() const { return m_Capacity; }
UInt64 RingAllocator::GetUsedSpace() const { return m_UsedSpace; }
UInt64 RingAllocator::GetAvailableSpace() const { return m_Capacity - m_UsedSpace; }

std::optional<RingAllocator::Allocation> RingAllocator::Allocate(UInt64 size, UInt64 alignment)
{
    if (size == 0 || size > m_Capacity)
    {
        return std::nullopt;
    }

    UInt64 alignedOffset = AlignUp(m_WriteOffset, alignment);
    UInt64 alignmentWaste = alignedOffset - m_WriteOffset;
    UInt64 totalSize = alignmentWaste + size;

    // Check if allocation wraps past the end of the ring
    if (alignedOffset + size > m_Capacity)
    {
        // Wrap around: waste the remaining space at the end and try from offset 0
        UInt64 wastedTail = m_Capacity - m_WriteOffset;
        alignedOffset = AlignUp(0, alignment);
        totalSize = wastedTail + alignedOffset + size;

        if (totalSize > GetAvailableSpace())
        {
            return std::nullopt; // Not enough space even after wrap
        }

        m_WriteOffset = alignedOffset + size;
        m_UsedSpace += totalSize;
        return Allocation{alignedOffset, size};
    }

    if (totalSize > GetAvailableSpace())
    {
        return std::nullopt; // Ring is full
    }

    m_WriteOffset = alignedOffset + size;
    m_UsedSpace += totalSize;

    return Allocation{alignedOffset, size};
}

void RingAllocator::MarkTicket(RHIGpuTicket ticket)
{
    m_Watermarks.push_back({ticket, m_WriteOffset});
}

void RingAllocator::ReclaimUpTo(RHIGpuTicket completedTicket)
{
    // Find the latest watermark that has been completed
    UInt64 reclaimOffset = m_ReadOffset;
    size_t reclaimCount = 0;

    for (size_t i = 0; i < m_Watermarks.size(); ++i)
    {
        if (m_Watermarks[i].ticket <= completedTicket)
        {
            reclaimOffset = m_Watermarks[i].offset;
            reclaimCount = i + 1;
        }
        else
        {
            break; // Watermarks are monotonically increasing
        }
    }

    if (reclaimCount > 0)
    {
        // Calculate reclaimed space
        if (reclaimOffset >= m_ReadOffset)
        {
            m_UsedSpace -= (reclaimOffset - m_ReadOffset);
        }
        else
        {
            // Wrapped around
            m_UsedSpace -= (m_Capacity - m_ReadOffset) + reclaimOffset;
        }

        m_ReadOffset = reclaimOffset;
        m_Watermarks.erase(m_Watermarks.begin(), m_Watermarks.begin() + reclaimCount);
    }
}

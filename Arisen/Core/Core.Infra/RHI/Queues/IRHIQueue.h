#pragma once

#include "Common/CommandHeaders.h"
#include "RHI/Utils/RHIDeferredDeletionQueue.h"

namespace ArisenEngine::RHI
{
    class RHICommandBuffer;

    enum class RHIQueueType : UInt8
    {
        Graphics,
        Compute,
        Transfer,
        Present,
    };

    // RHI-level queue abstraction:
    // - Submit produces a monotonic ticket (submitID / fenceValue / completion serial)
    // - Update advances completed ticket and may trigger automatic GC
    class IRHIQueue
    {
    public:
        virtual ~IRHIQueue() = default;

        virtual RHIQueueType GetType() const = 0;

        // Returns a ticket assigned to this submission.
        virtual RHIGpuTicket Submit(RHICommandBuffer* commandBuffer) = 0;

        // Poll completion and advance completed ticket.
        virtual void Update() = 0;

        virtual RHIGpuTicket GetCompletedTicket() const = 0;
    };
}


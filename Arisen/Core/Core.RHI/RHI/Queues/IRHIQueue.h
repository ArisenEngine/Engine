#pragma once

#include "Base/FoundationMinimal.h"
#include "RHI/Resources/RHIDeferredDeletionQueue.h"
#include "RHI/Queues/RHIQueueType.h"

namespace ArisenEngine::RHI
{
    class RHICommandBuffer;

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

        // Returns the most recently assigned ticket on this queue (monotonic).
        // Useful for scheduling deferred deletion at "everything submitted so far must be complete".
        virtual RHIGpuTicket GetLatestTicket() const = 0;

        // Block until the specified ticket is completed.
        // Default implementation polls Update without sleeping.
        virtual void WaitForTicket(RHIGpuTicket ticket)
        {
            while (GetCompletedTicket() < ticket)
            {
                Update();
            }
        }
    };
}



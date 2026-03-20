#pragma once

#include "Base/FoundationMinimal.h"
#include "RHI/Resources/RHIDeferredDeletionQueue.h"
#include <functional>
#include <mutex>
#include <utility>
#include <map>

namespace ArisenEngine::RHI
{
    // A simple, thread-safe deferred deletion system.
    // Enqueue destructions into a per-frame bucket and flush once that frame's fence is signaled.
    class RHIVkDeferredDeletion final : public IRHIDeferredDeletionQueue
    {
    public:
        explicit RHIVkDeferredDeletion(UInt32 maxFramesInFlight);

        // IRHIDeferredDeletionQueue
        void Enqueue(const RHIDeletionDependencies& deps, RHIDeferredDeleteItem item) override;
        void Flush(RHIQueueType queue, RHIGpuTicket ticket) override;

    private:
        struct PendingEntry
        {
            RHIDeletionDependencies deps;
            RHIDeferredDeleteItem item;
        };

        UInt32 m_MaxFramesInFlight{0};
        RHIGpuTicket m_CompletedTickets[4]{0, 0, 0, 0};
        Containers::Vector<PendingEntry> m_Pending;
        std::mutex m_Mutex;
    };
}

#pragma once

#include "Base/FoundationMinimal.h"
#include "RHI/Utils/RHIDeferredDeletionQueue.h"
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
        void Enqueue(RHIQueueType queue, RHIGpuTicket ticket, RHIDeferredDeleteItem item) override;
        void Flush(RHIQueueType queue, RHIGpuTicket ticket) override;

    private:
        UInt32 m_MaxFramesInFlight {0}; // kept for future ring-based strategies / diagnostics
        std::map<RHIQueueType, std::map<RHIGpuTicket, Containers::Vector<RHIDeferredDeleteItem>>> m_Pending;
        std::mutex m_Mutex;
    };
}



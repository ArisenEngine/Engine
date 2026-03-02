#pragma once

#include "Base/FoundationMinimal.h"
#include "RHI/Queues/RHIQueueType.h"

#include <functional>

namespace ArisenEngine::RHI
{
    // Backend-agnostic "GPU completion ticket".
    // - Vulkan can use frameIndex or timeline value
    // - DX12 uses fenceValue
    // - Metal can map to commandBuffer completion serial
    using RHIGpuTicket = UInt64;

    // Set of tickets across different queue types that must be satisfied 
    // before a resource can be safely destroyed.
    struct RHIDeletionDependencies final
    {
        RHIGpuTicket tickets[4]{0, 0, 0, 0}; // Index matches RHIQueueType enum values

        void Update(RHIQueueType queue, RHIGpuTicket ticket)
        {
            auto idx = static_cast<UInt32>(queue);
            if (idx < 4 && ticket > tickets[idx])
            {
                tickets[idx] = ticket;
            }
        }

        bool IsSatisfied(RHIQueueType queue, RHIGpuTicket completedTicket) const
        {
            auto idx = static_cast<UInt32>(queue);
            return idx >= 4 || completedTicket >= tickets[idx];
        }

        bool IsFullySatisfied(const RHIGpuTicket* completedTickets) const
        {
            for (int i = 0; i < 4; ++i)
            {
                if (completedTickets[i] < tickets[i]) return false;
            }
            return true;
        }
    };

    // A type-erased delete item.
    struct RHIDeferredDeleteItem final
    {
        void* ptr{nullptr};
        void (*deleter)(void*){nullptr};
    };

    template <typename T>
    inline RHIDeferredDeleteItem MakeDeferredDeleteItem(T* p)
    {
        return RHIDeferredDeleteItem{
            p,
            +[](void* x)
            {
                delete static_cast<T*>(x);
            },
        };
    }

    class IRHIDeferredDeletionQueue
    {
    public:
        virtual ~IRHIDeferredDeletionQueue() = default;

        // Enqueue an object to delete when all its dependencies are satisfied.
        virtual void Enqueue(const RHIDeletionDependencies& deps, RHIDeferredDeleteItem item) = 0;

        // Flush all work that is known-safe for a specific queue's progress.
        virtual void Flush(RHIQueueType queue, RHIGpuTicket ticket) = 0;
    };
}

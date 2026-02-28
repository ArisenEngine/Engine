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

    // A type-erased delete item. This is intentionally C-FFI friendly:
    // - ptr: object pointer
    // - deleter: function pointer (no captures)
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

        // Enqueue an object to delete when it's safe (based on ticket).
        virtual void Enqueue(RHIQueueType queue, RHIGpuTicket ticket, RHIDeferredDeleteItem item) = 0;

        // Flush all work that is known-safe up to the given ticket.
        // Implementations may treat this as "flush bucket(ticket)" (frame index),
        // or "flush <= ticket" (monotonic fences).
        virtual void Flush(RHIQueueType queue, RHIGpuTicket ticket) = 0;
    };
}

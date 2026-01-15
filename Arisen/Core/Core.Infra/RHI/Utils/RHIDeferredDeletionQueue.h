#pragma once

#include "Common/CommandHeaders.h"

#include <functional>

namespace ArisenEngine::RHI
{
    // Backend-agnostic "GPU completion ticket".
    // - Vulkan can use frameIndex or timeline value
    // - DX12 uses fenceValue
    // - Metal can map to commandBuffer completion serial
    using RHIGpuTicket = UInt64;

    class IRHIDeferredDeletionQueue
    {
    public:
        virtual ~IRHIDeferredDeletionQueue() = default;

        // Enqueue an action to run when it's safe (based on ticket).
        virtual void Enqueue(RHIGpuTicket ticket, std::function<void()>&& fn) = 0;

        // Flush all work that is known-safe up to the given ticket.
        // Implementations may treat this as "flush bucket(ticket)" (frame index),
        // or "flush <= ticket" (monotonic fences).
        virtual void Flush(RHIGpuTicket ticket) = 0;
    };
}


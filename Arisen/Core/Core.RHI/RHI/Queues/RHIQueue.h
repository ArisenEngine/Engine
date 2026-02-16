#pragma once

#include "Base/FoundationMinimal.h"
#include "RHI/Resources/RHIDeferredDeletionQueue.h"
#include "RHI/Queues/RHIQueueType.h"

#include "RHI/Handles/RHIHandle.h" // Needed for RHISemaphoreHandle
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHICommandBuffer;
    class RHISwapChain; // Forward declaration

    class RHISwapChain; // Forward declaration

    // Descriptor for submission synchronization
    struct RHISubmitDescriptor
    {
        class RHISwapChain* WaitSwapChain = nullptr;   // Optional: Waits for local frame's ImageAvailable
        class RHISwapChain* SignalSwapChain = nullptr; // Optional: Signals local frame's RenderFinished

        // Explicit semaphores (optional, for Async Compute / non-swapchain sync)
        RHISemaphoreHandle* pWaitSemaphores = nullptr;
        const UInt32* pWaitDstStageMask = nullptr; // EPipelineStageFlag bits
        const uint64_t* pWaitValues = nullptr; // Optional: Timeline wait values
        UInt32 waitSemaphoreCount = 0;
        
        RHISemaphoreHandle* pSignalSemaphores = nullptr;
        const uint64_t* pSignalValues = nullptr; // Optional: Timeline signal values
        UInt32 signalSemaphoreCount = 0;
    };

    // RHI-level queue abstraction:
    // - Submit produces a monotonic ticket (submitID / fenceValue / completion serial)
    // - Update advances completed ticket and may trigger automatic GC
    class RHI_DLL RHIQueue
    {
    public:
        virtual ~RHIQueue();

        virtual RHIQueueType GetType() const = 0;

        // Returns a ticket assigned to this submission.
        virtual RHIGpuTicket Submit(RHICommandBufferHandle commandBuffer, const RHISubmitDescriptor* descriptor = nullptr) = 0;

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



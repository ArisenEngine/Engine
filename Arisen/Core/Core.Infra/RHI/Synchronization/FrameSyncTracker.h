#pragma once
#include "RHI/Queues/IRHIQueue.h"
#include "../../Common/CommandHeaders.h"
#include <atomic>
#include <memory>

namespace ArisenEngine::RHI
{
    // Tracks per-frame submit tickets and provides CPU<->GPU sync helpers.
    class FrameSyncTracker
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(FrameSyncTracker)
        explicit FrameSyncTracker(UInt32 maxFramesInFlight);

        void OnSubmit(UInt32 frameIndex, RHIGpuTicket submitId);

        // Block until the last submit for this frame-slot has completed.
        void Wait(UInt32 frameIndex, IRHIQueue* queue);

        // Drain all tracked frame-slots (useful for shutdown).
        void Drain(IRHIQueue* queue);

    private:
        Containers::Vector<std::atomic<RHIGpuTicket>> m_FrameLastSubmitId;
    };
}

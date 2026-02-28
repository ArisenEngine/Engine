#pragma once
#include "RHI/Queues/RHIQueue.h"
#include "Base/FoundationMinimal.h"
#include <atomic>
#include <memory>

#include "../Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    // Tracks per-frame submit tickets and provides CPU<->GPU sync helpers.
    class RHI_DLL FrameSyncTracker
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(FrameSyncTracker)
        explicit FrameSyncTracker(UInt32 maxFramesInFlight);

        void OnSubmit(UInt32 frameIndex, RHIGpuTicket submitTicket);

        // Block until the last submit for this frame-slot has completed.
        void Wait(UInt32 frameIndex, RHIQueue* queue);

        // Drain all tracked frame-slots (useful for shutdown).
        void Drain(RHIQueue* queue);

    private:
        std::unique_ptr<std::atomic<RHIGpuTicket>[]> m_FrameLatestSubmitTicket;
        UInt32 m_FrameCount = 0;
    };
}

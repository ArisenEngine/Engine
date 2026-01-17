#include "FrameSyncTracker.h"
ArisenEngine::RHI::FrameSyncTracker::FrameSyncTracker(UInt32 maxFramesInFlight)
{
    m_FrameCount = maxFramesInFlight;
    m_FrameLastSubmitId = std::make_unique<std::atomic<RHIGpuTicket>[]>(maxFramesInFlight);
    for (UInt32 i = 0; i < maxFramesInFlight; ++i)
    {
        m_FrameLastSubmitId[i].store(0, std::memory_order_relaxed);
    }
}

void ArisenEngine::RHI::FrameSyncTracker::OnSubmit(UInt32 frameIndex, RHIGpuTicket submitId)
{
    if (m_FrameCount == 0)
    {
        return;
    }
    m_FrameLastSubmitId[frameIndex % m_FrameCount].store(submitId, std::memory_order_release);
}

void ArisenEngine::RHI::FrameSyncTracker::Wait(UInt32 frameIndex, IRHIQueue* queue)
{
    if (queue == nullptr || m_FrameCount == 0)
    {
        return;
    }

    const auto index = frameIndex % m_FrameCount;
    const auto target = m_FrameLastSubmitId[index].load(std::memory_order_acquire);
    if (target == 0)
    {
        return;
    }

    queue->WaitForTicket(target);
}

void ArisenEngine::RHI::FrameSyncTracker::Drain(IRHIQueue* queue)
{
    if (queue == nullptr || m_FrameCount == 0)
    {
        return;
    }

    RHIGpuTicket maxTarget = 0;
    for (UInt32 i = 0; i < m_FrameCount; ++i)
    {
        const auto t = m_FrameLastSubmitId[i].load(std::memory_order_acquire);
        if (t > maxTarget) maxTarget = t;
    }
    
    if (maxTarget == 0)
    {
        return;
    }

    queue->WaitForTicket(maxTarget);
}

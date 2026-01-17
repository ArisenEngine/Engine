#include "FrameSyncTracker.h"
ArisenEngine::RHI::FrameSyncTracker::FrameSyncTracker(UInt32 maxFramesInFlight)
{
    m_FrameLastSubmitId.resize(maxFramesInFlight);
    for (UInt32 i = 0; i < maxFramesInFlight; ++i)
    {
        m_FrameLastSubmitId[i].store(0, std::memory_order_relaxed);
    }
}

void ArisenEngine::RHI::FrameSyncTracker::OnSubmit(UInt32 frameIndex, RHIGpuTicket submitId)
{
    if (m_FrameLastSubmitId.empty())
    {
        return;
    }
    m_FrameLastSubmitId[frameIndex % m_FrameLastSubmitId.size()].store(submitId, std::memory_order_release);
}

void ArisenEngine::RHI::FrameSyncTracker::Wait(UInt32 frameIndex, IRHIQueue* queue)
{
    if (queue == nullptr || m_FrameLastSubmitId.empty())
    {
        return;
    }

    const auto index = frameIndex % m_FrameLastSubmitId.size();
    const auto target = m_FrameLastSubmitId[index].load(std::memory_order_acquire);
    if (target == 0)
    {
        return;
    }

    queue->WaitForTicket(target);
}

void ArisenEngine::RHI::FrameSyncTracker::Drain(IRHIQueue* queue)
{
    if (queue == nullptr || m_FrameLastSubmitId.empty())
    {
        return;
    }

    RHIGpuTicket maxTarget = 0;
    for (const auto& v : m_FrameLastSubmitId)
    {
        const auto t = v.load(std::memory_order_acquire);
        if (t > maxTarget) maxTarget = t;
    }
    if (maxTarget == 0)
    {
        return;
    }

    queue->WaitForTicket(maxTarget);
}

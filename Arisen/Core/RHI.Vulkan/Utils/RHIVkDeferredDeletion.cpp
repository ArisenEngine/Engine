#include "RHIVkDeferredDeletion.h"

ArisenEngine::RHI::RHIVkDeferredDeletion::RHIVkDeferredDeletion(UInt32 maxFramesInFlight)
    : m_MaxFramesInFlight(maxFramesInFlight)
{
}

void ArisenEngine::RHI::RHIVkDeferredDeletion::Enqueue(RHIGpuTicket ticket, std::function<void()>&& fn)
{
    {
        std::lock_guard<std::mutex> lock(m_Mutex);
        m_Pending[ticket].emplace_back(std::move(fn));
    }
}

void ArisenEngine::RHI::RHIVkDeferredDeletion::Flush(RHIGpuTicket ticket)
{
    Containers::Vector<std::function<void()>> toRun;
    {
        std::lock_guard<std::mutex> lock(m_Mutex);
        auto it = m_Pending.begin();
        while (it != m_Pending.end() && it->first <= ticket)
        {
            auto& bucket = it->second;
            for (auto& fn : bucket)
            {
                toRun.emplace_back(std::move(fn));
            }
            it = m_Pending.erase(it);
        }
    }

    for (auto& fn : toRun)
    {
        if (fn) fn();
    }
}


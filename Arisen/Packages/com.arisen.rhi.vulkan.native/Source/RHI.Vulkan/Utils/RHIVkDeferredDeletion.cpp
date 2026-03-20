#include "Utils/RHIVkDeferredDeletion.h"
#include "Logger/Logger.h"
#include <iostream>

ArisenEngine::RHI::RHIVkDeferredDeletion::RHIVkDeferredDeletion(UInt32 maxFramesInFlight)
    : m_MaxFramesInFlight(maxFramesInFlight)
{
}

void ArisenEngine::RHI::RHIVkDeferredDeletion::Enqueue(const RHIDeletionDependencies& deps,
                                                       RHIDeferredDeleteItem item)
{
    std::lock_guard<std::mutex> lock(m_Mutex);
    m_Pending.push_back({ deps, item });
}

void ArisenEngine::RHI::RHIVkDeferredDeletion::Flush(RHIQueueType queue, RHIGpuTicket ticket)
{
    Containers::Vector<RHIDeferredDeleteItem> toRun;
    {
        std::lock_guard<std::mutex> lock(m_Mutex);
        
        // Update completed tracking for this queue
        auto idx = static_cast<UInt32>(queue);
        if (idx < 4 && ticket > m_CompletedTickets[idx])
        {
            m_CompletedTickets[idx] = ticket;
        }

        // Check which items are fully satisfied
        for (auto it = m_Pending.begin(); it != m_Pending.end(); )
        {
            if (it->deps.IsFullySatisfied(m_CompletedTickets))
            {
                toRun.push_back(it->item);
                it = m_Pending.erase(it);
            }
            else
            {
                ++it;
            }
        }
    }

    for (auto& item : toRun)
    {
        if (item.deleter && item.ptr)
        {
            item.deleter(item.ptr);
        }
    }
}


#include "RHIVkDeferredDeletion.h"
#include "Logger/Logger.h"
#include <iostream>

ArisenEngine::RHI::RHIVkDeferredDeletion::RHIVkDeferredDeletion(UInt32 maxFramesInFlight)
    : m_MaxFramesInFlight(maxFramesInFlight)
{
}

void ArisenEngine::RHI::RHIVkDeferredDeletion::Enqueue(RHIQueueType queue, RHIGpuTicket ticket, RHIDeferredDeleteItem item)
{
    {
        std::lock_guard<std::mutex> lock(m_Mutex);
        m_Pending[queue][ticket].emplace_back(item);
    }
}

void ArisenEngine::RHI::RHIVkDeferredDeletion::Flush(RHIQueueType queue, RHIGpuTicket ticket)
{
    Containers::Vector<RHIDeferredDeleteItem> toRun;
    {
        std::lock_guard<std::mutex> lock(m_Mutex);
        auto& pending = m_Pending[queue];

        LOG_DEBUG("[RHIVkDeferredDeletion::Flush]: Queue " + std::to_string((int)queue) + ", Ticket " + std::to_string(ticket) + ", Pending Count: " + std::to_string(pending.size()));
        auto it = pending.begin();
        int count = 0;
        while (it != pending.end() && it->first <= ticket)
        {
            auto& bucket = it->second;
            LOG_DEBUG("[RHIVkDeferredDeletion::Flush]: Processing Ticket " + std::to_string(it->first) + ", Items: " + std::to_string(bucket.size()));
            for (auto& item : bucket)
            {
                toRun.emplace_back(item);
            }
            it = pending.erase(it);
            count++;
        }
        LOG_DEBUG("[RHIVkDeferredDeletion::Flush]: Extracted " + std::to_string(count) + " buckets");
    }

    int runCount = 0;
    for (auto& item : toRun)
    {
        if (item.deleter && item.ptr) 
        {
            LOG_DEBUG("[RHIVkDeferredDeletion::Flush]: Running Deleter");
            item.deleter(item.ptr);
        }
        runCount++;
    }
    LOG_DEBUG("[RHIVkDeferredDeletion::Flush]: Finished Running Deleters");
}


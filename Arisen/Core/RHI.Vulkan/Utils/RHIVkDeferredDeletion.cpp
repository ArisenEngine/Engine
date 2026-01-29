
#include "Utils/RHIVkDeferredDeletion.h"
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
        auto it = pending.begin();
        int count = 0;
        while (it != pending.end() && it->first <= ticket)
        {
            auto& bucket = it->second;
            for (auto& item : bucket)
            {
                toRun.emplace_back(item);
            }
            it = pending.erase(it);
            count++;
        }
    }

    int runCount = 0;
    for (auto& item : toRun)
    {
        if (item.deleter && item.ptr) 
        {
            item.deleter(item.ptr);
        }
        runCount++;
    }
}






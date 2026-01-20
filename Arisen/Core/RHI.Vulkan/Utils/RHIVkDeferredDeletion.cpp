
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

        std::cout << "[RHIVkDeferredDeletion::Flush]: Queue " << (int)queue << ", Ticket " << ticket << ", Pending Count: " << pending.size() << std::endl;
        auto it = pending.begin();
        int count = 0;
        while (it != pending.end() && it->first <= ticket)
        {
            auto& bucket = it->second;
            std::cout << "[RHIVkDeferredDeletion::Flush]: Processing Ticket " << it->first << ", Items: " << bucket.size() << std::endl;
            for (auto& item : bucket)
            {
                toRun.emplace_back(item);
            }
            it = pending.erase(it);
            count++;
        }
        std::cout << "[RHIVkDeferredDeletion::Flush]: Extracted " << count << " buckets" << std::endl;
    }

    int runCount = 0;
    for (auto& item : toRun)
    {
        if (item.deleter && item.ptr) 
        {
            std::cout << "[RHIVkDeferredDeletion::Flush]: Running Deleter " << runCount << ", Ptr: " << item.ptr << std::endl;
            item.deleter(item.ptr);
        }
        runCount++;
    }
    std::cout << "[RHIVkDeferredDeletion::Flush]: Finished Running Deleters" << std::endl;
}


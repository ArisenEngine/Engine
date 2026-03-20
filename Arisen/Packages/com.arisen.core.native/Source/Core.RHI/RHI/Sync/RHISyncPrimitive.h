#pragma once
#include "Base/FoundationMinimal.h"
#include "../Handles/RHIHandle.h"

namespace ArisenEngine::RHI
{
    /**
     * @brief Interface for synchronization primitives (Fences, Semaphores).
     */
    class RHISyncPrimitive
    {
    public:
        virtual ~RHISyncPrimitive() = default;


        // Timeline Sync (Semaphores)
        virtual void WaitSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) = 0;
        virtual void SignalSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) = 0;
        virtual UInt64 GetSemaphoreValue(RHISemaphoreHandle handle) = 0;
    };
}

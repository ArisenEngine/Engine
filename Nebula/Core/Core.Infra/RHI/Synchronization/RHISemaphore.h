#pragma once
#include "RHI/Synchronization/SynchObject.h"

namespace NebulaEngine::RHI
{
    class RHISemaphore : public SynchObject
    {
    public:
        NO_COPY_NO_MOVE(RHISemaphore)
        RHISemaphore() = default;
        ~RHISemaphore() noexcept override = default;

        void* GetHandle() override = 0;
        void Lock() override = 0;
        void Unlock() override = 0;
    };
}
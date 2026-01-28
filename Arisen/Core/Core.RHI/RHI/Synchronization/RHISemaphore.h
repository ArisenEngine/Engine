#pragma once
#include "Threadable/SemaphoreObject.h"

namespace ArisenEngine::RHI
{
    class RHISemaphore : public Threadable::SemaphoreObject
    {
    public:
        NO_COPY_NO_MOVE(RHISemaphore)
        RHISemaphore() = default;
        ~RHISemaphore() noexcept override = default;

        void* GetHandle() override = 0;
        void Wait() override = 0;
        void Signal() override = 0;
    };
}

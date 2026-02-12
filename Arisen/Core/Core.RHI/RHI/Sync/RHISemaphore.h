#pragma once
#include "Concurrency/Semaphore.h"

namespace ArisenEngine::RHI
{
    class RHISemaphore : public Concurrency::Semaphore
    {
    public:
        NO_COPY_NO_MOVE(RHISemaphore)
        RHISemaphore() = default;
        ~RHISemaphore() noexcept override = default;

        void* GetHandle() override = 0;
        void Wait() override = 0;
        void Signal() override = 0;

        // Timeline semaphore support
        virtual void Wait(uint64_t value) = 0;
        virtual void Signal(uint64_t value) = 0;
        virtual uint64_t GetValue() = 0;
        virtual bool IsTimeline() const = 0;
    };
}


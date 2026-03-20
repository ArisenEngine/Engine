#pragma once
#include "Concurrency/Semaphore.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHI_DLL RHISemaphore : public Concurrency::Semaphore
    {
    public:
        NO_COPY_NO_MOVE(RHISemaphore)
        RHISemaphore();
        ~RHISemaphore() noexcept override;

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

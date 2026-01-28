#pragma once
#include "Concurrency/SyncObject.h"

namespace ArisenEngine::RHI
{
    class RHIFence : public virtual Concurrency::SyncObject
    {
    public:
        NO_COPY_NO_MOVE(RHIFence)
        RHIFence() = default;
        ~RHIFence() noexcept override = default;

        void* GetHandle() override { return nullptr; }
        bool Lock() override  { return true; }
        void Unlock() override { }
    };
}


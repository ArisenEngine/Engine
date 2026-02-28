#pragma once
#include "Concurrency/SyncObject.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHI_DLL RHIFence : public virtual Concurrency::SyncObject
    {
    public:
        NO_COPY_NO_MOVE(RHIFence)
        RHIFence();
        ~RHIFence() noexcept override;

        void* GetHandle() override { return nullptr; }
        bool Lock() override { return true; }

        void Unlock() override
        {
        }
    };
}

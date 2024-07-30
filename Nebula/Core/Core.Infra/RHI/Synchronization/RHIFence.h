#pragma once
#include "RHI/Synchronization/SynchObject.h"

namespace NebulaEngine::RHI
{
    class RHIFence : public virtual SynchObject
    {
    public:
        NO_COPY_NO_MOVE(RHIFence)
        RHIFence(): SynchObject() {};
        ~RHIFence() noexcept override = default;

        void* GetHandle() override { return nullptr; }
        void Lock() override  { }
        void Unlock() override { }
    };
}

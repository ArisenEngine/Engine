#pragma once
#include "../../../Core.Platform/Synchronization/SynchObject.h"
#include "../../Common/CommandHeaders.h"

namespace NebulaEngine::RHI
{
    class RHIFence : public Platforms::SynchObject
    {
    public:
        NO_COPY_NO_MOVE(RHIFence)
        RHIFence(): SynchObject() {};
        ~RHIFence() noexcept override = default;

        void* GetHandle() override = 0;
        void Lock() override = 0;
        void Unlock() override = 0;
    };
}

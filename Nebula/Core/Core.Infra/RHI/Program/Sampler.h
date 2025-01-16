#pragma once
#include "../RHICommon.h"
#include "../Devices/Device.h"

namespace NebulaEngine::RHI
{
    class Sampler
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(Sampler)
        VIRTUAL_DECONSTRUCTOR(Sampler)
        Sampler(Device* device);
        virtual void* GetHandle() const = 0;
    };
}

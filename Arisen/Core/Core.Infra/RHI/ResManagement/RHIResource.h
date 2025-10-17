#pragma once
#include "CoreInfraCommon.h"
#include "Common/CommandHeaders.h"

namespace ArisenEngine::RHI
{
    class RHIDevice;
    COREINFRA_DLL class RHIResource
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIResource)
        RHIResource(RHIDevice * device);
        virtual ~RHIResource();
    };
}

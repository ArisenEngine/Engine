#pragma once
#include "../../Common/CommandHeaders.h"

namespace NebulaEngine::RHI
{
    class GPUPipeline
    {
        
    public:
        NO_COPY_NO_MOVE(GPUPipeline)
        GPUPipeline() = default;
        VIRTUAL_DECONSTRUCTOR(GPUPipeline)
        virtual void* GetHandle() const = 0;
    protected:
        
    };
}

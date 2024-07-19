#pragma once
#include "../../Common/CommandHeaders.h"
namespace NebulaEngine::RHI
{
    class GPURenderPass
    {
    public:
        NO_COPY_NO_MOVE(GPURenderPass)
        GPURenderPass() = default;
        VIRTUAL_DECONSTRUCTOR(GPURenderPass)
        virtual void* GetHandle() = 0;
        
    };
    
}

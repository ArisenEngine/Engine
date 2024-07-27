#pragma once
#include "../../Common/CommandHeaders.h"

namespace NebulaEngine::RHI
{
    class RHICommandBufferPool
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHICommandBufferPool)
        RHICommandBufferPool(const Device* device) : m_Device(device) { };
        virtual ~RHICommandBufferPool()
        {
            m_Device = nullptr;
        }
        virtual void* GetHandle() = 0;

    protected:
        const Device* m_Device;
    };
}

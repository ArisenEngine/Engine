#pragma once
#include "../../Common/CommandHeaders.h"

namespace NebulaEngine::RHI
{
    class DeviceMemory
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(DeviceMemory)
        virtual ~DeviceMemory() noexcept
        {
            m_TotalBytes = 0;
        }
        virtual void* GetHandle() const = 0;
        const u32 GetBytes() const { return m_TotalBytes; }
    protected:

        u32 m_TotalBytes { 0 };
        
    };
}

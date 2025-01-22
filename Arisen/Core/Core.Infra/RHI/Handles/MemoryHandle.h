#pragma once
#include "../../Common/CommandHeaders.h"
#include "Logger/Logger.h"
#include "RHI/Memory/DeviceMemory.h"
#include "RHI/Memory/MemoryView.h"

namespace ArisenEngine::RHI
{
    class MemoryHandle
    {
    public:
        NO_COPY_NO_MOVE(MemoryHandle)
        MemoryHandle() = default;
        virtual ~MemoryHandle() noexcept
        {
            LOG_DEBUG(" ~MemoryHandle ");
            
            if (m_MemoryView != nullptr)
            {
                delete m_MemoryView;
                m_MemoryView = nullptr;
            }

            if (m_DeviceMemory != nullptr)
            {
                delete m_DeviceMemory;
                m_DeviceMemory = nullptr;
            }
        }
        
        virtual void* GetHandle() const = 0;
        
        MemoryView* GetMemoryView() const { return m_MemoryView; }
        DeviceMemory* GetDeviceMemory() const { return  m_DeviceMemory; }
        
    protected:
        MemoryView* m_MemoryView {nullptr};
        DeviceMemory* m_DeviceMemory {nullptr};
    };
}

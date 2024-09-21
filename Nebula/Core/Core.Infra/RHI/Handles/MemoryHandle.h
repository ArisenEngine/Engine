#pragma once
#include "../../Common/CommandHeaders.h"
#include "Logger/Logger.h"
#include "RHI/Memory/MemoryView.h"

namespace NebulaEngine::RHI
{
    class DeviceMemory;

    class MemoryHandle
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(MemoryHandle)
        MemoryHandle(bool needRecycleMemory) : m_NeedRecycleMemory(needRecycleMemory) { }
        virtual ~MemoryHandle() noexcept
        {
            LOG_DEBUG(" ~MemoryHandle ");
            
            if (m_MemoryView != nullptr)
            {
                delete m_MemoryView;
                m_MemoryView = nullptr;
            }

            if (NeedRecycleMemory())
            {
                // TODO: recycle raw memory to memory pool
        
            }
            else
            {
        
            }
            
            if (m_DeviceMemory != nullptr)
            {
                // delete m_DeviceMemory;
                m_DeviceMemory = nullptr;
            }
        }
        
        virtual void* GetHandle() const = 0;

        bool NeedRecycleMemory() const
        {
            return m_NeedRecycleMemory;
        }
        
        MemoryView* GetMemoryView() const { return m_MemoryView; }
        const DeviceMemory& GetDeviceMemory() const { return  *m_DeviceMemory; }
        
    protected:
        MemoryView* m_MemoryView {nullptr};
        DeviceMemory* m_DeviceMemory {nullptr};
        bool m_NeedRecycleMemory { true };
    };
}

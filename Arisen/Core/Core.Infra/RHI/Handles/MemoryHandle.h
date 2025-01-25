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
            LOG_DEBUG(" ~MemoryHandle: " + m_Name);
            
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

        virtual bool AllocDeviceMemory(UInt32 memoryPropertiesBits) = 0;
    
    public:
        
        MemoryView* GetMemoryView() const { return m_MemoryView; }
        DeviceMemory* GetDeviceMemory() const { return  m_DeviceMemory; }

        const std::string GetName() const
        {
            return m_Name;
        }

        void SetName(const std::string&& name)
        {
            m_Name = name;
        }
        
        void FreeDeviceMemory() const
        {
            ASSERT(m_DeviceMemory != nullptr);
            m_DeviceMemory->FreeDeviceMemory();
        }
        
    protected:
        std::string m_Name { "Anonymous" };
        MemoryView* m_MemoryView {nullptr};
        DeviceMemory* m_DeviceMemory {nullptr};
    };
}

#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Memory/RawMemory.h"
#include "RHI/Memory/MemoryView.h"

namespace NebulaEngine::RHI
{
    class ImageHandle
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(ImageHandle)
        ImageHandle(bool needRecycleMemory): m_NeedRecycleMemory(needRecycleMemory) { }
        virtual ~ImageHandle() noexcept
        {
            m_MemoryView = nullptr;
            m_DeviceMemory = nullptr;
        }
        virtual void* GetHandle() const = 0;
        const MemoryView& GetMemoryView() const { return *m_MemoryView; }
        const RawMemory& GetDeviceMemory() const { return  *m_DeviceMemory; }
    protected:
        bool m_NeedRecycleMemory { true };
        MemoryView* m_MemoryView;
        RawMemory* m_DeviceMemory;
    };
}

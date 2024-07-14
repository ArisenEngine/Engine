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
        VIRTUAL_DECONSTRUCTOR(ImageHandle)
        virtual void* GetHandle() const = 0;
    protected:
        bool m_NeedRecycleMemory { true };
        MemoryView* m_MemoryView;
        RawMemory* m_DeviceMemory;
    };
}

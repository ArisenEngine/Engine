#pragma once
#include "MemoryView.h"

namespace NebulaEngine::RHI
{
    class BufferView : public MemoryView
    {
    public:
        NO_COPY_NO_MOVE(BufferView)
        BufferView(): MemoryView(MemoryViewType::BUFFER_MEMORY_VIEW_TYPE){}
        ~BufferView() noexcept override = default;
        void* GetView() override = 0;
        void* GetViewPointer() override = 0;

    
    private:
        
    };
}

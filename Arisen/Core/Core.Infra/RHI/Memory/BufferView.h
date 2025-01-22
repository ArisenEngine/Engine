#pragma once
#include "MemoryView.h"

namespace ArisenEngine::RHI
{
    class BufferView : public MemoryView
    {
    public:
        NO_COPY_NO_MOVE(BufferView)
        BufferView(): MemoryView(MemoryViewType::BUFFER_MEMORY_VIEW_TYPE){}
        ~BufferView() noexcept override = default;
        void* GetView() const override = 0;
        void* GetViewPointer() override = 0;

    
    private:
        
    };
}

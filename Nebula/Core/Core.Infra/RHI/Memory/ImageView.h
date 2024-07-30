#pragma once
#include "MemoryView.h"

namespace NebulaEngine::RHI
{
    class ImageView : public MemoryView
    {
    public:
        NO_COPY_NO_MOVE(ImageView)
        ImageView():MemoryView(MemoryViewType::IMAGE_MEMORY_VIEW_TYPE) {}
        ~ImageView() noexcept override = default;
        void* GetView() override = 0;

        virtual const u32 GetWidth() const = 0;
        virtual const u32 GetHeight() const = 0;
        virtual const u32 GetLayerCount() const = 0;
        virtual const Format GetFormat() const = 0;
    private:
        
    };
}

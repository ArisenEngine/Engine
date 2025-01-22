#pragma once
#include "MemoryView.h"

namespace ArisenEngine::RHI
{
    class ImageView : public MemoryView
    {
    public:
        NO_COPY_NO_MOVE(ImageView)
        ImageView(): MemoryView(MemoryViewType::IMAGE_MEMORY_VIEW_TYPE) {};
        ~ImageView() noexcept override = default;
        void* GetView() const override = 0;
        void* GetViewPointer() override = 0;

        virtual const UInt32 GetWidth() const = 0;
        virtual const UInt32 GetHeight() const = 0;
        virtual const UInt32 GetLayerCount() const = 0;
        virtual const Format GetFormat() const = 0;
    private:
        
    };
}

#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/Memory/ImageViewType.h"
#include "RHI/Enums/Memory/MemoryViewType.h"
#include "RHI/Handles/ComponentMapping.h"
#include "RHI/Enums/Image/EFormat.h"

namespace ArisenEngine::RHI
{
    typedef struct ImageViewDesc
    {
        UInt32 createFlags;
        ImageViewType type;
        EFormat format;
        ComponentMapping componentMapping;
        UInt32 width;
        UInt32 height;
        // subresource
        UInt32 aspectMask;
        UInt32 baseMipLevel;
        UInt32 levelCount;
        UInt32 baseArrayLayer;
        UInt32 layerCount;
        std::optional<const void*> customData;
    } ImageViewDesc;

    
    class MemoryView
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(MemoryView)
        MemoryView(MemoryViewType type) : m_ViewType(type) { }
        VIRTUAL_DECONSTRUCTOR(MemoryView)
        virtual void* GetView() const = 0;
        virtual void* GetViewPointer() = 0;
        const MemoryViewType GetViewType() const { return m_ViewType; }
    protected:
        MemoryViewType m_ViewType;
        std::optional<ImageViewDesc> m_ImageViewDesc;
    };
}

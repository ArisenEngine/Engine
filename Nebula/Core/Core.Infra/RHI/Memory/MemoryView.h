#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/ImageViewType.h"
#include "RHI/Handles/ComponentMapping.h"

namespace NebulaEngine::RHI
{
    typedef struct ImageViewDesc
    {
        u32 createFlags;
        ImageViewType type;
        Format format;
        ComponentMapping componentMapping;

        // subresource
        u32 aspectMask;
        u32 baseMipLevel;
        u32 levelCount;
        u32 baseArrayLayer;
        u32 layerCount;
        std::optional<const void*> customData;
    } ImageViewDesc;

    
    class MemoryView
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(MemoryView)
        MemoryView(MemoryViewType type) : m_ViewType(type) { }
        VIRTUAL_DECONSTRUCTOR(MemoryView)
    protected:
        MemoryViewType m_ViewType;
        std::optional<ImageViewDesc> m_ImageViewDesc;
    };
}

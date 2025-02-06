#pragma once
#include "../../Common/CommandHeaders.h"
#include "MemoryHandle.h"
#include "RHI/Enums/Image/EImageTiling.h"
#include "RHI/Enums/Image/EImageType.h"
#include "RHI/Enums/Image/EImageUsageFlagBits.h"
#include "RHI/Enums/Image/EImageLayout.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Memory/ESharingMode.h"

namespace ArisenEngine::RHI
{
    typedef struct ImageDescriptor
    {
        EImageType imageType;
        UInt32 width;
        UInt32 height;
        UInt32 depth;
        UInt32 mipLevels;
        UInt32 arrayLayers;
        EFormat format;
        EImageTiling tiling;
        EImageLayout imageLayout;
        UInt32 usage;
        ESampleCountFlagBits sampleCount;
        ESharingMode sharingMode;
        UInt32 queueFamilyIndexCount;
        const void* pQueueFamilyIndices;
    } ImageDescriptor;
    
    class ImageHandle : public MemoryHandle
    {
    public:
        NO_COPY_NO_MOVE(ImageHandle)
        ImageHandle() = default;
        virtual void AllocHandle(ImageDescriptor&& desc) = 0;
        virtual void FreeHandle() = 0;
        // TODO: support multiple views
        virtual UInt32 AddImageView(ImageViewDesc&& desc) = 0;
        bool AllocDeviceMemory(UInt32 memoryPropertiesBits) override = 0;
        ~ImageHandle() noexcept override = default;
    };
}

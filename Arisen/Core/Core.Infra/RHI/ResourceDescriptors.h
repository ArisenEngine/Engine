#pragma once
#include "../../Common/CommandHeaders.h"
#include "Enums/Image/EImageTiling.h"
#include "RHI/Enums/Image/EImageType.h"
#include "RHI/Enums/Image/EImageUsageFlagBits.h"
#include "RHI/Enums/Image/EImageLayout.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Memory/ESharingMode.h"
#include "RHI/Enums/Image/EImageViewType.h"
#include "RHI/Enums/Image/EFormat.h"
#include <optional>

namespace ArisenEngine::RHI {

struct BufferDescriptor {
    UInt32 createFlagBits;
    UInt64 size;
    UInt32 usage;
    ESharingMode sharingMode;
    UInt32 queueFamilyIndexCount;
    const void* pQueueFamilyIndices;
};

struct ImageDescriptor {
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
};

struct ImageViewDesc {
    EImageViewType viewType;
    EFormat format;
    UInt32 baseMipLevel;
    UInt32 levelCount;
    UInt32 baseArrayLayer;
    UInt32 layerCount;
    std::optional<UInt32> width;
    std::optional<UInt32> height;
};

} // namespace ArisenEngine::RHI

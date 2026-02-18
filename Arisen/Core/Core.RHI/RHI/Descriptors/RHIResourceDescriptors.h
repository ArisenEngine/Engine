#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/Image/EImageTiling.h"
#include "RHI/Enums/Image/EImageType.h"
#include "RHI/Enums/Image/EImageUsageFlagBits.h"
#include "RHI/Enums/Image/EImageLayout.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Memory/ESharingMode.h"
#include "RHI/Enums/Image/EImageViewType.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Memory/ERHIMemoryUsage.h"


namespace ArisenEngine::RHI {

// TODO(CppSharp-P1): RHIBufferDescriptor::pQueueFamilyIndices 是 const void*，CppSharp 无法正确映射。
// 考虑替换为 Containers::Vector<UInt32> 或固定大小数组 UInt32 queueFamilyIndices[4]。
struct RHIBufferDescriptor {
    UInt32 createFlagBits;
    UInt64 size;
    UInt32 usage;
    ESharingMode sharingMode;
    UInt32 queueFamilyIndexCount;
    const void* pQueueFamilyIndices;  // TODO: 替换为类型安全的数组
    ERHIMemoryUsage memoryUsage;
};

struct RHIImageDescriptor {
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
    ERHIMemoryUsage memoryUsage;
};

// CppSharp-P0 RESOLVED: std::optional<UInt32> replaced with UInt32, 0 = auto-detect from parent image.
struct RHIImageViewDesc {
    EImageViewType viewType;
    EFormat format;
    UInt32 aspectMask;
    UInt32 baseMipLevel;
    UInt32 levelCount;
    UInt32 baseArrayLayer;
    UInt32 layerCount;
    UInt32 width = 0;   // 0 = auto-detect from parent image
    UInt32 height = 0;  // 0 = auto-detect from parent image
};

} // namespace ArisenEngine::RHI


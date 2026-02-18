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
#include <optional>

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

// TODO(CppSharp-P0): std::optional<UInt32> 是非 POD 类型，CppSharp 无法正确映射。
// 替换为 UInt32 width = 0; UInt32 height = 0; 约定 0 表示 "auto-detect from parent image"。
// 这也使此结构体可平面序列化。
struct RHIImageViewDesc {
    EImageViewType viewType;
    EFormat format;
    UInt32 aspectMask;
    UInt32 baseMipLevel;
    UInt32 levelCount;
    UInt32 baseArrayLayer;
    UInt32 layerCount;
    std::optional<UInt32> width;   // TODO: 替换为 UInt32 width = 0;
    std::optional<UInt32> height;  // TODO: 替换为 UInt32 height = 0;
};

} // namespace ArisenEngine::RHI


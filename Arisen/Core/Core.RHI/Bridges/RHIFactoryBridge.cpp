// RHIFactoryBridge.cpp — extern "C" bridge for RHIFactory pure virtual methods
#include "RHI/Core/RHIFactory.h"
#include "RHI/Definitions/CoreRHICommon.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Descriptors/RHIResourceDescriptors.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_BEGIN_BRIDGE("RHIFactory", "Core.RHI.dll", "Arisen.Native.RHI")

// Helper: construct a typed handle from (index, generation) pair
template<typename T>
static inline RHIHandle<T> MakeHandle(uint32_t index, uint32_t generation)
{
    RHIHandle<T> h;
    h.index = index;
    h.generation = generation;
    return h;
}

extern "C" {

// ============================================================================
// Buffer
// ============================================================================

RHI_DLL void RHIFactory_CreateBuffer(RHIFactory* f,
    uint32_t createFlagBits, uint64_t size, uint32_t usage,
    int sharingMode, uint32_t queueFamilyIndexCount,
    int memoryUsage, const char* name,
    uint32_t* outIndex, uint32_t* outGeneration)
{
    RHIBufferDescriptor desc{};
    desc.createFlagBits = createFlagBits;
    desc.size = size;
    desc.usage = usage;
    desc.sharingMode = static_cast<ESharingMode>(sharingMode);
    desc.queueFamilyIndexCount = queueFamilyIndexCount;
    desc.pQueueFamilyIndices = nullptr;
    desc.memoryUsage = static_cast<ERHIMemoryUsage>(memoryUsage);
    auto handle = f->CreateBuffer(std::move(desc), name ? name : "Anonymous");
    *outIndex = handle.index;
    *outGeneration = handle.generation;
}

RHI_DLL void RHIFactory_ReleaseBuffer(RHIFactory* f, uint32_t index, uint32_t generation)
{
    f->ReleaseBuffer(MakeHandle<RHIBufferTag>(index, generation));
}

RHI_DLL void RHIFactory_BufferMemoryCopy(RHIFactory* f, uint32_t index, uint32_t generation,
    const void* src, uint64_t size, uint64_t offset)
{
    f->BufferMemoryCopy(MakeHandle<RHIBufferTag>(index, generation), src, size, offset);
}

RHI_DLL void* RHIFactory_MapBuffer(RHIFactory* f, uint32_t index, uint32_t generation)
{
    return f->MapBuffer(MakeHandle<RHIBufferTag>(index, generation));
}

RHI_DLL void RHIFactory_UnmapBuffer(RHIFactory* f, uint32_t index, uint32_t generation)
{
    f->UnmapBuffer(MakeHandle<RHIBufferTag>(index, generation));
}

RHI_DLL uint64_t RHIFactory_GetBufferSize(RHIFactory* f, uint32_t index, uint32_t generation)
{
    return f->GetBufferSize(MakeHandle<RHIBufferTag>(index, generation));
}

RHI_DLL uint64_t RHIFactory_GetBufferDeviceAddress(RHIFactory* f, uint32_t index, uint32_t generation)
{
    return f->GetBufferDeviceAddress(MakeHandle<RHIBufferTag>(index, generation));
}

// ============================================================================
// Image
// ============================================================================

RHI_DLL void RHIFactory_CreateImage(RHIFactory* f,
    int imageType, uint32_t width, uint32_t height, uint32_t depth,
    uint32_t mipLevels, uint32_t arrayLayers, int format, int tiling,
    int imageLayout, uint32_t usage, int sampleCount, int sharingMode,
    int memoryUsage, const char* name,
    uint32_t* outIndex, uint32_t* outGeneration)
{
    RHIImageDescriptor desc{};
    desc.imageType = static_cast<EImageType>(imageType);
    desc.width = width;
    desc.height = height;
    desc.depth = depth;
    desc.mipLevels = mipLevels;
    desc.arrayLayers = arrayLayers;
    desc.format = static_cast<EFormat>(format);
    desc.tiling = static_cast<EImageTiling>(tiling);
    desc.imageLayout = static_cast<EImageLayout>(imageLayout);
    desc.usage = usage;
    desc.sampleCount = static_cast<ESampleCountFlagBits>(sampleCount);
    desc.sharingMode = static_cast<ESharingMode>(sharingMode);
    desc.queueFamilyIndexCount = 0;
    desc.pQueueFamilyIndices = nullptr;
    desc.memoryUsage = static_cast<ERHIMemoryUsage>(memoryUsage);
    auto handle = f->CreateImage(std::move(desc), name ? name : "Anonymous");
    *outIndex = handle.index;
    *outGeneration = handle.generation;
}

RHI_DLL void RHIFactory_ReleaseImage(RHIFactory* f, uint32_t index, uint32_t generation)
{
    f->ReleaseImage(MakeHandle<RHIImageTag>(index, generation));
}

// ============================================================================
// ImageView
// ============================================================================

RHI_DLL void RHIFactory_CreateImageView(RHIFactory* f,
    uint32_t imageIndex, uint32_t imageGeneration,
    int viewType, int format, uint32_t aspectMask,
    uint32_t baseMipLevel, uint32_t levelCount,
    uint32_t baseArrayLayer, uint32_t layerCount,
    uint32_t* outIndex, uint32_t* outGeneration)
{
    RHIImageViewDesc desc{};
    desc.viewType = static_cast<EImageViewType>(viewType);
    desc.format = static_cast<EFormat>(format);
    desc.aspectMask = aspectMask;
    desc.baseMipLevel = baseMipLevel;
    desc.levelCount = levelCount;
    desc.baseArrayLayer = baseArrayLayer;
    desc.layerCount = layerCount;
    auto handle = f->CreateImageView(MakeHandle<RHIImageTag>(imageIndex, imageGeneration), std::move(desc));
    *outIndex = handle.index;
    *outGeneration = handle.generation;
}

RHI_DLL void RHIFactory_ReleaseImageView(RHIFactory* f, uint32_t index, uint32_t generation)
{
    f->ReleaseImageView(MakeHandle<RHIImageViewTag>(index, generation));
}

// ============================================================================
// Sampler
// ============================================================================

RHI_DLL void RHIFactory_ReleaseSampler(RHIFactory* f, uint32_t index, uint32_t generation)
{
    f->ReleaseSampler(MakeHandle<RHISamplerTag>(index, generation));
}

// ============================================================================
// Sync Primitives
// ============================================================================

RHI_DLL void RHIFactory_CreateSemaphore(RHIFactory* f, uint32_t* outIndex, uint32_t* outGeneration)
{
    auto handle = f->CreateSemaphore();
    *outIndex = handle.index;
    *outGeneration = handle.generation;
}

RHI_DLL void RHIFactory_ReleaseSemaphore(RHIFactory* f, uint32_t index, uint32_t generation)
{
    f->ReleaseSemaphore(MakeHandle<RHISemaphoreTag>(index, generation));
}

RHI_DLL void RHIFactory_CreateFence(RHIFactory* f, int signaled, uint32_t* outIndex, uint32_t* outGeneration)
{
    auto handle = f->CreateFence(signaled != 0);
    *outIndex = handle.index;
    *outGeneration = handle.generation;
}

RHI_DLL void RHIFactory_ReleaseFence(RHIFactory* f, uint32_t index, uint32_t generation)
{
    f->ReleaseFence(MakeHandle<RHIFenceTag>(index, generation));
}

// ============================================================================
// RenderPass / FrameBuffer
// ============================================================================

RHI_DLL void RHIFactory_CreateRenderPass(RHIFactory* f, uint32_t* outIndex, uint32_t* outGeneration)
{
    auto handle = f->CreateRenderPass();
    *outIndex = handle.index;
    *outGeneration = handle.generation;
}

RHI_DLL void RHIFactory_ReleaseRenderPass(RHIFactory* f, uint32_t index, uint32_t generation)
{
    f->ReleaseRenderPass(MakeHandle<RHIRenderPassTag>(index, generation));
}

RHI_DLL void RHIFactory_CreateFrameBuffer(RHIFactory* f, uint32_t* outIndex, uint32_t* outGeneration)
{
    auto handle = f->CreateFrameBuffer();
    *outIndex = handle.index;
    *outGeneration = handle.generation;
}

RHI_DLL void RHIFactory_ReleaseFrameBuffer(RHIFactory* f, uint32_t index, uint32_t generation)
{
    f->ReleaseFrameBuffer(MakeHandle<RHIFrameBufferTag>(index, generation));
}

// ============================================================================
// CommandBufferPool
// ============================================================================

RHI_DLL void RHIFactory_CreateCommandBufferPool(RHIFactory* f, int queueType,
    uint32_t* outIndex, uint32_t* outGeneration)
{
    auto handle = f->CreateCommandBufferPool(static_cast<RHIQueueType>(queueType));
    *outIndex = handle.index;
    *outGeneration = handle.generation;
}

RHI_DLL void RHIFactory_ReleaseCommandBufferPool(RHIFactory* f, uint32_t index, uint32_t generation)
{
    f->ReleaseCommandBufferPool(MakeHandle<RHICommandBufferPoolTag>(index, generation));
}

// ============================================================================
// ImageView Queries
// ============================================================================

RHI_DLL int RHIFactory_GetImageViewFormat(RHIFactory* f, uint32_t index, uint32_t generation)
{
    return static_cast<int>(f->GetImageViewFormat(MakeHandle<RHIImageViewTag>(index, generation)));
}

RHI_DLL uint32_t RHIFactory_GetImageViewWidth(RHIFactory* f, uint32_t index, uint32_t generation)
{
    return f->GetImageViewWidth(MakeHandle<RHIImageViewTag>(index, generation));
}

RHI_DLL uint32_t RHIFactory_GetImageViewHeight(RHIFactory* f, uint32_t index, uint32_t generation)
{
    return f->GetImageViewHeight(MakeHandle<RHIImageViewTag>(index, generation));
}

} // extern "C"

ARISEN_BIND_END_BRIDGE()

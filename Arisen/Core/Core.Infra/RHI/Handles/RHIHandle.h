#pragma once

#include "../../Common/CommandHeaders.h"

namespace ArisenEngine {
namespace RHI {
/**
 * @brief Base structure for all RHI handles.
 * 32-bit index + 32-bit generation = 64-bit POD, blittable for C# interop.
 */
template <typename T> struct RHIHandle {
  UInt32 index{0xFFFFFFFFu};
  UInt32 generation{0};

  bool IsValid() const { return index != 0xFFFFFFFFu; }

  bool operator==(const RHIHandle &other) const {
    return index == other.index && generation == other.generation;
  }

  bool operator!=(const RHIHandle &other) const { return !(*this == other); }

  static RHIHandle<T> Invalid() { return {0xFFFFFFFFu, 0}; }
};

// Specific tags to make handles type-safe in C++
struct RHIBufferTag {};
struct RHIImageTag {};
struct RHISamplerTag {};
struct RHIImageViewTag {};
struct RHIShaderTag {};
struct RHIPipelineTag {};
struct RHICommandBufferTag {};
struct RHIDescriptorSetTag {};
struct RHIFenceTag {};
struct RHISemaphoreTag {};
struct RHIRenderPassTag {};
struct RHIFrameBufferTag {};

using RHIBufferHandle = RHIHandle<RHIBufferTag>;
using RHIImageHandle = RHIHandle<RHIImageTag>;
using RHIImageViewHandle = RHIHandle<RHIImageViewTag>;
using RHISamplerHandle = RHIHandle<RHISamplerTag>;
using RHIShaderHandle = RHIHandle<RHIShaderTag>;
using RHIPipelineHandle = RHIHandle<RHIPipelineTag>;
using RHICommandBufferHandle = RHIHandle<RHICommandBufferTag>;
using RHIDescriptorSetHandle = RHIHandle<RHIDescriptorSetTag>;
using RHIFenceHandle = RHIHandle<RHIFenceTag>;
using RHISemaphoreHandle = RHIHandle<RHISemaphoreTag>;
using RHIRenderPassHandle = RHIHandle<RHIRenderPassTag>;
using RHIFrameBufferHandle = RHIHandle<RHIFrameBufferTag>;
struct RHIResourceTag {};
using RHIResourceHandle = RHIHandle<RHIResourceTag>;
} // namespace RHI
} // namespace ArisenEngine

#pragma once
#include "Base/FoundationMinimal.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(ERHIMemoryUsage)

    enum class ERHIMemoryUsage : UInt8
    {
        GpuOnly, // Device local, fastest for GPU access. (VMA_MEMORY_USAGE_AUTO_PREFER_DEVICE)
        Upload, // CPU write, GPU read (staging). (VMA_MEMORY_USAGE_CPU_TO_GPU)
        Readback, // GPU write, CPU read. (VMA_MEMORY_USAGE_GPU_TO_CPU)
        Transient // Short-lived, often mapped. (VMA_MEMORY_USAGE_CPU_TO_GPU / AUTO_PREFER_HOST)
    };
} // namespace ArisenEngine::RHI

#pragma once

#include "Base/FoundationMinimal.h"
#include <atomic>

namespace ArisenEngine::RHI
{
    struct RHIResourceStats
    {
        // Resource Counts
        std::atomic<UInt32> bufferCount{0};
        std::atomic<UInt32> imageCount{0};
        std::atomic<UInt32> imageViewCount{0};
        std::atomic<UInt32> samplerCount{0};
        std::atomic<UInt32> renderPassCount{0};
        std::atomic<UInt32> frameBufferCount{0};
        std::atomic<UInt32> pipelineCount{0};
        std::atomic<UInt32> shaderProgramCount{0};
        std::atomic<UInt32> descriptorSetCount{0};
        std::atomic<UInt32> commandBufferCount{0};
        std::atomic<UInt32> synchronizationCount{0}; // Fences + Semaphores

        // Memory Usage (Bytes)
        std::atomic<UInt64> totalVideoMemoryAllocated{0};
        std::atomic<UInt64> totalVideoMemoryUsed{0}; // Detailed tracking if possible
    };

    class RHIInspector
    {
    public:
        // Static accessor or just a helper class. 
        // For now, it defines the stats structure.
    };
}

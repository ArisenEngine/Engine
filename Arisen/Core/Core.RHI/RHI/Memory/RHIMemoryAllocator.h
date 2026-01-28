#pragma once
#include "Base/FoundationMinimal.h"

namespace ArisenEngine::RHI
{
    class RHIMemoryAllocator
    {
    public:
        RHIMemoryAllocator() = default;
        virtual ~RHIMemoryAllocator() noexcept = default;
        NO_COPY_NO_MOVE(RHIMemoryAllocator)

        virtual void* GetHandle() const = 0;
    };
}


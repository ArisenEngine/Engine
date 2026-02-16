#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHI_DLL RHIMemoryAllocator
    {
    public:
        RHIMemoryAllocator();
        virtual ~RHIMemoryAllocator() noexcept;
        NO_COPY_NO_MOVE(RHIMemoryAllocator)

        virtual void* GetHandle() const = 0;
    };
}


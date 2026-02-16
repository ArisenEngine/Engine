#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    enum class EDescriptorHeapType
    {
        CBV_SRV_UAV,
        SAMPLER,
        RTV,
        DSV,
        NUM_TYPES
    };

    class RHI_DLL RHIDescriptorHeap
    {
    public:
        virtual ~RHIDescriptorHeap() = default;

        virtual EDescriptorHeapType GetType() const = 0;
        virtual UInt32 GetDescriptorSize() const = 0;
        virtual UInt32 GetCapacity() const = 0;
        virtual void* GetNativeHandle() const = 0;
        
        // Allocate a range of descriptors. Returns the index of the first descriptor.
        virtual UInt32 Allocate(UInt32 count) = 0;
        // Free a range of descriptors.
        virtual void Free(UInt32 index, UInt32 count) = 0;
    };
}

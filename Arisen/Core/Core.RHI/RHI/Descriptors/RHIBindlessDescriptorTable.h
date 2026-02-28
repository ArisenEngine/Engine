#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Descriptors/RHIDescriptorHeap.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHI_DLL RHIBindlessDescriptorTable
    {
    public:
        virtual ~RHIBindlessDescriptorTable() = default;

        virtual void SetDescriptorHeap(RHIDescriptorHeap* heap) = 0;

        // Bind a resource to a specific index in the table
        virtual void BindResource(UInt32 index, RHIResourceHandle resource) = 0;

        // Get the GPU handle/index for binding in shaders
        virtual UInt32 GetBindlessIndex(UInt32 index) const = 0;

        virtual RHIDescriptorHeap* GetHeap() const = 0;
    };
}

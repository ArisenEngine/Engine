#pragma once
#include "RHI/Descriptors/RHIBindlessDescriptorTable.h"
#include "Descriptors/RHIVkDescriptorHeap.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;

    class RHIVkBindlessDescriptorTable : public RHIBindlessDescriptorTable
    {
    public:
        RHIVkBindlessDescriptorTable(RHIVkDevice* device, RHIVkDescriptorHeap* heap);
        virtual ~RHIVkBindlessDescriptorTable() override;

        void SetDescriptorHeap(RHIDescriptorHeap* heap) override;
        void BindResource(UInt32 index, RHIResourceHandle resource) override;
        UInt32 GetBindlessIndex(UInt32 index) const override;
        RHIDescriptorHeap* GetHeap() const override { return m_Heap; }

    private:
        RHIVkDevice* m_Device;
        RHIVkDescriptorHeap* m_Heap;
    };
}

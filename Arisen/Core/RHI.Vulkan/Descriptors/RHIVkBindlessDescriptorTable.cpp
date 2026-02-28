#include "Descriptors/RHIVkBindlessDescriptorTable.h"
#include "../Core/RHIVkDevice.h"

namespace ArisenEngine::RHI
{
    RHIVkBindlessDescriptorTable::RHIVkBindlessDescriptorTable(RHIVkDevice* device, RHIVkDescriptorHeap* heap)
        : m_Device(device), m_Heap(heap)
    {
    }

    RHIVkBindlessDescriptorTable::~RHIVkBindlessDescriptorTable()
    {
    }

    void RHIVkBindlessDescriptorTable::SetDescriptorHeap(RHIDescriptorHeap* heap)
    {
        m_Heap = static_cast<RHIVkDescriptorHeap*>(heap);
    }

    void RHIVkBindlessDescriptorTable::BindResource(UInt32 index, RHIResourceHandle resource)
    {
        // TODO: Update the descriptor set at 'index' with the resource info.
        // This requires casting resource handle to VkImageView / VkBufferView and calling vkUpdateDescriptorSets

        // This is a placeholder logic.
        // In a real implementation we need to know the type of the resource and the type of the heap to create correct WriteDescriptorSet.
    }

    UInt32 RHIVkBindlessDescriptorTable::GetBindlessIndex(UInt32 index) const
    {
        // For now, index IS the offset in the heap.
        return index;
    }
}

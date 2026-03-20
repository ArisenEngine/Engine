#pragma once
#include "RHI/Descriptors/RHIDescriptorHeap.h"
#include <vulkan/vulkan.h>
#include <vector>
#include <mutex>
#include <queue>

namespace ArisenEngine::RHI
{
    class RHIVkDevice;

    class RHIVkDescriptorHeap : public RHIDescriptorHeap
    {
    public:
        RHIVkDescriptorHeap(RHIVkDevice* device, EDescriptorHeapType type, UInt32 descriptorLimit);
        virtual ~RHIVkDescriptorHeap() override;

        EDescriptorHeapType GetType() const override { return m_Type; }
        UInt32 GetDescriptorSize() const override { return 0; } // Not used in Vulkan typically
        UInt32 GetCapacity() const override { return m_Limit; }
        void* GetNativeHandle() const override { return (void*)m_DescriptorSet; }

        UInt32 Allocate(UInt32 count) override;
        void Free(UInt32 index, UInt32 count) override;

        VkDescriptorSet GetVkDescriptorSet() const { return m_DescriptorSet; }
        VkDescriptorSetLayout GetVkDescriptorSetLayout() const { return m_Layout; }

    private:
        RHIVkDevice* m_Device;
        EDescriptorHeapType m_Type;
        UInt32 m_Limit;

        VkDescriptorPool m_Pool = VK_NULL_HANDLE;
        VkDescriptorSetLayout m_Layout = VK_NULL_HANDLE;
        VkDescriptorSet m_DescriptorSet = VK_NULL_HANDLE;

        std::mutex m_Mutex;
        std::queue<std::pair<UInt32, UInt32>> m_FreeIntervals; // Simple free list (start, count)
        UInt32 m_CurrentOffset = 0;
    };
}

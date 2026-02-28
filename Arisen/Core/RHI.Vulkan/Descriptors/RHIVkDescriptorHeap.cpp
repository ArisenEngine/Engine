#include "Descriptors/RHIVkDescriptorHeap.h"
#include "../Core/RHIVkDevice.h"
#include <iostream>

namespace ArisenEngine::RHI
{
    RHIVkDescriptorHeap::RHIVkDescriptorHeap(RHIVkDevice* device, EDescriptorHeapType type, UInt32 descriptorLimit)
        : m_Device(device), m_Type(type), m_Limit(descriptorLimit)
    {
        // 1. Create Descriptor Pool
        VkDescriptorPoolSize poolSizes[] = {
            {VK_DESCRIPTOR_TYPE_SAMPLER, descriptorLimit},
            {VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER, descriptorLimit},
            {VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE, descriptorLimit},
            {VK_DESCRIPTOR_TYPE_STORAGE_IMAGE, descriptorLimit},
            {VK_DESCRIPTOR_TYPE_UNIFORM_TEXEL_BUFFER, descriptorLimit},
            {VK_DESCRIPTOR_TYPE_STORAGE_TEXEL_BUFFER, descriptorLimit},
            {VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorLimit},
            {VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, descriptorLimit},
            {VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC, descriptorLimit},
            {VK_DESCRIPTOR_TYPE_STORAGE_BUFFER_DYNAMIC, descriptorLimit},
            {VK_DESCRIPTOR_TYPE_INPUT_ATTACHMENT, descriptorLimit}
        };

        VkDescriptorPoolCreateInfo poolInfo{};
        poolInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
        poolInfo.flags = VK_DESCRIPTOR_POOL_CREATE_UPDATE_AFTER_BIND_BIT;
        poolInfo.maxSets = 1; // We only need 1 big set for bindless usually
        poolInfo.poolSizeCount = std::size(poolSizes);
        poolInfo.pPoolSizes = poolSizes;

        if (vkCreateDescriptorPool(static_cast<VkDevice>(m_Device->GetHandle()), &poolInfo, nullptr, &m_Pool) !=
            VK_SUCCESS)
        {
            std::cerr << "Failed to create bindless descriptor pool!" << std::endl;
        }

        // 2. Create Descriptor Set Layout
        // For bindless, we usually have a single binding that is an array of size 'descriptorLimit'
        // with VARIABLE_DESCRIPTOR_COUNT and PARTIALLY_BOUND flags.

        VkDescriptorSetLayoutBinding binding{};
        binding.binding = 0;
        binding.descriptorType = VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE; // Defaulting to sampled image for now
        // If we want multiple types in one heap, we need separate bindings or aliasing. 
        // For simplicity, let's assume this heap is for a specific type or we just use one huge binding per type?
        // Actually, 'EDescriptorHeapType' suggests we segregate by type (CBV_SRV_UAV, SAMPLER).
        // So validation:

        if (type == EDescriptorHeapType::SAMPLER)
            binding.descriptorType = VK_DESCRIPTOR_TYPE_SAMPLER;
        else if (type == EDescriptorHeapType::CBV_SRV_UAV)
            binding.descriptorType = VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE; // Just one for now for simplicity? 
            // REAL implementation would probably need multiple bindings for different types or use descriptor indexing features.
            // But let's start with S SAMPLED_IMAGE for SRV heap.
        else
            binding.descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;

        binding.descriptorCount = descriptorLimit;
        binding.stageFlags = VK_SHADER_STAGE_ALL;
        binding.pImmutableSamplers = nullptr;

        VkDescriptorBindingFlags bindingFlags = VK_DESCRIPTOR_BINDING_PARTIALLY_BOUND_BIT |
            VK_DESCRIPTOR_BINDING_UPDATE_AFTER_BIND_BIT;

        VkDescriptorSetLayoutBindingFlagsCreateInfo bindingFlagsInfo{};
        bindingFlagsInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_BINDING_FLAGS_CREATE_INFO;
        bindingFlagsInfo.bindingCount = 1;
        bindingFlagsInfo.pBindingFlags = &bindingFlags;

        VkDescriptorSetLayoutCreateInfo layoutInfo{};
        layoutInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
        layoutInfo.bindingCount = 1;
        layoutInfo.pBindings = &binding;
        layoutInfo.flags = VK_DESCRIPTOR_SET_LAYOUT_CREATE_UPDATE_AFTER_BIND_POOL_BIT;
        layoutInfo.pNext = &bindingFlagsInfo;

        if (vkCreateDescriptorSetLayout(static_cast<VkDevice>(m_Device->GetHandle()), &layoutInfo, nullptr, &m_Layout)
            != VK_SUCCESS)
        {
            std::cerr << "Failed to create bindless descriptor set layout!" << std::endl;
        }

        // 3. Allocate Descriptor Set
        VkDescriptorSetAllocateInfo allocInfo{};
        allocInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO;
        allocInfo.descriptorPool = m_Pool;
        allocInfo.descriptorSetCount = 1;
        allocInfo.pSetLayouts = &m_Layout;

        if (vkAllocateDescriptorSets(static_cast<VkDevice>(m_Device->GetHandle()), &allocInfo, &m_DescriptorSet) !=
            VK_SUCCESS)
        {
            std::cerr << "Failed to allocate bindless descriptor set!" << std::endl;
        }
    }

    RHIVkDescriptorHeap::~RHIVkDescriptorHeap()
    {
        if (m_Layout != VK_NULL_HANDLE)
            vkDestroyDescriptorSetLayout(static_cast<VkDevice>(m_Device->GetHandle()), m_Layout, nullptr);
        if (m_Pool != VK_NULL_HANDLE)
            vkDestroyDescriptorPool(static_cast<VkDevice>(m_Device->GetHandle()), m_Pool, nullptr);
    }

    UInt32 RHIVkDescriptorHeap::Allocate(UInt32 count)
    {
        std::lock_guard<std::mutex> lock(m_Mutex);
        // Simple linear allocation for now, no fragmentation handling
        if (m_CurrentOffset + count > m_Limit)
        {
            // Try to reuse freed intervals?
            if (!m_FreeIntervals.empty())
            {
                // Check head of queue
                auto& interval = m_FreeIntervals.front();
                if (interval.second >= count)
                {
                    UInt32 offset = interval.first;
                    // Update interval
                    if (interval.second > count)
                    {
                        interval.first += count;
                        interval.second -= count;
                    }
                    else
                    {
                        m_FreeIntervals.pop();
                    }
                    return offset;
                }
            }
            return 0xFFFFFFFF; // OOM
        }

        UInt32 offset = m_CurrentOffset;
        m_CurrentOffset += count;
        return offset;
    }

    void RHIVkDescriptorHeap::Free(UInt32 index, UInt32 count)
    {
        std::lock_guard<std::mutex> lock(m_Mutex);
        m_FreeIntervals.push({index, count});
    }
}

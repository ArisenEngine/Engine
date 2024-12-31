#pragma once
#include <vulkan/vulkan.h>
namespace NebulaEngine::RHI
{
    /// 
    /// @param binding 
    /// @param type 
    /// @param count 
    /// @param stage
    /// @param pImmutableSamplers
    /// @return 
    inline VkDescriptorSetLayoutBinding DescriptorSetLayoutBinding(uint32_t binding, VkDescriptorType type,
        uint32_t count, VkShaderStageFlags stage, const VkSampler* pImmutableSamplers)
    {
        VkDescriptorSetLayoutBinding layoutBinding{};
        layoutBinding.binding = binding;
        layoutBinding.descriptorType = type;
        layoutBinding.descriptorCount = count;
        layoutBinding.stageFlags = stage;
        layoutBinding.pImmutableSamplers = pImmutableSamplers;
        
        return layoutBinding;
    }

    
    inline VkDescriptorSetLayoutCreateInfo DescriptorSetLayoutCreateInfo(uint32_t bindingCount, const VkDescriptorSetLayoutBinding* pBindings)
    {
        VkDescriptorSetLayoutCreateInfo layoutInfo{};
        layoutInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
        // TODO set flags
        // layoutInfo.flags = VK_DESCRIPTOR_SET_LAYOUT_CREATE_PUSH_DESCRIPTOR_BIT_KHR;
        layoutInfo.bindingCount = bindingCount;
        layoutInfo.pBindings = pBindings;
        return layoutInfo;
    }
}

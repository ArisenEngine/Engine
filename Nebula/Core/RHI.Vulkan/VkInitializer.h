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
        // TODO: set flags
        // layoutInfo.flags = VK_DESCRIPTOR_SET_LAYOUT_CREATE_PUSH_DESCRIPTOR_BIT_KHR;
        layoutInfo.bindingCount = bindingCount;
        layoutInfo.pBindings = pBindings;
        return layoutInfo;
    }

    inline VkDescriptorPoolSize DescriptorPoolSize(EDescriptorType type, UInt32 count)
    {
        VkDescriptorPoolSize poolSize{};
        poolSize.type = static_cast<VkDescriptorType>(type);
        poolSize.descriptorCount = count;
        return poolSize;
    }

    inline VkDescriptorPoolCreateInfo DescriptorPoolCreateInfo(UInt32 poolSizeCount, const VkDescriptorPoolSize* poolSize, UInt32 maxSets)
    {
        VkDescriptorPoolCreateInfo poolInfo{};
        poolInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
        poolInfo.poolSizeCount = poolSizeCount;
        poolInfo.pPoolSizes = poolSize;
        poolInfo.maxSets = maxSets;
        // TODO: set flags
        // poolInfo.flags = VK_DESCRIPTOR_POOL_CREATE_HOST_ONLY_BIT_EXT;
        return poolInfo;
    }

    inline VkDescriptorSetAllocateInfo DescriptorSetAllocateInfo(VkDescriptorPool pool, UInt32 descriptorSetCount, const VkDescriptorSetLayout* pSetLayouts)
    {
        VkDescriptorSetAllocateInfo allocInfo{};
        allocInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO;
        allocInfo.descriptorPool = pool;
        allocInfo.descriptorSetCount = (descriptorSetCount);
        allocInfo.pSetLayouts = pSetLayouts;
        return allocInfo;
    }

    inline VkWriteDescriptorSet WriteDescriptorSet(
        VkDescriptorSet dstSet, UInt32 dstBinding, UInt32 dstArrayElement, UInt32 descriptorCount,
        VkDescriptorType descriptorType, const VkDescriptorImageInfo* pImageInfo, const VkDescriptorBufferInfo* pBufferInfo,
        const VkBufferView* pTexelBufferView)
    {
        VkWriteDescriptorSet descriptorWrite{};
        descriptorWrite.sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
        descriptorWrite.dstSet = dstSet;
        descriptorWrite.dstBinding = dstBinding;
        descriptorWrite.dstArrayElement = dstArrayElement;
        descriptorWrite.descriptorCount = descriptorCount;
        descriptorWrite.descriptorType = descriptorType;
        descriptorWrite.pImageInfo = pImageInfo;
        descriptorWrite.pBufferInfo = pBufferInfo;
        descriptorWrite.pTexelBufferView = pTexelBufferView;
        return descriptorWrite;
    }
    
}

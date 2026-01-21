#include "RHIVkBindlessManager.h"
#include "../Devices/RHIVkDevice.h"
#include "../Handles/RHIVkImageHandle.h"
#include "../Handles/RHIVkBufferHandle.h"
#include "../Program/RHIVkSampler.h"
#include "../VkInitializer.h"

namespace ArisenEngine::RHI
{
    RHIVkBindlessManager::RHIVkBindlessManager(RHIVkDevice* device)
        : m_Device(device)
    {
        m_ImageFreeList.capacity = MAX_BINDLESS_IMAGES;
        m_SamplerFreeList.capacity = MAX_BINDLESS_SAMPLERS;
        m_BufferFreeList.capacity = MAX_BINDLESS_BUFFERS;
    }

    RHIVkBindlessManager::~RHIVkBindlessManager()
    {
        Shutdown();
    }

    void RHIVkBindlessManager::Initialize()
    {
        VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());

        // 1. Create Descriptor Set Layout
        VkDescriptorSetLayoutBinding bindings[3] = {};
        
        // Binding 0: Sampled Images
        bindings[0].binding = IMAGE_BINDING;
        bindings[0].descriptorType = VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE;
        bindings[0].descriptorCount = MAX_BINDLESS_IMAGES;
        bindings[0].stageFlags = VK_SHADER_STAGE_ALL;
        bindings[0].pImmutableSamplers = nullptr;

        // Binding 1: Samplers
        bindings[1].binding = SAMPLER_BINDING;
        bindings[1].descriptorType = VK_DESCRIPTOR_TYPE_SAMPLER;
        bindings[1].descriptorCount = MAX_BINDLESS_SAMPLERS;
        bindings[1].stageFlags = VK_SHADER_STAGE_ALL;
        bindings[1].pImmutableSamplers = nullptr;

        // Binding 2: Storage Buffers
        bindings[2].binding = BUFFER_BINDING;
        bindings[2].descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;
        bindings[2].descriptorCount = MAX_BINDLESS_BUFFERS;
        bindings[2].stageFlags = VK_SHADER_STAGE_ALL;
        bindings[2].pImmutableSamplers = nullptr;

        VkDescriptorBindingFlags bindingFlags[3] = {
            VK_DESCRIPTOR_BINDING_PARTIALLY_BOUND_BIT | VK_DESCRIPTOR_BINDING_UPDATE_AFTER_BIND_BIT,
            VK_DESCRIPTOR_BINDING_PARTIALLY_BOUND_BIT | VK_DESCRIPTOR_BINDING_UPDATE_AFTER_BIND_BIT,
            VK_DESCRIPTOR_BINDING_PARTIALLY_BOUND_BIT | VK_DESCRIPTOR_BINDING_UPDATE_AFTER_BIND_BIT
        };

        VkDescriptorSetLayoutBindingFlagsCreateInfo layoutFlags{};
        layoutFlags.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_BINDING_FLAGS_CREATE_INFO;
        layoutFlags.bindingCount = 3;
        layoutFlags.pBindingFlags = bindingFlags;

        VkDescriptorSetLayoutCreateInfo layoutInfo{};
        layoutInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
        layoutInfo.bindingCount = 3;
        layoutInfo.pBindings = bindings;
        layoutInfo.flags = VK_DESCRIPTOR_SET_LAYOUT_CREATE_UPDATE_AFTER_BIND_POOL_BIT;
        layoutInfo.pNext = &layoutFlags;

        if (vkCreateDescriptorSetLayout(vkDevice, &layoutInfo, nullptr, &m_DescriptorSetLayout) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkBindlessManager::Initialize]: Failed to create bindless descriptor set layout!");
        }

        // 2. Create Descriptor Pool
        VkDescriptorPoolSize poolSizes[3] = {};
        poolSizes[0].type = VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE;
        poolSizes[0].descriptorCount = MAX_BINDLESS_IMAGES;
        poolSizes[1].type = VK_DESCRIPTOR_TYPE_SAMPLER;
        poolSizes[1].descriptorCount = MAX_BINDLESS_SAMPLERS;
        poolSizes[2].type = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;
        poolSizes[2].descriptorCount = MAX_BINDLESS_BUFFERS;

        VkDescriptorPoolCreateInfo poolInfo{};
        poolInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
        poolInfo.maxSets = 1;
        poolInfo.poolSizeCount = 3;
        poolInfo.pPoolSizes = poolSizes;
        poolInfo.flags = VK_DESCRIPTOR_POOL_CREATE_UPDATE_AFTER_BIND_BIT;

        if (vkCreateDescriptorPool(vkDevice, &poolInfo, nullptr, &m_DescriptorPool) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkBindlessManager::Initialize]: Failed to create bindless descriptor pool!");
        }

        // 3. Allocate Descriptor Set
        VkDescriptorSetAllocateInfo allocInfo{};
        allocInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO;
        allocInfo.descriptorPool = m_DescriptorPool;
        allocInfo.descriptorSetCount = 1;
        allocInfo.pSetLayouts = &m_DescriptorSetLayout;

        if (vkAllocateDescriptorSets(vkDevice, &allocInfo, &m_DescriptorSet) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkBindlessManager::Initialize]: Failed to allocate bindless descriptor set!");
        }
    }

    void RHIVkBindlessManager::Shutdown()
    {
        VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
        if (m_DescriptorSetLayout != VK_NULL_HANDLE)
        {
            vkDestroyDescriptorSetLayout(vkDevice, m_DescriptorSetLayout, nullptr);
            m_DescriptorSetLayout = VK_NULL_HANDLE;
        }
        if (m_DescriptorPool != VK_NULL_HANDLE)
        {
            vkDestroyDescriptorPool(vkDevice, m_DescriptorPool, nullptr);
            m_DescriptorPool = VK_NULL_HANDLE;
        }
        m_DescriptorSet = VK_NULL_HANDLE;
    }

    UInt32 RHIVkBindlessManager::RegisterImage(ImageHandle* image)
    {
        UInt32 index = AcquireIndex(m_ImageFreeList);
        if (index == 0xFFFFFFFF) return index;

        VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
        RHIVkImageHandle* vkImage = static_cast<RHIVkImageHandle*>(image);

        VkDescriptorImageInfo imageInfo{};
        imageInfo.imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
        imageInfo.imageView = static_cast<VkImageView>(image->GetMemoryView()->GetView());
        imageInfo.sampler = VK_NULL_HANDLE;

        VkWriteDescriptorSet write{};
        write.sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
        write.dstSet = m_DescriptorSet;
        write.dstBinding = IMAGE_BINDING;
        write.dstArrayElement = index;
        write.descriptorType = VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE;
        write.descriptorCount = 1;
        write.pImageInfo = &imageInfo;

        vkUpdateDescriptorSets(vkDevice, 1, &write, 0, nullptr);

        return index;
    }

    UInt32 RHIVkBindlessManager::RegisterSampler(RHISampler* sampler)
    {
        UInt32 index = AcquireIndex(m_SamplerFreeList);
        if (index == 0xFFFFFFFF) return index;

        VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
        RHIVkSampler* vkSampler = static_cast<RHIVkSampler*>(sampler);

        VkDescriptorImageInfo samplerInfo{};
        samplerInfo.sampler = static_cast<VkSampler>(vkSampler->GetHandle());

        VkWriteDescriptorSet write{};
        write.sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
        write.dstSet = m_DescriptorSet;
        write.dstBinding = SAMPLER_BINDING;
        write.dstArrayElement = index;
        write.descriptorType = VK_DESCRIPTOR_TYPE_SAMPLER;
        write.descriptorCount = 1;
        write.pImageInfo = &samplerInfo;

        vkUpdateDescriptorSets(vkDevice, 1, &write, 0, nullptr);

        return index;
    }

    UInt32 RHIVkBindlessManager::RegisterBuffer(BufferHandle* buffer)
    {
        UInt32 index = AcquireIndex(m_BufferFreeList);
        if (index == 0xFFFFFFFF) return index;

        VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
        RHIVkBufferHandle* vkBuffer = static_cast<RHIVkBufferHandle*>(buffer);

        VkDescriptorBufferInfo bufferInfo{};
        bufferInfo.buffer = static_cast<VkBuffer>(vkBuffer->GetHandle());
        bufferInfo.offset = 0;
        bufferInfo.range = vkBuffer->BufferSize();

        VkWriteDescriptorSet write{};
        write.sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
        write.dstSet = m_DescriptorSet;
        write.dstBinding = BUFFER_BINDING;
        write.dstArrayElement = index;
        write.descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;
        write.descriptorCount = 1;
        write.pBufferInfo = &bufferInfo;

        vkUpdateDescriptorSets(vkDevice, 1, &write, 0, nullptr);

        return index;
    }

    void RHIVkBindlessManager::UnregisterImage(UInt32 index)
    {
        ReleaseIndex(m_ImageFreeList, index);
    }

    void RHIVkBindlessManager::UnregisterSampler(UInt32 index)
    {
        ReleaseIndex(m_SamplerFreeList, index);
    }

    void RHIVkBindlessManager::UnregisterBuffer(UInt32 index)
    {
        ReleaseIndex(m_BufferFreeList, index);
    }

    UInt32 RHIVkBindlessManager::AcquireIndex(FreeList& list)
    {
        std::lock_guard<std::mutex> lock(list.mutex);
        if (!list.freeIndices.empty())
        {
            UInt32 index = list.freeIndices.back();
            list.freeIndices.pop_back();
            return index;
        }

        if (list.nextIndex < list.capacity)
        {
            return list.nextIndex++;
        }

        return 0xFFFFFFFF;
    }

    void RHIVkBindlessManager::ReleaseIndex(FreeList& list, UInt32 index)
    {
        std::lock_guard<std::mutex> lock(list.mutex);
        list.freeIndices.push_back(index);
    }
}

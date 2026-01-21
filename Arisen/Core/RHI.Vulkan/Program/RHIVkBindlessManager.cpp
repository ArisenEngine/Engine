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
        auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);
        VkDevice device = static_cast<VkDevice>(vkDevice->GetHandle());

        // 1. Create Descriptor Set Layout
        Containers::Vector<VkDescriptorSetLayoutBinding> bindings;
        
        // Bindless Image - Descriptor Count can be huge (e.g., 100,000)
        VkDescriptorSetLayoutBinding imageBinding{};
        imageBinding.binding = 0;
        imageBinding.descriptorType = VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE;
        imageBinding.descriptorCount = 10000; // Placeholder Max
        imageBinding.stageFlags = VK_SHADER_STAGE_ALL;
        imageBinding.pImmutableSamplers = nullptr;
        bindings.emplace_back(imageBinding);

        // Bindless Buffer
        VkDescriptorSetLayoutBinding bufferBinding{};
        bufferBinding.binding = 1;
        bufferBinding.descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;
        bufferBinding.descriptorCount = 10000;
        bufferBinding.stageFlags = VK_SHADER_STAGE_ALL;
        bufferBinding.pImmutableSamplers = nullptr;
        bindings.emplace_back(bufferBinding);

        VkDescriptorSetLayoutCreateInfo layoutInfo{};
        layoutInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
        layoutInfo.bindingCount = static_cast<uint32_t>(bindings.size());
        layoutInfo.pBindings = bindings.data();
        layoutInfo.flags = VK_DESCRIPTOR_SET_LAYOUT_CREATE_UPDATE_AFTER_BIND_POOL_BIT;

        // Indexing flags for bindless
        VkDescriptorBindingFlags bindingFlags[2] = {
            VK_DESCRIPTOR_BINDING_PARTIALLY_BOUND_BIT | VK_DESCRIPTOR_BINDING_UPDATE_AFTER_BIND_BIT,
            VK_DESCRIPTOR_BINDING_PARTIALLY_BOUND_BIT | VK_DESCRIPTOR_BINDING_UPDATE_AFTER_BIND_BIT | VK_DESCRIPTOR_BINDING_VARIABLE_DESCRIPTOR_COUNT_BIT
        };

        VkDescriptorSetLayoutBindingFlagsCreateInfo layoutBindingFlags{};
        layoutBindingFlags.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_BINDING_FLAGS_CREATE_INFO;
        layoutBindingFlags.bindingCount = 2;
        layoutBindingFlags.pBindingFlags = bindingFlags;
        layoutInfo.pNext = &layoutBindingFlags;

        if (vkCreateDescriptorSetLayout(device, &layoutInfo, nullptr, &m_DescriptorSetLayout) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkBindlessManager]: failed to create descriptor set layout!");
        }

        // 2. Create Descriptor Pool
        VkDescriptorPoolSize poolSizes[2];
        poolSizes[0].type = VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE;
        poolSizes[0].descriptorCount = 10000;
        poolSizes[1].type = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;
        poolSizes[1].descriptorCount = 10000;

        VkDescriptorPoolCreateInfo poolInfo{};
        poolInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
        poolInfo.flags = VK_DESCRIPTOR_POOL_CREATE_UPDATE_AFTER_BIND_BIT;
        poolInfo.maxSets = 1;
        poolInfo.poolSizeCount = 2;
        poolInfo.pPoolSizes = poolSizes;
        poolInfo.pPoolSizes = poolSizes;

        if (vkCreateDescriptorPool(device, &poolInfo, nullptr, &m_DescriptorPool) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkBindlessManager]: failed to create descriptor pool!");
        }

        // 3. Allocate Descriptor Set
        VkDescriptorSetVariableDescriptorCountAllocateInfo variableDescriptorCountAllocInfo{};
        variableDescriptorCountAllocInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_VARIABLE_DESCRIPTOR_COUNT_ALLOCATE_INFO;
        uint32_t variableDescriptorCount = 10000;
        variableDescriptorCountAllocInfo.descriptorSetCount = 1;
        variableDescriptorCountAllocInfo.pDescriptorCounts = &variableDescriptorCount;

        VkDescriptorSetAllocateInfo allocInfo{};
        allocInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO;
        allocInfo.descriptorPool = m_DescriptorPool;
        allocInfo.descriptorSetCount = 1;
        allocInfo.pSetLayouts = &m_DescriptorSetLayout;
        allocInfo.pNext = &variableDescriptorCountAllocInfo;

        if (vkAllocateDescriptorSets(device, &allocInfo, &m_DescriptorSet) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkBindlessManager]: failed to allocate descriptor set!");
        }
        std::cout << "[DEBUG] RHIVkBindlessManager::Initialize END" << std::endl;
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
        
        MemoryView* view = image->GetMemoryView();
        if (!view)
        {
            LOG_ERROR("[RHIVkBindlessManager::RegisterImage]: Image " + image->GetName() + " has no memory view!");
            return 0xFFFFFFFF;
        }
        
        imageInfo.imageView = static_cast<VkImageView>(view->GetView());
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

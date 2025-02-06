#pragma once
#include <format>
#include <vulkan/vulkan.h>
#include "RHI/Enums/Pipeline/EDescriptorType.h"
#include "RHI/Enums/Image/EImageTiling.h"
#include "RHI/Enums/Image/EImageType.h"
#include "RHI/Enums/Image/EImageUsageFlagBits.h"
#include "RHI/Enums/Pipeline/EAccessFlag.h"
#include "RHI/Handles/BufferHandle.h"
#include "RHI/Memory/ImageSubresourceLayers.h"
#include "RHI/Synchronization/RHIImageSubresourceRange.h"

#define VK_STRUCT_INITIALIZE(type, name) type name ##{##};

namespace ArisenEngine::RHI
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
        VK_STRUCT_INITIALIZE(VkDescriptorSetLayoutBinding, layoutBinding)
        layoutBinding.binding = binding;
        layoutBinding.descriptorType = type;
        layoutBinding.descriptorCount = count;
        layoutBinding.stageFlags = stage;
        layoutBinding.pImmutableSamplers = pImmutableSamplers;
        
        return layoutBinding;
    }

    
    inline VkDescriptorSetLayoutCreateInfo DescriptorSetLayoutCreateInfo(uint32_t bindingCount, const VkDescriptorSetLayoutBinding* pBindings)
    {
        VK_STRUCT_INITIALIZE(VkDescriptorSetLayoutCreateInfo, layoutInfo)
        layoutInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
        // TODO: set flags
        // layoutInfo.flags = VK_DESCRIPTOR_SET_LAYOUT_CREATE_PUSH_DESCRIPTOR_BIT_KHR;
        layoutInfo.bindingCount = bindingCount;
        layoutInfo.pBindings = pBindings;
        return layoutInfo;
    }

    inline VkDescriptorPoolSize DescriptorPoolSize(EDescriptorType type, UInt32 count)
    {
        VK_STRUCT_INITIALIZE(VkDescriptorPoolSize, poolSize)
        poolSize.type = static_cast<VkDescriptorType>(type);
        poolSize.descriptorCount = count;
        return poolSize;
    }

    inline VkDescriptorPoolCreateInfo DescriptorPoolCreateInfo(UInt32 poolSizeCount, const VkDescriptorPoolSize* poolSize, UInt32 maxSets)
    {
        VK_STRUCT_INITIALIZE(VkDescriptorPoolCreateInfo, poolInfo)
        poolInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
        poolInfo.poolSizeCount = poolSizeCount;
        poolInfo.pPoolSizes = poolSize;
        poolInfo.maxSets = maxSets;
        // TODO: set flags
        // poolInfo.flags = VK_DESCRIPTOR_POOL_CREATE_HOST_ONLY_BIT_EXT;
        return poolInfo;
    }

    inline VkDescriptorSetAllocateInfo DescriptorSetAllocateInfo(
        VkDescriptorPool pool, UInt32 descriptorSetCount,
        const VkDescriptorSetLayout* pSetLayouts)
    {
        VK_STRUCT_INITIALIZE(VkDescriptorSetAllocateInfo, allocInfo)
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
        VK_STRUCT_INITIALIZE(VkWriteDescriptorSet, descriptorWrite)
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

    inline VkDescriptorImageInfo DescriptorImageInfo(VkSampler sampler, VkImageView imageView, VkImageLayout imageLayout)
    {
        VkDescriptorImageInfo descriptorImageInfo
        {
            sampler,
            imageView,
            imageLayout
        };
        return descriptorImageInfo;
    }

    inline VkDescriptorBufferInfo DescriptorBufferInfo(VkBuffer buffer, VkDeviceSize offset, VkDeviceSize range)
    {
        VkDescriptorBufferInfo descriptorBufferInfo
        {
            buffer,
            offset,
            range
        };
        return descriptorBufferInfo;
    }

    inline VkBufferCreateInfo BufferCreateInfo(
        UInt32 createFlagBits,
        UInt64 size, 
        UInt32 usage,
        ESharingMode sharingMode,
        UInt32 queueFamilyIndexCount,
        const void* pQueueFamilyIndices)
    {
        VK_STRUCT_INITIALIZE(VkBufferCreateInfo, bufferInfo)
        bufferInfo.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
        bufferInfo.flags = createFlagBits;
        bufferInfo.size = size;
        bufferInfo.usage = static_cast<VkBufferUsageFlags>(usage);
        bufferInfo.sharingMode = static_cast<VkSharingMode>(sharingMode);

        if (sharingMode == SHARING_MODE_CONCURRENT)
        {
            bufferInfo.queueFamilyIndexCount = static_cast<uint32_t>(queueFamilyIndexCount);
            bufferInfo.pQueueFamilyIndices = static_cast<const uint32_t*>(pQueueFamilyIndices);
        }

        return bufferInfo;
    }

    inline VkImageCreateInfo ImageCreateInfo(
        EImageType imageType,
        UInt32 width, UInt32 height, UInt32 depth, UInt32 mipLevels, UInt32 arrayLayers,
        EFormat format, EImageTiling tiling, EImageLayout initialLayout, UInt32 usage,
        ESampleCountFlagBits sampleCount, ESharingMode sharingMode,
        UInt32 queueFamilyIndexCount,
        const void* pQueueFamilyIndices)
    {
        VK_STRUCT_INITIALIZE(VkImageCreateInfo, imageInfo)
        imageInfo.sType = VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO;
        imageInfo.imageType = static_cast<VkImageType>(imageType);
        imageInfo.extent.width = width;
        imageInfo.extent.height = height;
        imageInfo.extent.depth = depth;
        imageInfo.mipLevels = mipLevels;
        imageInfo.arrayLayers = arrayLayers;
        imageInfo.format = static_cast<VkFormat>(format);
        imageInfo.tiling = static_cast<VkImageTiling>(tiling);
        imageInfo.initialLayout = static_cast<VkImageLayout>(initialLayout);
        imageInfo.usage = static_cast<VkImageUsageFlags>(usage);
        imageInfo.samples = static_cast<VkSampleCountFlagBits>(sampleCount);
        imageInfo.sharingMode = static_cast<VkSharingMode>(sharingMode);

        if (sharingMode == RHI::SHARING_MODE_CONCURRENT)
        {
            imageInfo.queueFamilyIndexCount = static_cast<uint32_t>(queueFamilyIndexCount);
            imageInfo.pQueueFamilyIndices = static_cast<const uint32_t*>(pQueueFamilyIndices);
        }
        
        return imageInfo;
    }

    inline VkBufferImageCopy BufferImageCopyRegion(
        UInt64 bufferOffset,
        UInt32 bufferRowLength,
        UInt32 bufferImageHeight,
        ImageSubresourceLayers imageSubresource,
    SInt32 offsetX, SInt32 offsetY, SInt32 offsetZ,
    UInt32 width, UInt32 height, UInt32 depth)
    {
        VK_STRUCT_INITIALIZE(VkBufferImageCopy, imageCopy)
        imageCopy.bufferOffset = static_cast<VkDeviceSize>(bufferOffset);
        imageCopy.bufferRowLength = static_cast<uint32_t>(bufferRowLength);
        imageCopy.bufferImageHeight = static_cast<uint32_t>(bufferImageHeight);
        imageCopy.imageSubresource.aspectMask = static_cast<VkImageAspectFlags>(imageSubresource.aspectMask);
        imageCopy.imageSubresource.mipLevel = static_cast<uint32_t>(imageSubresource.mipLevel);
        imageCopy.imageSubresource.baseArrayLayer = static_cast<uint32_t>(imageSubresource.baseArrayLayer);
        imageCopy.imageSubresource.layerCount = static_cast<uint32_t>(imageSubresource.layerCount);
        imageCopy.imageOffset.x = offsetX;
        imageCopy.imageOffset.y = offsetY;
        imageCopy.imageOffset.z = offsetZ;
        imageCopy.imageExtent.width = width;
        imageCopy.imageExtent.height = height;
        imageCopy.imageExtent.depth = depth;
      
        return imageCopy;
    }

    inline VkMemoryBarrier CreateMemoryBarrier(EAccessFlag srcAccess, EAccessFlag dstAccess)
    {
        VK_STRUCT_INITIALIZE(VkMemoryBarrier, barrier)
        barrier.pNext = nullptr;
        barrier.sType = VK_STRUCTURE_TYPE_MEMORY_BARRIER;
        barrier.srcAccessMask = static_cast<VkAccessFlags>(srcAccess);
        barrier.dstAccessMask = static_cast<VkAccessFlags>(dstAccess);
        return barrier;
    }

    inline VkBufferMemoryBarrier BufferMemoryBarrier(
        EAccessFlag srcAccess, EAccessFlag dstAccess,
        UInt32 srcQueueFamilyIndex, UInt32 dstQueueFamilyIndex, BufferHandle* bufferHandle)
    {
        VK_STRUCT_INITIALIZE(VkBufferMemoryBarrier, barrier)
        barrier.pNext = nullptr;
        barrier.sType = VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER;
        barrier.srcAccessMask = static_cast<VkAccessFlags>(srcAccess);
        barrier.dstAccessMask = static_cast<VkAccessFlags>(dstAccess);
        barrier.srcQueueFamilyIndex = srcQueueFamilyIndex;
        barrier.dstQueueFamilyIndex = dstQueueFamilyIndex;
        barrier.buffer = static_cast<VkBuffer>(bufferHandle->GetHandle());
        barrier.offset = bufferHandle->Offset();
        barrier.size = bufferHandle->Range();
        
        return barrier;
    }

    inline VkImageMemoryBarrier ImageMemoryBarrier(
        EAccessFlag srcAccess, EAccessFlag dstAccess,
        UInt32 srcQueueFamilyIndex, UInt32 dstQueueFamilyIndex,
        EImageLayout oldLayout, EImageLayout newLayout, ImageHandle* imageHandle,
        RHIImageSubresourceRange&& subResourceRange)
    {
        VK_STRUCT_INITIALIZE(VkImageMemoryBarrier, barrier)
        barrier.sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER;
        barrier.pNext = nullptr;
        barrier.srcAccessMask = static_cast<VkAccessFlags>(srcAccess);
        barrier.dstAccessMask = static_cast<VkAccessFlags>(dstAccess);
        barrier.srcQueueFamilyIndex = srcQueueFamilyIndex;
        barrier.dstQueueFamilyIndex = dstQueueFamilyIndex;
        barrier.oldLayout = static_cast<VkImageLayout>(oldLayout);
        barrier.newLayout = static_cast<VkImageLayout>(newLayout);
        barrier.image = static_cast<VkImage>(imageHandle->GetHandle());
        barrier.subresourceRange.aspectMask = static_cast<VkImageAspectFlags>(subResourceRange.aspectMask);
        barrier.subresourceRange.baseMipLevel = subResourceRange.baseMipLevel;
        barrier.subresourceRange.levelCount = subResourceRange.levelCount;
        barrier.subresourceRange.baseArrayLayer = subResourceRange.baseArrayLayer;
        barrier.subresourceRange.layerCount = subResourceRange.layerCount;
        
        return barrier;
    }
}

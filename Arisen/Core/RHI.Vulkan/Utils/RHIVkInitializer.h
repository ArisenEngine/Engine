#pragma once
#include "../../Core.RHI/RHI/Core/RHICommon.h"
#include "RHI/Enums/Pipeline/EDescriptorType.h"
#include "RHI/Enums/Image/EImageTiling.h"
#include "RHI/Enums/Image/EImageType.h"
#include "RHI/Enums/Image/EImageUsageFlagBits.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Memory/ESharingMode.h"
#include "RHI/Enums/Pipeline/EAccessFlag.h"
#include "RHI/Enums/Pipeline/EPipelineStageFlag.h"
#include "RHI/Enums/Image/EImageLayout.h"
#include "RHI/Sync/RHIImageSubresourceRange.h"
#include "RHI/Allocation/RHIImageSubresourceLayers.h"
// #include "RHI/Handles/ImageHandle.h"

#define VK_STRUCT_INITIALIZE(type, name) type name ##{##};

//
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
                                                                   uint32_t count, VkShaderStageFlags stage,
                                                                   const VkSampler* pImmutableSamplers)
    {
        VK_STRUCT_INITIALIZE(VkDescriptorSetLayoutBinding, layoutBinding)
        layoutBinding.binding = binding;
        layoutBinding.descriptorType = type;
        layoutBinding.descriptorCount = count;
        layoutBinding.stageFlags = stage;
        layoutBinding.pImmutableSamplers = pImmutableSamplers;

        return layoutBinding;
    }


    inline VkDescriptorSetLayoutCreateInfo DescriptorSetLayoutCreateInfo(
        uint32_t bindingCount, const VkDescriptorSetLayoutBinding* pBindings)
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

    inline VkDescriptorPoolCreateInfo DescriptorPoolCreateInfo(UInt32 poolSizeCount,
                                                               const VkDescriptorPoolSize* poolSize, UInt32 maxSets)
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
        VkDescriptorType descriptorType, const VkDescriptorImageInfo* pImageInfo,
        const VkDescriptorBufferInfo* pBufferInfo,
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

    inline VkDescriptorImageInfo DescriptorImageInfo(VkSampler sampler, VkImageView imageView,
                                                     VkImageLayout imageLayout)
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
        RHIImageSubresourceLayers imageSubresource,
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

    inline VkImageCopy ImageCopyRegion(
        RHIImageSubresourceLayers srcSubresource,
        RHIOffset3D srcOffset,
        RHIImageSubresourceLayers dstSubresource,
        RHIOffset3D dstOffset,
        RHIExtent3D extent)
    {
        VK_STRUCT_INITIALIZE(VkImageCopy, imageCopy)
        imageCopy.srcSubresource.aspectMask = static_cast<VkImageAspectFlags>(srcSubresource.aspectMask);
        imageCopy.srcSubresource.mipLevel = static_cast<uint32_t>(srcSubresource.mipLevel);
        imageCopy.srcSubresource.baseArrayLayer = static_cast<uint32_t>(srcSubresource.baseArrayLayer);
        imageCopy.srcSubresource.layerCount = static_cast<uint32_t>(srcSubresource.layerCount);
        imageCopy.srcOffset.x = srcOffset.x;
        imageCopy.srcOffset.y = srcOffset.y;
        imageCopy.srcOffset.z = srcOffset.z;
        imageCopy.dstSubresource.aspectMask = static_cast<VkImageAspectFlags>(dstSubresource.aspectMask);
        imageCopy.dstSubresource.mipLevel = static_cast<uint32_t>(dstSubresource.mipLevel);
        imageCopy.dstSubresource.baseArrayLayer = static_cast<uint32_t>(dstSubresource.baseArrayLayer);
        imageCopy.dstSubresource.layerCount = static_cast<uint32_t>(dstSubresource.layerCount);
        imageCopy.dstOffset.x = dstOffset.x;
        imageCopy.dstOffset.y = dstOffset.y;
        imageCopy.dstOffset.z = dstOffset.z;
        imageCopy.extent.width = extent.width;
        imageCopy.extent.height = extent.height;
        imageCopy.extent.depth = extent.depth;
        return imageCopy;
    }

    inline VkMemoryBarrier2KHR MemoryBarrier2(
        VkPipelineStageFlags2KHR srcStageMask, VkAccessFlags2KHR srcAccessMask,
        VkPipelineStageFlags2KHR dstStageMask, VkAccessFlags2KHR dstAccessMask)
    {
        VK_STRUCT_INITIALIZE(VkMemoryBarrier2KHR, barrier)
        barrier.sType = VK_STRUCTURE_TYPE_MEMORY_BARRIER_2_KHR;
        barrier.srcStageMask = srcStageMask;
        barrier.srcAccessMask = srcAccessMask;
        barrier.dstStageMask = dstStageMask;
        barrier.dstAccessMask = dstAccessMask;
        return barrier;
    }

    inline VkBufferMemoryBarrier2KHR BufferMemoryBarrier2(
        VkPipelineStageFlags2KHR srcStageMask, VkAccessFlags2KHR srcAccessMask,
        VkPipelineStageFlags2KHR dstStageMask, VkAccessFlags2KHR dstAccessMask,
        UInt32 srcQueueFamilyIndex, UInt32 dstQueueFamilyIndex, VkBuffer buffer, VkDeviceSize offset, VkDeviceSize size)
    {
        VK_STRUCT_INITIALIZE(VkBufferMemoryBarrier2KHR, barrier)
        barrier.sType = VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER_2_KHR;
        barrier.srcStageMask = srcStageMask;
        barrier.srcAccessMask = srcAccessMask;
        barrier.dstStageMask = dstStageMask;
        barrier.dstAccessMask = dstAccessMask;
        barrier.srcQueueFamilyIndex = srcQueueFamilyIndex;
        barrier.dstQueueFamilyIndex = dstQueueFamilyIndex;
        barrier.buffer = buffer;
        barrier.offset = offset;
        barrier.size = size;
        return barrier;
    }

    inline VkImageMemoryBarrier2KHR ImageMemoryBarrier2(
        VkPipelineStageFlags2KHR srcStageMask, VkAccessFlags2KHR srcAccessMask,
        VkPipelineStageFlags2KHR dstStageMask, VkAccessFlags2KHR dstAccessMask,
        UInt32 srcQueueFamilyIndex, UInt32 dstQueueFamilyIndex,
        EImageLayout oldLayout, EImageLayout newLayout, VkImage image,
        const RHIImageSubresourceRange& subResourceRange)
    {
        VK_STRUCT_INITIALIZE(VkImageMemoryBarrier2KHR, barrier)
        barrier.sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER_2_KHR;
        barrier.srcStageMask = srcStageMask;
        barrier.srcAccessMask = srcAccessMask;
        barrier.dstStageMask = dstStageMask;
        barrier.dstAccessMask = dstAccessMask;
        barrier.srcQueueFamilyIndex = srcQueueFamilyIndex;
        barrier.dstQueueFamilyIndex = dstQueueFamilyIndex;
        barrier.oldLayout = static_cast<VkImageLayout>(oldLayout);
        barrier.newLayout = static_cast<VkImageLayout>(newLayout);
        barrier.image = image;
        barrier.subresourceRange.aspectMask = static_cast<VkImageAspectFlags>(subResourceRange.aspectMask);
        barrier.subresourceRange.baseMipLevel = subResourceRange.baseMipLevel;
        barrier.subresourceRange.levelCount = subResourceRange.levelCount;
        barrier.subresourceRange.baseArrayLayer = subResourceRange.baseArrayLayer;
        barrier.subresourceRange.layerCount = subResourceRange.layerCount;
        return barrier;
    }

    inline VkDependencyInfoKHR DependencyInfo(
        UInt32 memoryBarrierCount, const VkMemoryBarrier2KHR* pMemoryBarriers,
        UInt32 bufferMemoryBarrierCount, const VkBufferMemoryBarrier2KHR* pBufferMemoryBarriers,
        UInt32 imageMemoryBarrierCount, const VkImageMemoryBarrier2KHR* pImageMemoryBarriers,
        VkDependencyFlags dependencyFlags = 0)
    {
        VK_STRUCT_INITIALIZE(VkDependencyInfoKHR, dependencyInfo)
        dependencyInfo.sType = VK_STRUCTURE_TYPE_DEPENDENCY_INFO_KHR;
        dependencyInfo.dependencyFlags = dependencyFlags;
        dependencyInfo.memoryBarrierCount = memoryBarrierCount;
        dependencyInfo.pMemoryBarriers = pMemoryBarriers;
        dependencyInfo.bufferMemoryBarrierCount = bufferMemoryBarrierCount;
        dependencyInfo.pBufferMemoryBarriers = pBufferMemoryBarriers;
        dependencyInfo.imageMemoryBarrierCount = imageMemoryBarrierCount;
        dependencyInfo.pImageMemoryBarriers = pImageMemoryBarriers;
        return dependencyInfo;
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
        UInt32 srcQueueFamilyIndex, UInt32 dstQueueFamilyIndex, VkBuffer buffer, VkDeviceSize offset, VkDeviceSize size)
    {
        VK_STRUCT_INITIALIZE(VkBufferMemoryBarrier, barrier)
        barrier.pNext = nullptr;
        barrier.sType = VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER;
        barrier.srcAccessMask = static_cast<VkAccessFlags>(srcAccess);
        barrier.dstAccessMask = static_cast<VkAccessFlags>(dstAccess);
        barrier.srcQueueFamilyIndex = srcQueueFamilyIndex;
        barrier.dstQueueFamilyIndex = dstQueueFamilyIndex;
        barrier.buffer = buffer;
        barrier.offset = offset;
        barrier.size = size;

        return barrier;
    }

    inline VkImageMemoryBarrier ImageMemoryBarrier(
        EAccessFlag srcAccess, EAccessFlag dstAccess,
        UInt32 srcQueueFamilyIndex, UInt32 dstQueueFamilyIndex,
        EImageLayout oldLayout, EImageLayout newLayout, VkImage image,
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
        barrier.image = image;
        barrier.subresourceRange.aspectMask = static_cast<VkImageAspectFlags>(subResourceRange.aspectMask);
        barrier.subresourceRange.baseMipLevel = subResourceRange.baseMipLevel;
        barrier.subresourceRange.levelCount = subResourceRange.levelCount;
        barrier.subresourceRange.baseArrayLayer = subResourceRange.baseArrayLayer;
        barrier.subresourceRange.layerCount = subResourceRange.layerCount;

        return barrier;
    }

    inline VkPipelineStageFlags2KHR MapPipelineStageFlags2(EPipelineStageFlag flags)
    {
        VkPipelineStageFlags2KHR flags2 = 0;
        if (flags & PIPELINE_STAGE_TOP_OF_PIPE_BIT) flags2 |= VK_PIPELINE_STAGE_2_TOP_OF_PIPE_BIT_KHR;
        if (flags & PIPELINE_STAGE_DRAW_INDIRECT_BIT) flags2 |= VK_PIPELINE_STAGE_2_DRAW_INDIRECT_BIT_KHR;
        if (flags & PIPELINE_STAGE_VERTEX_INPUT_BIT) flags2 |= VK_PIPELINE_STAGE_2_VERTEX_INPUT_BIT_KHR;
        if (flags & PIPELINE_STAGE_VERTEX_SHADER_BIT) flags2 |= VK_PIPELINE_STAGE_2_VERTEX_SHADER_BIT_KHR;
        if (flags & PIPELINE_STAGE_TESSELLATION_CONTROL_SHADER_BIT) flags2 |=
            VK_PIPELINE_STAGE_2_TESSELLATION_CONTROL_SHADER_BIT_KHR;
        if (flags & PIPELINE_STAGE_TESSELLATION_EVALUATION_SHADER_BIT) flags2 |=
            VK_PIPELINE_STAGE_2_TESSELLATION_EVALUATION_SHADER_BIT_KHR;
        if (flags & PIPELINE_STAGE_GEOMETRY_SHADER_BIT) flags2 |= VK_PIPELINE_STAGE_2_GEOMETRY_SHADER_BIT_KHR;
        if (flags & PIPELINE_STAGE_FRAGMENT_SHADER_BIT) flags2 |= VK_PIPELINE_STAGE_2_FRAGMENT_SHADER_BIT_KHR;
        if (flags & PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT) flags2 |= VK_PIPELINE_STAGE_2_EARLY_FRAGMENT_TESTS_BIT_KHR;
        if (flags & PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT) flags2 |= VK_PIPELINE_STAGE_2_LATE_FRAGMENT_TESTS_BIT_KHR;
        if (flags & PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT) flags2 |=
            VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT_KHR;
        if (flags & PIPELINE_STAGE_COMPUTE_SHADER_BIT) flags2 |= VK_PIPELINE_STAGE_2_COMPUTE_SHADER_BIT_KHR;
        if (flags & PIPELINE_STAGE_TRANSFER_BIT) flags2 |= VK_PIPELINE_STAGE_2_ALL_TRANSFER_BIT_KHR;
        if (flags & PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT) flags2 |= VK_PIPELINE_STAGE_2_BOTTOM_OF_PIPE_BIT_KHR;
        if (flags & PIPELINE_STAGE_HOST_BIT) flags2 |= VK_PIPELINE_STAGE_2_HOST_BIT_KHR;
        if (flags & PIPELINE_STAGE_ALL_GRAPHICS_BIT) flags2 |= VK_PIPELINE_STAGE_2_ALL_GRAPHICS_BIT_KHR;
        if (flags & PIPELINE_STAGE_ALL_COMMANDS_BIT) flags2 |= VK_PIPELINE_STAGE_2_ALL_COMMANDS_BIT_KHR;
        if (flags & PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR) flags2 |=
            VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_BUILD_BIT_KHR;
        if (flags & PIPELINE_STAGE_RAY_TRACING_SHADER_BIT_KHR) flags2 |= VK_PIPELINE_STAGE_2_RAY_TRACING_SHADER_BIT_KHR;
        if (flags & PIPELINE_STAGE_TASK_SHADER_BIT_EXT) flags2 |= VK_PIPELINE_STAGE_2_TASK_SHADER_BIT_EXT;
        if (flags & PIPELINE_STAGE_MESH_SHADER_BIT_EXT) flags2 |= VK_PIPELINE_STAGE_2_MESH_SHADER_BIT_EXT;

        return flags2;
    }

    inline VkAccessFlags2KHR MapAccessFlags2(EAccessFlag flags)
    {
        VkAccessFlags2KHR flags2 = 0;
        if (flags & ACCESS_INDIRECT_COMMAND_READ_BIT) flags2 |= VK_ACCESS_2_INDIRECT_COMMAND_READ_BIT_KHR;
        if (flags & ACCESS_INDEX_READ_BIT) flags2 |= VK_ACCESS_2_INDEX_READ_BIT_KHR;
        if (flags & ACCESS_VERTEX_ATTRIBUTE_READ_BIT) flags2 |= VK_ACCESS_2_VERTEX_ATTRIBUTE_READ_BIT_KHR;
        if (flags & ACCESS_UNIFORM_READ_BIT) flags2 |= VK_ACCESS_2_UNIFORM_READ_BIT_KHR;
        if (flags & ACCESS_INPUT_ATTACHMENT_READ_BIT) flags2 |= VK_ACCESS_2_INPUT_ATTACHMENT_READ_BIT_KHR;
        if (flags & ACCESS_SHADER_READ_BIT) flags2 |= VK_ACCESS_2_SHADER_READ_BIT_KHR;
        if (flags & ACCESS_SHADER_WRITE_BIT) flags2 |= VK_ACCESS_2_SHADER_WRITE_BIT_KHR;
        if (flags & ACCESS_COLOR_ATTACHMENT_READ_BIT) flags2 |= VK_ACCESS_2_COLOR_ATTACHMENT_READ_BIT_KHR;
        if (flags & ACCESS_COLOR_ATTACHMENT_WRITE_BIT) flags2 |= VK_ACCESS_2_COLOR_ATTACHMENT_WRITE_BIT_KHR;
        if (flags & ACCESS_DEPTH_STENCIL_ATTACHMENT_READ_BIT) flags2 |=
            VK_ACCESS_2_DEPTH_STENCIL_ATTACHMENT_READ_BIT_KHR;
        if (flags & ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT) flags2 |=
            VK_ACCESS_2_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT_KHR;
        if (flags & ACCESS_TRANSFER_READ_BIT) flags2 |= VK_ACCESS_2_TRANSFER_READ_BIT_KHR;
        if (flags & ACCESS_TRANSFER_WRITE_BIT) flags2 |= VK_ACCESS_2_TRANSFER_WRITE_BIT_KHR;
        if (flags & ACCESS_HOST_READ_BIT) flags2 |= VK_ACCESS_2_HOST_READ_BIT_KHR;
        if (flags & ACCESS_HOST_WRITE_BIT) flags2 |= VK_ACCESS_2_HOST_WRITE_BIT_KHR;
        if (flags & ACCESS_MEMORY_READ_BIT) flags2 |= VK_ACCESS_2_MEMORY_READ_BIT_KHR;
        if (flags & ACCESS_MEMORY_WRITE_BIT) flags2 |= VK_ACCESS_2_MEMORY_WRITE_BIT_KHR;
        if (flags & ACCESS_ACCELERATION_STRUCTURE_READ_BIT_KHR) flags2 |=
            VK_ACCESS_2_ACCELERATION_STRUCTURE_READ_BIT_KHR;
        if (flags & ACCESS_ACCELERATION_STRUCTURE_WRITE_BIT_KHR) flags2 |=
            VK_ACCESS_2_ACCELERATION_STRUCTURE_WRITE_BIT_KHR;

        return flags2;
    }

    inline VkSamplerCreateInfo SamplerCreateInfo(RHISamplerDesc&& desc)
    {
        VK_STRUCT_INITIALIZE(VkSamplerCreateInfo, samplerCreateInfo)
        samplerCreateInfo.sType = VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO;
        samplerCreateInfo.magFilter = static_cast<VkFilter>(desc.magFilter);
        samplerCreateInfo.minFilter = static_cast<VkFilter>(desc.minFilter);
        samplerCreateInfo.mipmapMode = static_cast<VkSamplerMipmapMode>(desc.mipmapMode);
        samplerCreateInfo.addressModeU = static_cast<::VkSamplerAddressMode>(desc.addressModeU);
        samplerCreateInfo.addressModeV = static_cast<::VkSamplerAddressMode>(desc.addressModeV);
        samplerCreateInfo.addressModeW = static_cast<::VkSamplerAddressMode>(desc.addressModeW);
        samplerCreateInfo.mipLodBias = static_cast<float>(desc.mipLodBias);
        samplerCreateInfo.anisotropyEnable = static_cast<VkBool32>(desc.anisotropyEnable);
        samplerCreateInfo.maxAnisotropy = static_cast<float>(desc.maxAnisotropy);
        samplerCreateInfo.compareEnable = static_cast<VkBool32>(desc.compareEnable);
        samplerCreateInfo.compareOp = static_cast<VkCompareOp>(desc.compareOp);
        samplerCreateInfo.minLod = static_cast<float>(desc.minLod);
        samplerCreateInfo.maxLod = static_cast<float>(desc.maxLod);
        samplerCreateInfo.borderColor = static_cast<VkBorderColor>(desc.borderColor);
        samplerCreateInfo.unnormalizedCoordinates = static_cast<VkBool32>(desc.unnormalizedCoordinates);
        return samplerCreateInfo;
    }

    inline VkImageViewCreateInfo ImageViewCreateInfo(VkImage image, EImageViewType viewType, EFormat format,
                                                     UInt32 aspectMask, UInt32 baseMipLevel, UInt32 levelCount,
                                                     UInt32 baseArrayLayer, UInt32 layerCount)
    {
        VK_STRUCT_INITIALIZE(VkImageViewCreateInfo, viewInfo)
        viewInfo.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
        viewInfo.image = image;
        viewInfo.viewType = static_cast<VkImageViewType>(viewType);
        viewInfo.format = static_cast<VkFormat>(format);
        viewInfo.subresourceRange.aspectMask = aspectMask;
        viewInfo.subresourceRange.baseMipLevel = baseMipLevel;
        viewInfo.subresourceRange.levelCount = levelCount;
        viewInfo.subresourceRange.baseArrayLayer = baseArrayLayer;
        viewInfo.subresourceRange.layerCount = layerCount;
        return viewInfo;
    }
}

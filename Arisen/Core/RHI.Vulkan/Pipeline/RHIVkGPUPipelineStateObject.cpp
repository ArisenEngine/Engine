
#include "Pipeline/RHIVkGPUPipelineStateObject.h"
#include "Pipeline/RHIVkGPUPipeline.h"
#include <vulkan/vulkan.h>
#include <cstring>
#include <vector>
#include "Core/RHIVkDevice.h"
#include "Utils/RHIVkInitializer.h"
#include "Descriptors/RHIVkBindlessManager.h"
#include "Handles/RHIVkResourcePools.h"

ArisenEngine::RHI::RHIVkGPUPipelineStateObject::~RHIVkGPUPipelineStateObject() noexcept
{
    LOG_DEBUG("[RHIVkGPUPipelineStateObject::~RHIVkGPUPipelineStateObject]: ~RHIVkGPUPipelineStateObject");
    Clear();
}

ArisenEngine::RHI::RHIVkGPUPipelineStateObject::RHIVkGPUPipelineStateObject(RHIVkDevice* device): RHIPipelineState(), m_Device(device)
{
    LOG_DEBUG("[RHIVkGPUPipelineStateObject::RHIVkGPUPipelineStateObject]: PSO Create.");
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::AddProgram(RHIShaderProgramHandle handle)
{
    auto* item = m_Device->GetGPUProgramPool()->Get(handle);
    if (!item || !item->program)
    {
        LOG_ERROR("[RHIVkGPUPipelineStateObject::AddProgram]: Invalid handle or program not found.");
        return;
    }
    auto* program = item->program;

    VkPipelineShaderStageCreateInfo shaderStageCreateInfo {};
    shaderStageCreateInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
    shaderStageCreateInfo.flags = program->GetShaderStageCreateFlags();
    shaderStageCreateInfo.stage = static_cast<VkShaderStageFlagBits>(program->GetShaderState());
    shaderStageCreateInfo.module = static_cast<VkShaderModule>(program->GetHandle());
    shaderStageCreateInfo.pName = program->GetEntry();
    shaderStageCreateInfo.pSpecializationInfo = static_cast<const VkSpecializationInfo*>(program->GetSpecializationInfo());
    auto it = m_PipelineStageCreateInfos.begin();
    for(;it != m_PipelineStageCreateInfos.end(); ++it)
    {
        if (it->stage == shaderStageCreateInfo.stage)
        {
            LOG_ERROR("[RHIVkGPUPipeline::AddProgram]: pipeline stage duplicated, shader name:  " + program->GetName());
            continue;
        }

        if (it->stage > shaderStageCreateInfo.stage)
        {
            break;
        }
    }

    m_PipelineStageCreateInfos.insert(it, shaderStageCreateInfo);

    // Merge Reflection Data
    auto vkProgram = static_cast<RHIVkGPUProgram*>(program);
    const auto& reflectionData = vkProgram->GetReflectionData();
    
    for (const auto& binding : reflectionData.ResourceBindings)
    {
        InternalAddDescriptorSetLayoutBinding(
            binding.Set,
            binding.Binding,
            binding.DescriptorType,
            binding.Count,
            binding.StageFlags
        );
    }

    // Merge Push Constants
    for (const auto& pc : reflectionData.PushConstants)
    {
        bool found = false;
        for (auto& existingPC : m_PushConstantRanges)
        {
            if (existingPC.offset == pc.Offset && existingPC.size == pc.Size)
            {
                existingPC.stageFlags |= pc.StageFlags;
                found = true;
                break;
            }
        }

        if (!found)
        {
            VkPushConstantRange range{};
            range.offset = pc.Offset;
            range.size = pc.Size;
            range.stageFlags = pc.StageFlags;
            m_PushConstantRanges.emplace_back(range);
        }
    }
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::ClearAllPrograms()
{
    m_PipelineStageCreateInfos.clear();
}

const ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetHash() const 
{
    UInt32 hash = 2166136261u;
    const UInt32 prime = 16777619u;

    auto hashCombine = [&](UInt32 value) {
        hash ^= value;
        hash *= prime;
    };

    // Hash Shader Modules
    for (const auto& stage : m_PipelineStageCreateInfos) {
        hashCombine(static_cast<UInt32>(reinterpret_cast<uintptr_t>(stage.module)));
        hashCombine(static_cast<UInt32>(stage.stage));
    }

    // Hash Blend State
    for (const auto& attachment : m_BlendAttachmentStates) {
        hashCombine(attachment.blendEnable);
        hashCombine(attachment.colorWriteMask);
        if (attachment.blendEnable) {
             hashCombine(attachment.srcColorBlendFactor);
             hashCombine(attachment.dstColorBlendFactor);
             hashCombine(attachment.colorBlendOp);
             hashCombine(attachment.srcAlphaBlendFactor);
             hashCombine(attachment.dstAlphaBlendFactor);
             hashCombine(attachment.alphaBlendOp);
        }
    }
    
    // Hash Vertex Bindings
    hashCombine(static_cast<UInt32>(m_VertexInputBindingDescriptions.size()));
    for (const auto& desc : m_VertexInputBindingDescriptions) {
        hashCombine(desc.binding);
        hashCombine(desc.stride);
    }
    
    // Hash Attachments
    hashCombine(static_cast<UInt32>(m_ColorAttachmentFormats.size()));
    for (const auto& fmt : m_ColorAttachmentFormats) {
        hashCombine(static_cast<UInt32>(fmt));
    }
    hashCombine(static_cast<UInt32>(m_DepthAttachmentFormat));
    hashCombine(static_cast<UInt32>(m_StencilAttachmentFormat));

    return hash;
}

bool ArisenEngine::RHI::RHIVkGPUPipelineStateObject::IsMeshPipeline() const
{
    for (const auto& stage : m_PipelineStageCreateInfos)
    {
        if (stage.stage == VK_SHADER_STAGE_MESH_BIT_EXT || stage.stage == VK_SHADER_STAGE_TASK_BIT_EXT)
        {
            return true;
        }
    }
    return false;
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::Clear()
{
    ClearAllPrograms();
    ClearBlendState();
    ClearVertexBindingDescriptions();
    ClearVertexInputAttributeDescriptions();
    ClearDynamicPipelineStates();
    ClearDescriptorSetLayoutBindings();
    ClearDescriptorSetLayouts();
    m_PushConstantRanges.clear();
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::AddVertexBindingDescription(UInt32 binding, UInt32 stride,
    EVertexInputRate inputRate)
{
    m_VertexInputBindingDescriptions.emplace_back(VkVertexInputBindingDescription{binding, stride,static_cast<VkVertexInputRate>(inputRate)});
}

void* ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetVertexBindingDescriptions()
{
    return m_VertexInputBindingDescriptions.data();
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::ClearVertexInputAttributeDescriptions()
{
    m_VertexInputAttributeDescriptions.clear();
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetStageCount()
{
    return static_cast<UInt32>(m_PipelineStageCreateInfos.size());
}

void* ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetVertexInputAttributeDescriptions()
{
    return m_VertexInputAttributeDescriptions.data();
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::ClearVertexBindingDescriptions()
{
    m_VertexInputBindingDescriptions.clear();
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetVertexInputAttributeDescriptionCount()
{
    return static_cast<UInt32>(m_VertexInputAttributeDescriptions.size());
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::AddVertexInputAttributeDescription(UInt32 location, UInt32 binding,
    EFormat format, UInt32 offset)
{
    m_VertexInputAttributeDescriptions.emplace_back(VkVertexInputAttributeDescription{location, binding, static_cast<VkFormat>(format), offset});
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetVertexBindingDescriptionCount()
{
    return static_cast<UInt32>(m_VertexInputBindingDescriptions.size());
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::AddBlendAttachmentState(bool enable, EBlendFactor srcColor,
                                                                             EBlendFactor dstColor, EBlendOp colorBlendOp,
                                                                             EBlendFactor srcAlpha, EBlendFactor dstAlpha, EBlendOp alphaBlendOp,
                                                                             UInt32 writeMask)
{
    VkPipelineColorBlendAttachmentState blendState;
    blendState.blendEnable = static_cast<VkBool32>(enable);
    blendState.srcColorBlendFactor = static_cast<VkBlendFactor>(srcColor);
    blendState.dstColorBlendFactor = static_cast<VkBlendFactor>(dstColor);
    blendState.colorBlendOp = static_cast<VkBlendOp>(colorBlendOp);
    blendState.srcAlphaBlendFactor = static_cast<VkBlendFactor>(srcAlpha);
    blendState.dstAlphaBlendFactor = static_cast<VkBlendFactor>(dstAlpha);
    blendState.alphaBlendOp = static_cast<VkBlendOp>(alphaBlendOp);
    blendState.colorWriteMask = writeMask;
    m_BlendAttachmentStates.emplace_back(blendState);
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::AddBlendAttachmentState(bool enable, UInt32 writeMask)
{
    VkPipelineColorBlendAttachmentState blendState;
    blendState.blendEnable = static_cast<VkBool32>(enable);
    blendState.colorWriteMask = writeMask;

    blendState.srcColorBlendFactor = static_cast<VkBlendFactor>(EBlendFactor::BLEND_FACTOR_ONE);
    blendState.dstColorBlendFactor = static_cast<VkBlendFactor>(BLEND_FACTOR_ONE);
    blendState.colorBlendOp = static_cast<VkBlendOp>(EBlendOp::BLEND_OP_ADD);
    blendState.srcAlphaBlendFactor = static_cast<VkBlendFactor>(BLEND_FACTOR_ONE);
    blendState.dstAlphaBlendFactor = static_cast<VkBlendFactor>(BLEND_FACTOR_ONE);
    blendState.alphaBlendOp = static_cast<VkBlendOp>(BLEND_OP_ADD);
    
    m_BlendAttachmentStates.emplace_back(blendState);
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::ClearBlendState()
{
    m_BlendAttachmentStates.clear();
}

const ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetBlendStateCount() const
{
    return static_cast<UInt32>(m_BlendAttachmentStates.size());
}

void* ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetBlendAttachmentStates()
{
    return m_BlendAttachmentStates.data();
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::ClearDescriptorSetLayoutBindings()
{
    m_DescriptorSetLayoutBindings.clear();
}

// TODO: cache descriptor set layout
void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::BuildDescriptorSetLayout()
{
    ClearDescriptorSetLayouts();
    auto vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
    
    UInt32 maxSetIndex = 3; // Set 3 is Reserved for Bindless
    for (const auto& pair : m_DescriptorSetLayoutBindings)
    {
        if (pair.first > maxSetIndex) maxSetIndex = pair.first;
    }

    m_DescriptorSetLayouts.resize(maxSetIndex + 1, VK_NULL_HANDLE);
    VkDescriptorSetLayout bindlessLayout = m_Device->GetBindlessManager()->GetDescriptorSetLayout();

    for (UInt32 i = 0; i <= maxSetIndex; ++i)
    {
        if (i == 3)
        {
            m_DescriptorSetLayouts[i] = bindlessLayout;
            continue;
        }

        if (m_DescriptorSetLayoutBindings.contains(i))
        {
            const auto& bindings = m_DescriptorSetLayoutBindings[i];
            auto descriptorSetLayoutInfo = DescriptorSetLayoutCreateInfo(static_cast<uint32_t>(bindings.size()), bindings.data());
            VkDescriptorSetLayout descriptorSetLayout;
            if (vkCreateDescriptorSetLayout(vkDevice, &descriptorSetLayoutInfo, nullptr, &descriptorSetLayout) != VK_SUCCESS)
            {
                LOG_FATAL_AND_THROW("[RHIVkGPUPipelineStateObject::BuildDescriptorSetLayout]: failed to create descriptor set layout!");
            }
            m_DescriptorSetLayouts[i] = descriptorSetLayout;
        }
        else
        {
            auto descriptorSetLayoutInfo = DescriptorSetLayoutCreateInfo(0, nullptr);
            VkDescriptorSetLayout descriptorSetLayout;
            if (vkCreateDescriptorSetLayout(vkDevice, &descriptorSetLayoutInfo, nullptr, &descriptorSetLayout) != VK_SUCCESS)
            {
                LOG_FATAL_AND_THROW("[RHIVkGPUPipelineStateObject::BuildDescriptorSetLayout]: failed to create empty descriptor set layout!");
            }
            m_DescriptorSetLayouts[i] = descriptorSetLayout;
        }
    }
}

VkDescriptorSetLayout ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetVkDescriptorSetLayout(UInt32 layoutIndex) const
{
    // NOTE: layoutIndex is a logical set index, not "nth element in map".
    // We keep descriptor set layouts in a vector; validate against that.
    if (layoutIndex >= m_DescriptorSetLayouts.size())
    {
        LOG_FATAL_AND_THROW("[RHIVkGPUPipelineStateObject::GetVkDescriptorSetLayout] layout index out of range: " + std::to_string(layoutIndex));
    }
    return m_DescriptorSetLayouts[layoutIndex];
}

const ArisenEngine::Containers::
Map<ArisenEngine::UInt32,ArisenEngine::Containers::
UnorderedMap<ArisenEngine::RHI::EDescriptorType, ArisenEngine::RHI::RHIDescriptorUpdateInfo>>&
ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetDescriptorUpdateInfos(
    UInt32 layoutIndex) const
{
    if (m_DescriptorUpdateInfos.contains(layoutIndex))
    {
        return m_DescriptorUpdateInfos.at(layoutIndex);
    }

    LOG_FATAL_AND_THROW("[RHIVkGPUPipelineStateObject::GetDescriptorUpdateInfos] layout index: " + std::to_string(layoutIndex) + " is not exist.");
    
    // Satisfy compiler warning; execution will not reach here.
    static const ArisenEngine::Containers::Map<ArisenEngine::UInt32, ArisenEngine::Containers::UnorderedMap<ArisenEngine::RHI::EDescriptorType, ArisenEngine::RHI::RHIDescriptorUpdateInfo>> dummy;
    return dummy;
}


void* ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetDescriptorSetLayouts()
{
    return m_DescriptorSetLayouts.data();
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkGPUPipelineStateObject::DescriptorSetLayoutCount()
{
    return m_DescriptorSetLayouts.size();
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::ClearDescriptorSetLayouts()
{
    auto vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
    VkDescriptorSetLayout bindlessLayout = m_Device->GetBindlessManager()->GetDescriptorSetLayout();

    for (const auto& descriptorSetLayout : m_DescriptorSetLayouts)
    {
        if (descriptorSetLayout != VK_NULL_HANDLE && descriptorSetLayout != bindlessLayout)
        {
            vkDestroyDescriptorSetLayout(vkDevice, descriptorSetLayout, nullptr);
        }
    }
    m_DescriptorSetLayouts.clear();
}



void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::InternalAddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding,
    EDescriptorType type, UInt32 descriptorCount, UInt32 shaderStageFlags)
{
    auto descriptorSetLayoutBinding = DescriptorSetLayoutBinding(binding,
        static_cast<VkDescriptorType>(type), descriptorCount, shaderStageFlags,
        nullptr);
    if (m_DescriptorSetLayoutBindings.contains(layoutIndex))
    {
        auto& bindings = m_DescriptorSetLayoutBindings[layoutIndex];
        bool found = false;
        for (auto& existingBinding : bindings)
        {
            if (existingBinding.binding == binding)
            {
                // Verify compatibility
                if (existingBinding.descriptorType != static_cast<VkDescriptorType>(type) ||
                    existingBinding.descriptorCount != descriptorCount)
                {
                    LOG_ERROR("[RHIVkGPUPipelineStateObject::InternalAddDescriptorSetLayoutBinding]: Binding conflict at Set " 
                        + std::to_string(layoutIndex) + " Binding " + std::to_string(binding));
                    // Depending on severity, we might want to throw or return. For now, log error.
                }
                
                // Merge stages
                existingBinding.stageFlags |= shaderStageFlags;
                found = true;
                break;
            }
        }

        if (!found)
        {
            bindings.emplace_back(descriptorSetLayoutBinding);
        }
    }
    else
    {
        Containers::Vector<VkDescriptorSetLayoutBinding> bindings { descriptorSetLayoutBinding };
        m_DescriptorSetLayoutBindings.try_emplace(layoutIndex, bindings);
    }
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::InternalAddDescriptorUpdateInfo(UInt32 layoutIndex, UInt32 binding,EDescriptorType type,
            UInt32 descriptorCount, const Containers::Vector<RHIDescriptorImageInfo>&& imageInfos,
            const Containers::Vector<RHIBufferHandle>&& bufferHandles, const Containers::Vector<RHIImageViewHandle>&& bufferViews)
{
    if (!m_DescriptorUpdateInfos.contains(layoutIndex))
    {
        m_DescriptorUpdateInfos.try_emplace(layoutIndex);
    }

    if (!m_DescriptorUpdateInfos[layoutIndex].contains(binding))
    {
        m_DescriptorUpdateInfos[layoutIndex].try_emplace(binding);
    }

    m_DescriptorUpdateInfos[layoutIndex][binding].insert_or_assign(type,
        RHIDescriptorUpdateInfo {
            binding,
            type,
            descriptorCount,
            imageInfos,
            bufferHandles,
            bufferViews,
        });
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding,
    const Containers::Vector<RHIDescriptorImageInfo>&& imageInfos)
{
    if (!m_DescriptorSetLayoutBindings.contains(layoutIndex)) return;
    
    EDescriptorType type = EDescriptorType::DESCRIPTOR_TYPE_MAX_ENUM;
    UInt32 count = 0;
    
    for (const auto& b : m_DescriptorSetLayoutBindings[layoutIndex])
    {
        if (b.binding == binding)
        {
            type = static_cast<EDescriptorType>(b.descriptorType);
            count = b.descriptorCount;
            break;
        }
    }
    
    if (type != EDescriptorType::DESCRIPTOR_TYPE_MAX_ENUM)
    {
        InternalAddDescriptorUpdateInfo(layoutIndex, binding, type, count, std::move(imageInfos), {}, {});
    }
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding,
    const Containers::Vector<RHIBufferHandle>&& bufferHandles)
{
    if (!m_DescriptorSetLayoutBindings.contains(layoutIndex)) return;
    
    EDescriptorType type = EDescriptorType::DESCRIPTOR_TYPE_MAX_ENUM;
    UInt32 count = 0;
    
    for (const auto& b : m_DescriptorSetLayoutBindings[layoutIndex])
    {
        if (b.binding == binding)
        {
            type = static_cast<EDescriptorType>(b.descriptorType);
            count = b.descriptorCount;
            break;
        }
    }
    
    if (type != EDescriptorType::DESCRIPTOR_TYPE_MAX_ENUM)
    {
        InternalAddDescriptorUpdateInfo(layoutIndex, binding, type, count, {}, std::move(bufferHandles), {});
    }
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding,
    const Containers::Vector<RHIImageViewHandle>&& texelBufferViews)
{
     if (!m_DescriptorSetLayoutBindings.contains(layoutIndex)) return;
    
    EDescriptorType type = EDescriptorType::DESCRIPTOR_TYPE_MAX_ENUM;
    UInt32 count = 0;
    
    for (const auto& b : m_DescriptorSetLayoutBindings[layoutIndex])
    {
        if (b.binding == binding)
        {
            type = static_cast<EDescriptorType>(b.descriptorType);
            count = b.descriptorCount;
            break;
        }
    }
    
    if (type != EDescriptorType::DESCRIPTOR_TYPE_MAX_ENUM)
    {
        InternalAddDescriptorUpdateInfo(layoutIndex, binding, type, count, {}, {}, std::move(texelBufferViews));
    }
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::SetRenderingFormats(const Containers::Vector<EFormat>& colorFormats,
    EFormat depthFormat, EFormat stencilFormat)
{
    m_ColorAttachmentFormats.clear();
    for (const auto format : colorFormats)
    {
        m_ColorAttachmentFormats.emplace_back(static_cast<VkFormat>(format));
    }

    if (depthFormat != EFormat::FORMAT_UNDEFINED)
    {
        m_DepthAttachmentFormat = static_cast<VkFormat>(depthFormat);
    }

    if (stencilFormat != EFormat::FORMAT_UNDEFINED)
    {
        m_StencilAttachmentFormat = static_cast<VkFormat>(stencilFormat);
    }
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::FillRenderingCreateInfo(VkPipelineRenderingCreateInfoKHR& createInfo) const
{
    createInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_RENDERING_CREATE_INFO_KHR;
    createInfo.pNext = nullptr;
    createInfo.colorAttachmentCount = static_cast<uint32_t>(m_ColorAttachmentFormats.size());
    createInfo.pColorAttachmentFormats = m_ColorAttachmentFormats.data();
    createInfo.depthAttachmentFormat = m_DepthAttachmentFormat;
    createInfo.stencilAttachmentFormat = m_StencilAttachmentFormat;
}





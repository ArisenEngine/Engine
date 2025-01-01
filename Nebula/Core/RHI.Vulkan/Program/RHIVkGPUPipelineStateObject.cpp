
#include "RHIVkGPUPipelineStateObject.h"
#include "RHIVkGPUPipeline.h"
#include "../Devices/RHIVkDevice.h"
#include "../VkInitializer.h"

NebulaEngine::RHI::RHIVkGPUPipelineStateObject::~RHIVkGPUPipelineStateObject() noexcept
{
    LOG_DEBUG("[RHIVkGPUPipelineStateObject::~RHIVkGPUPipelineStateObject]: ~RHIVkGPUPipelineStateObject");
    Clear();
}

NebulaEngine::RHI::RHIVkGPUPipelineStateObject::RHIVkGPUPipelineStateObject(RHIVkDevice* device): GPUPipelineStateObject(), m_Device(device)
{
    LOG_DEBUG("[RHIVkGPUPipelineStateObject::RHIVkGPUPipelineStateObject]: PSO Create.");
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::AddProgram(u32 programId)
{
    auto program = m_Device->GetGPUProgram(programId);
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
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::ClearAllPrograms()
{
    m_PipelineStageCreateInfos.clear();
}

const NebulaEngine::u32 NebulaEngine::RHI::RHIVkGPUPipelineStateObject::GetHash() const 
{
    // TODO
    return 0;
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::Clear()
{
    ClearAllPrograms();
    ClearBlendState();
    ClearVertexBindingDescriptions();
    ClearVertexInputAttributeDescriptions();
    ClearDynamicPipelineStates();
    ClearDescriptorSetLayoutBindings();
    ClearDescriptorSetLayouts();
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::AddVertexBindingDescription(u32 binding, u32 stride,
    EVertexInputRate inputRate)
{
    m_VertexInputBindingDescriptions.emplace_back(VkVertexInputBindingDescription{binding, stride,static_cast<VkVertexInputRate>(inputRate)});
}

void* NebulaEngine::RHI::RHIVkGPUPipelineStateObject::GetVertexBindingDescriptions()
{
    return m_VertexInputBindingDescriptions.data();
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::ClearVertexInputAttributeDescriptions()
{
    m_VertexInputAttributeDescriptions.clear();
}

NebulaEngine::u32 NebulaEngine::RHI::RHIVkGPUPipelineStateObject::GetStageCount()
{
    return static_cast<u32>(m_PipelineStageCreateInfos.size());
}

void* NebulaEngine::RHI::RHIVkGPUPipelineStateObject::GetVertexInputAttributeDescriptions()
{
    return m_VertexInputAttributeDescriptions.data();
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::ClearVertexBindingDescriptions()
{
    m_VertexInputBindingDescriptions.clear();
}

NebulaEngine::u32 NebulaEngine::RHI::RHIVkGPUPipelineStateObject::GetVertexInputAttributeDescriptionCount()
{
    return static_cast<u32>(m_VertexInputAttributeDescriptions.size());
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::AddVertexInputAttributeDescription(u32 location, u32 binding,
    Format format, u32 offset)
{
    m_VertexInputAttributeDescriptions.emplace_back(VkVertexInputAttributeDescription{location, binding, static_cast<VkFormat>(format), offset});
}

NebulaEngine::u32 NebulaEngine::RHI::RHIVkGPUPipelineStateObject::GetVertexBindingDescriptionCount()
{
    return static_cast<u32>(m_VertexInputBindingDescriptions.size());
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::AddBlendAttachmentState(bool enable, EBlendFactor srcColor,
                                                                             EBlendFactor dstColor, EBlendOp colorBlendOp,
                                                                             EBlendFactor srcAlpha, EBlendFactor dstAlpha, EBlendOp alphaBlendOp,
                                                                             u32 writeMask)
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

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::AddBlendAttachmentState(bool enable, u32 writeMask)
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

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::ClearBlendState()
{
    m_BlendAttachmentStates.clear();
}

const NebulaEngine::u32 NebulaEngine::RHI::RHIVkGPUPipelineStateObject::GetBlendStateCount() const
{
    return static_cast<u32>(m_BlendAttachmentStates.size());
}

void* NebulaEngine::RHI::RHIVkGPUPipelineStateObject::GetBlendAttachmentStates()
{
    return m_BlendAttachmentStates.data();
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::ClearDescriptorSetLayoutBindings()
{
    m_DescriptorSetLayoutBindings.clear();
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::BuildDescriptorSetLayout()
{
    ClearDescriptorSetLayouts();
    auto vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
    for (const auto& pair : m_DescriptorSetLayoutBindings)
    {
        auto descriptorSetLayoutInfo = DescriptorSetLayoutCreateInfo(pair.second.size(), pair.second.data());
        VkDescriptorSetLayout descriptorSetLayout;
        if (vkCreateDescriptorSetLayout(vkDevice, &descriptorSetLayoutInfo, nullptr, &descriptorSetLayout) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkGPUPipelineStateObject::BuildDescriptorSetLayout]: failed to create descriptor set layout!");
        }
        m_DescriptorSetLayouts.emplace_back(descriptorSetLayout);
    }
    
}

void* NebulaEngine::RHI::RHIVkGPUPipelineStateObject::GetDescriptorSetLayouts()
{
    return m_DescriptorSetLayouts.data();
}

NebulaEngine::u32 NebulaEngine::RHI::RHIVkGPUPipelineStateObject::DescriptorSetLayoutCount()
{
    return m_DescriptorSetLayouts.size();
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::ClearDescriptorSetLayouts()
{
    auto vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
    for (const auto& descriptorSetLayout : m_DescriptorSetLayouts)
    {
        vkDestroyDescriptorSetLayout(vkDevice, descriptorSetLayout, nullptr);
    }
    m_DescriptorSetLayouts.clear();
}

void NebulaEngine::RHI::RHIVkGPUPipelineStateObject::AddDescriptorSetLayoutBinding(u32 layoutIndex, u32 binding,
                                                                                   EDescriptorType type, u32 descriptorCount, u32 shaderStageFlags, void* pImmutableSamplers)
{
    auto descriptorSetLayoutBinding = DescriptorSetLayoutBinding(binding,
        static_cast<VkDescriptorType>(type), descriptorCount, shaderStageFlags,
        static_cast<const VkSampler*>(pImmutableSamplers));
    if (m_DescriptorSetLayoutBindings.contains(layoutIndex))
    {
        m_DescriptorSetLayoutBindings[layoutIndex].emplace_back(descriptorSetLayoutBinding);
    }
    else
    {
        Containers::Vector<VkDescriptorSetLayoutBinding> bindings { descriptorSetLayoutBinding };
        m_DescriptorSetLayoutBindings.try_emplace(layoutIndex, bindings);
    }
}

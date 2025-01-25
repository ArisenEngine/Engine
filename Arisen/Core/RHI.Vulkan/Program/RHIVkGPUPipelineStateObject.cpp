
#include "RHIVkGPUPipelineStateObject.h"
#include "RHIVkGPUPipeline.h"
#include "../Devices/RHIVkDevice.h"
#include "../VkInitializer.h"

ArisenEngine::RHI::RHIVkGPUPipelineStateObject::~RHIVkGPUPipelineStateObject() noexcept
{
    LOG_DEBUG("[RHIVkGPUPipelineStateObject::~RHIVkGPUPipelineStateObject]: ~RHIVkGPUPipelineStateObject");
    Clear();
}

ArisenEngine::RHI::RHIVkGPUPipelineStateObject::RHIVkGPUPipelineStateObject(RHIVkDevice* device): GPUPipelineStateObject(), m_Device(device)
{
    LOG_DEBUG("[RHIVkGPUPipelineStateObject::RHIVkGPUPipelineStateObject]: PSO Create.");
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::AddProgram(UInt32 programId)
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

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::ClearAllPrograms()
{
    m_PipelineStageCreateInfos.clear();
}

const ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetHash() const 
{
    // TODO
    return 0;
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

VkDescriptorSetLayout ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetVkDescriptorSetLayout(UInt32 layoutIndex) const
{
    ASSERT(layoutIndex < m_DescriptorSetLayoutBindings.size());
    return m_DescriptorSetLayouts[layoutIndex];
}

ArisenEngine::Containers::
Map<ArisenEngine::UInt32,ArisenEngine::Containers::
UnorderedMap<ArisenEngine::RHI::EDescriptorType, ArisenEngine::RHI::RHIDescriptorUpdateInfo>>
ArisenEngine::RHI::RHIVkGPUPipelineStateObject::GetDescriptorUpdateInfos(
    UInt32 layoutIndex) const
{
    if (m_DescriptorUpdateInfos.contains(layoutIndex))
    {
        return m_DescriptorUpdateInfos.at(layoutIndex);
    }

    LOG_FATAL_AND_THROW("[RHIVkGPUPipelineStateObject::GetDescriptorUpdateInfos] layout index: " + std::to_string(layoutIndex) + " is not exist.");
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
    for (const auto& descriptorSetLayout : m_DescriptorSetLayouts)
    {
        vkDestroyDescriptorSetLayout(vkDevice, descriptorSetLayout, nullptr);
    }
    m_DescriptorSetLayouts.clear();
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::AddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding, EDescriptorType type,
            UInt32 descriptorCount, UInt32 shaderStageFlags, const Containers::Vector<RHIDescriptorImageInfo>&& imageInfos,
             ImmutableSamplers* pImmutableSamplers)
{
    ASSERT(descriptorCount == imageInfos.size());
    
    InternalAddDescriptorSetLayoutBinding(layoutIndex, binding, type, descriptorCount, shaderStageFlags, pImmutableSamplers);
    InternalAddDescriptorUpdateInfo(layoutIndex, binding, type, descriptorCount, std::move(imageInfos),
        {}, {});
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::AddDescriptorSetLayoutBinding(
UInt32 layoutIndex, UInt32 binding, EDescriptorType type,
                                               UInt32 descriptorCount, UInt32 shaderStageFlags,
                                               const Containers::Vector<std::shared_ptr<BufferHandle>>&& bufferHandles)
{

    ASSERT(descriptorCount == bufferHandles.size());
    InternalAddDescriptorSetLayoutBinding(layoutIndex, binding, type, descriptorCount, shaderStageFlags, nullptr);
    InternalAddDescriptorUpdateInfo(layoutIndex, binding, type, descriptorCount, {},
      std::move(bufferHandles), {});
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::AddDescriptorSetLayoutBinding(
UInt32 layoutIndex, UInt32 binding, EDescriptorType type,
                                              UInt32 descriptorCount, UInt32 shaderStageFlags,
                                              const Containers::Vector<BufferView*>&& bufferViews)
{
    ASSERT(descriptorCount == bufferViews.size());
    InternalAddDescriptorSetLayoutBinding(layoutIndex, binding, type, descriptorCount, shaderStageFlags, nullptr);
    InternalAddDescriptorUpdateInfo(layoutIndex, binding, type, descriptorCount, {},
       {}, std::move(bufferViews));
}

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::InternalAddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding,
    EDescriptorType type, UInt32 descriptorCount, UInt32 shaderStageFlags, ImmutableSamplers* pImmutableSamplers)
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

void ArisenEngine::RHI::RHIVkGPUPipelineStateObject::InternalAddDescriptorUpdateInfo(UInt32 layoutIndex, UInt32 binding,EDescriptorType type,
            UInt32 descriptorCount, const Containers::Vector<RHIDescriptorImageInfo>&& imageInfos,
            const Containers::Vector<std::shared_ptr<BufferHandle>>&& bufferHandles, const Containers::Vector<BufferView*>&& bufferViews)
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

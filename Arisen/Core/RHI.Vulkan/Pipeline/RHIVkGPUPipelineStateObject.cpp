
#include "Pipeline/RHIVkGPUPipelineStateObject.h"
#include "Pipeline/RHIVkGPUPipeline.h"
#include <vulkan/vulkan.h>
#include <cstring>
#include <vector>
#include "Core/RHIVkDevice.h"
#include "Utils/RHIVkInitializer.h"
#include "Descriptors/RHIVkBindlessManager.h"
#include "Handles/RHIVkResourcePools.h"

namespace ArisenEngine::RHI
{

RHIVkGPUPipelineStateObject::~RHIVkGPUPipelineStateObject() noexcept
{
    LOG_DEBUG("[RHIVkGPUPipelineStateObject::~RHIVkGPUPipelineStateObject]: ~RHIVkGPUPipelineStateObject");
    Clear();
}

RHIVkGPUPipelineStateObject::RHIVkGPUPipelineStateObject(RHIVkDevice* device): RHIPipelineState(), m_Device(device)
{
    LOG_DEBUG("[RHIVkGPUPipelineStateObject::RHIVkGPUPipelineStateObject]: PSO Create.");
}

void RHIVkGPUPipelineStateObject::AddProgram(RHIShaderProgramHandle handle)
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

void RHIVkGPUPipelineStateObject::ClearAllPrograms()
{
    m_PipelineStageCreateInfos.clear();
}

const UInt32 RHIVkGPUPipelineStateObject::GetHash() const
{
    // Generate a hash based on unique pipeline identity
    // Each PSO instance should get a unique hash based on its address
    // This ensures compute and graphics pipelines get distinct cache entries
    return static_cast<UInt32>(reinterpret_cast<uintptr_t>(this) & 0xFFFFFFFF);
}

bool RHIVkGPUPipelineStateObject::IsMeshPipeline() const
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

void RHIVkGPUPipelineStateObject::Clear()
{
    ClearAllPrograms();
    ClearVertexInputDescriptions();
    ClearDescriptorSetLayouts();
    m_PushConstantRanges.clear();
    m_ColorAttachmentFormats.clear();
    m_DepthAttachmentFormat = VK_FORMAT_UNDEFINED;
    m_StencilAttachmentFormat = VK_FORMAT_UNDEFINED;
    SetDynamicStateMask(0);
}

void RHIVkGPUPipelineStateObject::ClearVertexInputDescriptions()
{
    m_VertexInputBindingDescriptions.clear();
    m_VertexInputAttributeDescriptions.clear();
}

void RHIVkGPUPipelineStateObject::AddVertexBindingDescription(UInt32 binding, UInt32 stride,
    EVertexInputRate inputRate)
{
    VkVertexInputBindingDescription bindingDescription {};
    bindingDescription.binding = binding;
    bindingDescription.stride = stride;
    bindingDescription.inputRate = static_cast<VkVertexInputRate>(inputRate);

    m_VertexInputBindingDescriptions.emplace_back(bindingDescription);
}

void RHIVkGPUPipelineStateObject::AddVertexInputAttributeDescription(UInt32 location, UInt32 binding,
    EFormat format, UInt32 offset)
{
    VkVertexInputAttributeDescription attributeDescription {};
    attributeDescription.binding = binding;
    attributeDescription.location = location;
    attributeDescription.format = static_cast<VkFormat>(format);
    attributeDescription.offset = offset;

    m_VertexInputAttributeDescriptions.emplace_back(attributeDescription);
}

void RHIVkGPUPipelineStateObject::ClearDescriptorSetLayoutBindings()
{
    m_DescriptorSetLayoutBindings.clear();
}

// TODO: cache descriptor set layout
void RHIVkGPUPipelineStateObject::BuildDescriptorSetLayout()
{
    ClearDescriptorSetLayouts();

    auto vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
    VkDescriptorSetLayout bindlessLayout = m_Device->GetBindlessManager()->GetDescriptorSetLayout();

    // Loop through all defined sets
    // We expect sets to be relatively contiguous
    UInt32 maxSet = 0;
    for (auto const& [set, items] : m_DescriptorSetLayoutBindings)
    {
        if (set > maxSet) maxSet = set;
    }

    m_DescriptorSetLayouts.resize((std::max)(maxSet + 1, 4u)); // Ensure at least 4 sets (0-2 common + 3 bindless)
    
    for (UInt32 i = 0; i < m_DescriptorSetLayouts.size(); ++i)
    {
        if (i == 3) // Set 3 is reserved for Bindless
        {
            m_DescriptorSetLayouts[i] = bindlessLayout;
            continue;
        }

        if (m_DescriptorSetLayoutBindings.contains(i))
        {
            const auto& bindings = m_DescriptorSetLayoutBindings[i];
            VkDescriptorSetLayoutCreateInfo layoutInfo{};
            layoutInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
            layoutInfo.bindingCount = static_cast<uint32_t>(bindings.size());
            layoutInfo.pBindings = bindings.data();

            if (vkCreateDescriptorSetLayout(vkDevice, &layoutInfo, nullptr, &m_DescriptorSetLayouts[i]) != VK_SUCCESS)
            {
                LOG_FATAL("[RHIVkGPUPipelineStateObject::BuildDescriptorSetLayout]: failed to create descriptor set layout!");
            }
        }
        else
        {
            // Create empty layout for holes
            VkDescriptorSetLayoutCreateInfo layoutInfo{};
            layoutInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
            layoutInfo.bindingCount = 0;
            layoutInfo.pNext = nullptr;

            if (vkCreateDescriptorSetLayout(vkDevice, &layoutInfo, nullptr, &m_DescriptorSetLayouts[i]) != VK_SUCCESS)
            {
                LOG_FATAL("[RHIVkGPUPipelineStateObject::BuildDescriptorSetLayout]: failed to create empty descriptor set layout!");
            }
        }
    }
}

VkDescriptorSetLayout RHIVkGPUPipelineStateObject::GetVkDescriptorSetLayout(UInt32 layoutIndex) const
{
    // NOTE: layoutIndex is a logical set index, not "nth element in map".
    // We keep descriptor set layouts in a vector; validate against that.
    if (layoutIndex >= m_DescriptorSetLayouts.size())
    {
        LOG_ERROR("[RHIVkGPUPipelineStateObject::GetVkDescriptorSetLayout] layout index out of range: " + std::to_string(layoutIndex));
        return VK_NULL_HANDLE;
    }
    return m_DescriptorSetLayouts[layoutIndex];
}

const Containers::Map<UInt32, Containers::UnorderedMap<EDescriptorType, RHIDescriptorUpdateInfo>>&
RHIVkGPUPipelineStateObject::GetDescriptorUpdateInfos(UInt32 layoutIndex) const
{
    static Containers::UnorderedMap<EDescriptorType, RHIDescriptorUpdateInfo> empty;
    static Containers::Map<UInt32, Containers::UnorderedMap<EDescriptorType, RHIDescriptorUpdateInfo>> emptyMap;
    
    if (m_DescriptorUpdateInfos.contains(layoutIndex))
    {
        return m_DescriptorUpdateInfos.at(layoutIndex);
    }
    return emptyMap;
}


const VkDescriptorSetLayout* RHIVkGPUPipelineStateObject::GetDescriptorSetLayouts() const
{
    return m_DescriptorSetLayouts.data();
}

UInt32 RHIVkGPUPipelineStateObject::GetDescriptorSetLayoutCount() const
{
    return static_cast<UInt32>(m_DescriptorSetLayouts.size());
}

void RHIVkGPUPipelineStateObject::ClearDescriptorSetLayouts()
{
    auto vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
    VkDescriptorSetLayout bindlessLayout = m_Device->GetBindlessManager()->GetDescriptorSetLayout();

    for (VkDescriptorSetLayout descriptorSetLayout : m_DescriptorSetLayouts)
    {
        if (descriptorSetLayout != VK_NULL_HANDLE && descriptorSetLayout != bindlessLayout)
        {
            ::vkDestroyDescriptorSetLayout(vkDevice, descriptorSetLayout, nullptr);
        }
    }
    m_DescriptorSetLayouts.clear();
}



void RHIVkGPUPipelineStateObject::InternalAddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding,
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

void RHIVkGPUPipelineStateObject::InternalAddDescriptorUpdateInfo(UInt32 layoutIndex, UInt32 binding, EDescriptorType type,
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

void RHIVkGPUPipelineStateObject::UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding,
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

void RHIVkGPUPipelineStateObject::UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding,
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

void RHIVkGPUPipelineStateObject::UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding,
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

void RHIVkGPUPipelineStateObject::SetRenderingFormats(const Containers::Vector<EFormat>& colorFormats,
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

void RHIVkGPUPipelineStateObject::FillRenderingCreateInfo(VkPipelineRenderingCreateInfoKHR& createInfo) const
{
    createInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_RENDERING_CREATE_INFO_KHR;
    createInfo.pNext = nullptr;
    createInfo.colorAttachmentCount = static_cast<uint32_t>(m_ColorAttachmentFormats.size());
    createInfo.pColorAttachmentFormats = m_ColorAttachmentFormats.data();
    createInfo.depthAttachmentFormat = m_DepthAttachmentFormat;
    createInfo.stencilAttachmentFormat = m_StencilAttachmentFormat;
}

} // namespace ArisenEngine::RHI

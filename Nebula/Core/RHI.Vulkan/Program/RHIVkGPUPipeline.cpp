#include "RHIVkGPUPipeline.h"
#include "../Devices/RHIVkDevice.h"

#include <complex.h>

#include "Logger/Logger.h"

/**
    Shader stages: the shader modules that define the functionality of the programmable stages of the graphics pipeline
    Fixed-function state: all of the structures that define the fixed-function stages of the pipeline, like input assembly, rasterizer, viewport and color blending
    Pipeline layout: the uniform and push values referenced by the shader that can be updated at draw time
    Render pass: the attachments referenced by the pipeline stages and their usage 
 */

NebulaEngine::RHI::RHIVkGPUPipeline::RHIVkGPUPipeline(RHIVkDevice* device): GPUPipeline(),
m_Device(device), m_VkDevice(static_cast<VkDevice>(device->GetHandle()))
{
    
}

NebulaEngine::RHI::RHIVkGPUPipeline::~RHIVkGPUPipeline() noexcept
{
    LOG_DEBUG("[RHIVkGPUPipeline::~RHIVkGPUPipeline]: ~RHIVkGPUPipeline");
    FreeGraphicsPipeline();
    FreeGraphicsPipelineLayout();
    m_PipelineStageCreateInfos.clear();
    m_VertexInputAttributeDescriptions.clear();
    m_VertexInputBindingDescriptions.clear();
}

void NebulaEngine::RHI::RHIVkGPUPipeline::AddProgram(u32 programId)
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

void NebulaEngine::RHI::RHIVkGPUPipeline::AllocGraphicPipeline()
{
    ASSERT(m_VkGraphicsPipelineLayout != VK_NULL_HANDLE);

    // TODO: caching
    FreeGraphicsPipeline();
    
    // Vertex input info
    VkPipelineVertexInputStateCreateInfo vertexInputInfo{};
    vertexInputInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO;
    vertexInputInfo.vertexBindingDescriptionCount = m_VertexInputBindingDescriptions.size();
    vertexInputInfo.vertexAttributeDescriptionCount = m_VertexInputAttributeDescriptions.size();
    vertexInputInfo.pVertexAttributeDescriptions = m_VertexInputAttributeDescriptions.data();
    vertexInputInfo.pVertexBindingDescriptions = m_VertexInputBindingDescriptions.data();

    // Input Assembly info
    VkPipelineInputAssemblyStateCreateInfo inputAssemblyInfo {};
    inputAssemblyInfo.topology = static_cast<VkPrimitiveTopology>(m_PrimitiveTopology);
    inputAssemblyInfo.primitiveRestartEnable = static_cast<VkBool32>(m_PrimitiveRestart);

    // viewport state
    
    VkGraphicsPipelineCreateInfo pipelineInfo{};
    pipelineInfo.sType = VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO;
    pipelineInfo.stageCount = m_PipelineStageCreateInfos.size();
    pipelineInfo.pStages = m_PipelineStageCreateInfos.data();
    pipelineInfo.pVertexInputState = &vertexInputInfo;
    pipelineInfo.pInputAssemblyState = &inputAssemblyInfo;
    pipelineInfo.pViewportState = &viewportState;
    pipelineInfo.pRasterizationState = &rasterizer;
    pipelineInfo.pMultisampleState = &multisampling;
    pipelineInfo.pColorBlendState = &colorBlending;
    pipelineInfo.pDynamicState = &dynamicState;
    pipelineInfo.layout = m_VkGraphicsPipelineLayout;
    pipelineInfo.renderPass = renderPass;
    pipelineInfo.subpass = 0;
    pipelineInfo.basePipelineHandle = VK_NULL_HANDLE;

    if (vkCreateGraphicsPipelines(m_VkDevice, VK_NULL_HANDLE, 1, &pipelineInfo, nullptr,
        &m_VkGraphicPipeline) != VK_SUCCESS) {
        LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocPipeline]: failed to create GPU pipeline!");
    }
}

void NebulaEngine::RHI::RHIVkGPUPipeline::AllocGraphicsPipelineLayout()
{
    VkPipelineLayoutCreateInfo pipelineLayoutInfo{};
    pipelineLayoutInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
    pipelineLayoutInfo.setLayoutCount = m_DescriptorSetLayouts.size();
    pipelineLayoutInfo.pSetLayouts = m_DescriptorSetLayouts.data();
    pipelineLayoutInfo.pushConstantRangeCount = m_PushConstantRanges.size();
    pipelineLayoutInfo.pPushConstantRanges = m_PushConstantRanges.data();

    if (vkCreatePipelineLayout(m_VkDevice, &pipelineLayoutInfo, nullptr, &m_VkGraphicsPipelineLayout) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocGraphicsPipelineLayout]: failed to create pipeline layout!");
    }
    
}


void NebulaEngine::RHI::RHIVkGPUPipeline::FreeGraphicsPipelineLayout()
{
    if (m_VkGraphicsPipelineLayout != VK_NULL_HANDLE)
    {
        vkDestroyPipelineLayout(m_VkDevice, m_VkGraphicsPipelineLayout, nullptr);
        m_VkGraphicsPipelineLayout = VK_NULL_HANDLE;
        LOG_DEBUG("## Destroy Vulkan Pipeline Layout ##");
    }

    m_DescriptorSetLayouts.clear();
    m_PushConstantRanges.clear();
}

void NebulaEngine::RHI::RHIVkGPUPipeline::FreeGraphicsPipeline()
{
    if (m_VkGraphicPipeline != VK_NULL_HANDLE)
    {
        vkDestroyPipeline(m_VkDevice, m_VkGraphicPipeline, nullptr);
    }
}

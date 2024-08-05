#include "RHIVkGPUPipeline.h"
#include "RHI/Program/GPUPipelineStateObject.h"
#include "../Devices/RHIVkDevice.h"

NebulaEngine::RHI::RHIVkGPUPipeline::~RHIVkGPUPipeline() noexcept
{
    LOG_DEBUG("[RHIVkGPUPipeline::~RHIVkGPUPipeline]: ~RHIVkGPUPipeline");

    FreePipeline();
    FreePipelineLayout();
    m_DescriptorSetLayouts.clear();
    m_PushConstantRanges.clear();

    m_Device = nullptr;
    m_PipelineStateObject = nullptr;
    m_SubPass = nullptr;
    
}

NebulaEngine::RHI::RHIVkGPUPipeline::RHIVkGPUPipeline(RHIVkDevice* device, GPUPipelineStateObject* pso):
GPUPipeline(), m_Device(device), m_VkDevice(static_cast<VkDevice>(device->GetHandle())), m_PipelineStateObject(pso)
{
    
}

void NebulaEngine::RHI::RHIVkGPUPipeline::AllocGraphicPipeline(GPUSubPass* subPass)
{
    FreePipeline();
    FreePipelineLayout();

    m_SubPass = subPass;
    ASSERT(m_PipelineStateObject != nullptr);
    {
        // Create Pipeline Layout
        VkPipelineLayoutCreateInfo pipelineLayoutInfo {};
        pipelineLayoutInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
        pipelineLayoutInfo.setLayoutCount = static_cast<uint32_t>(m_DescriptorSetLayouts.size());
        pipelineLayoutInfo.pSetLayouts = m_DescriptorSetLayouts.data();
        pipelineLayoutInfo.pushConstantRangeCount = static_cast<uint32_t>(m_PushConstantRanges.size());
        pipelineLayoutInfo.pPushConstantRanges = m_PushConstantRanges.data();

        if (vkCreatePipelineLayout(m_VkDevice, &pipelineLayoutInfo, nullptr, &m_VkGraphicsPipelineLayout) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocVkPipeline]: failed to create pipeline layout!");
        }
    }
    
    {
         // Vertex input info
        VkPipelineVertexInputStateCreateInfo vertexInputInfo {};
        vertexInputInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO;
        vertexInputInfo.vertexBindingDescriptionCount = m_PipelineStateObject->GetVertexBindingDescriptionCount();
        vertexInputInfo.pVertexBindingDescriptions = m_PipelineStateObject->GetVertexBindingDescriptionCount() > 0 ? 
            static_cast<const VkVertexInputBindingDescription*>(m_PipelineStateObject->GetVertexBindingDescriptions()) : nullptr;
        vertexInputInfo.vertexAttributeDescriptionCount = m_PipelineStateObject->GetVertexInputAttributeDescriptionCount();
        vertexInputInfo.pVertexAttributeDescriptions = m_PipelineStateObject->GetVertexInputAttributeDescriptionCount() > 0 ? 
            static_cast<const VkVertexInputAttributeDescription*>(m_PipelineStateObject->GetVertexInputAttributeDescriptions()) : nullptr;
        
        // Input Assembly info
        VkPipelineInputAssemblyStateCreateInfo inputAssemblyInfo {};
        inputAssemblyInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO;
        inputAssemblyInfo.topology = static_cast<VkPrimitiveTopology>(m_PipelineStateObject->GetTopology());
        inputAssemblyInfo.primitiveRestartEnable = static_cast<VkBool32>(m_PipelineStateObject->IsPrimitiveRestart());

        // viewport state
        VkPipelineViewportStateCreateInfo viewportStateInfo {};
        viewportStateInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO;
        if (m_PipelineStateObject->HasDynamicStates(EDynamicPipelineState::DYNAMIC_STATE_SCISSOR))
        {
            viewportStateInfo.pScissors = nullptr;
            viewportStateInfo.scissorCount = 1;
        }
        else
        {
            // TODO:
        }

        if (m_PipelineStateObject->HasDynamicStates(EDynamicPipelineState::DYNAMIC_STATE_VIEWPORT))
        {
            viewportStateInfo.pViewports = nullptr;
            viewportStateInfo.viewportCount = 1;
        }
        else
        {
            // TODO:
        }

        // rasterizer state
        VkPipelineRasterizationStateCreateInfo rasterizerInfo {};
        rasterizerInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO;
        rasterizerInfo.depthClampEnable = static_cast<VkBool32>(m_PipelineStateObject->DepthClampEnable());
        rasterizerInfo.rasterizerDiscardEnable = static_cast<VkBool32>(m_PipelineStateObject->RasterizerDiscardEnable());
        rasterizerInfo.polygonMode = static_cast<VkPolygonMode>(m_PipelineStateObject->PolygonMode());
        rasterizerInfo.lineWidth = m_PipelineStateObject->LineWidth();
        rasterizerInfo.cullMode = static_cast<VkCullModeFlagBits>(m_PipelineStateObject->CullMode());
        rasterizerInfo.frontFace = static_cast<VkFrontFace>(m_PipelineStateObject->FrontFace());
        rasterizerInfo.depthBiasEnable = static_cast<VkBool32>(m_PipelineStateObject->DepthBiasEnable());

        // multiple sample
        VkPipelineMultisampleStateCreateInfo multipleSampleInfo {};
        multipleSampleInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO;
        multipleSampleInfo.rasterizationSamples = static_cast<VkSampleCountFlagBits>(m_PipelineStateObject->SampleCount());
        multipleSampleInfo.sampleShadingEnable = static_cast<VkBool32>(m_PipelineStateObject->SampleShadingEnable());
        multipleSampleInfo.minSampleShading = m_PipelineStateObject->MinSampleShading();
        multipleSampleInfo.pSampleMask = reinterpret_cast<const VkSampleMask*>(m_PipelineStateObject->SampleMask());
        multipleSampleInfo.alphaToCoverageEnable = static_cast<VkBool32>(m_PipelineStateObject->AlphaToCoverage());
        multipleSampleInfo.alphaToOneEnable = static_cast<VkBool32>(m_PipelineStateObject->AlphaToOne());

        // blend state
        
        VkPipelineColorBlendStateCreateInfo blendState {};
        blendState.sType = VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO;
        blendState.attachmentCount = m_PipelineStateObject->GetBlendStateCount();
        blendState.pAttachments = static_cast<const VkPipelineColorBlendAttachmentState*>(m_PipelineStateObject->GetBlendAttachmentStates());
        blendState.logicOpEnable = static_cast<VkBool32>(m_PipelineStateObject->IsLogicOpEnable());
        blendState.logicOp = static_cast<VkLogicOp>(m_PipelineStateObject->LogicOp());
        blendState.blendConstants[0] = m_PipelineStateObject->BlendConstantR();
        blendState.blendConstants[1] = m_PipelineStateObject->BlendConstantG();
        blendState.blendConstants[2] = m_PipelineStateObject->BlendConstantB();
        blendState.blendConstants[3] = m_PipelineStateObject->BlendConstantA();

        // dynamic state
        VkPipelineDynamicStateCreateInfo dynamicStatesInfo {};
        Containers::Vector<VkDynamicState> dynamicStates;
         for (auto dynamicState : m_PipelineStateObject->GetDynamicStates())
         {
             dynamicStates.emplace_back(static_cast<VkDynamicState>(dynamicState));
         }
        dynamicStatesInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO;
        dynamicStatesInfo.dynamicStateCount = static_cast<uint32_t>(dynamicStates.size());
        dynamicStatesInfo.pDynamicStates = dynamicStates.data();
        
        auto stages = m_PipelineStateObject->GetStageCreateInfo();
        VkGraphicsPipelineCreateInfo pipelineInfo {};
        pipelineInfo.sType = VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO;
        pipelineInfo.stageCount = static_cast<uint32_t>(m_PipelineStateObject->GetStageCount());
        pipelineInfo.pStages = static_cast<const VkPipelineShaderStageCreateInfo*>(stages);
        pipelineInfo.pVertexInputState = &vertexInputInfo;
        pipelineInfo.pInputAssemblyState = &inputAssemblyInfo;
        pipelineInfo.pViewportState = &viewportStateInfo;
        pipelineInfo.pRasterizationState = &rasterizerInfo;
        pipelineInfo.pMultisampleState = &multipleSampleInfo;
        pipelineInfo.pColorBlendState = &blendState;
        pipelineInfo.pDynamicState = &dynamicStatesInfo;
        pipelineInfo.layout = m_VkGraphicsPipelineLayout;
        pipelineInfo.renderPass = static_cast<VkRenderPass>(subPass->GetOwner()->GetHandle());
        pipelineInfo.subpass = subPass->GetIndex();
        pipelineInfo.basePipelineHandle = VK_NULL_HANDLE;
        pipelineInfo.pNext = nullptr;

        if (vkCreateGraphicsPipelines(m_VkDevice, VK_NULL_HANDLE, 1, &pipelineInfo, nullptr, &m_VkGraphicPipeline) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocPipeline]: failed to create GPU pipeline!");
        }
    }
}

const NebulaEngine::RHI::EPipelineBindPoint NebulaEngine::RHI::RHIVkGPUPipeline::GetBindPoint() const
{
    ASSERT(m_SubPass != nullptr);
    return m_SubPass->GetBindPoint();
}

void NebulaEngine::RHI::RHIVkGPUPipeline::FreePipelineLayout()
{
    if (m_VkGraphicsPipelineLayout != VK_NULL_HANDLE)
    {
        vkDestroyPipelineLayout(m_VkDevice, m_VkGraphicsPipelineLayout, nullptr);
        LOG_DEBUG("## Destroy Vulkan Pipeline Layout ##");
        m_VkGraphicsPipelineLayout = VK_NULL_HANDLE;
    }
}

void NebulaEngine::RHI::RHIVkGPUPipeline::FreePipeline()
{
    if (m_VkGraphicPipeline != VK_NULL_HANDLE)
    {
        vkDestroyPipeline(m_VkDevice, m_VkGraphicPipeline, nullptr);
        LOG_DEBUG("## Destroy Vulkan Graphic Pipeline ##");
        m_VkGraphicPipeline = VK_NULL_HANDLE;
    }
}

void NebulaEngine::RHI::RHIVkGPUPipeline::BindPipelineStateObject(GPUPipelineStateObject* pso)
{
    m_PipelineStateObject = pso;
}



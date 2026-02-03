#include "Pipeline/RHIVkGPUPipeline.h"

#include "Pipeline/RHIVkGPUPipelineStateObject.h"
#include "RHI/Pipeline/RHIPipelineState.h"
#include "Core/RHIVkDevice.h"

ArisenEngine::RHI::RHIVkGPUPipeline::~RHIVkGPUPipeline() noexcept
{
    LOG_DEBUG("[RHIVkGPUPipeline::~RHIVkGPUPipeline]: ~RHIVkGPUPipeline");

    FreeAllPipelines();
    FreeAllPipelineLayouts();

    m_Device = nullptr;
    m_PipelineStateObject = nullptr;
    m_SubPass = nullptr;
    
}

ArisenEngine::RHI::RHIVkGPUPipeline::RHIVkGPUPipeline(RHIVkDevice* device, RHIPipelineState* pso, UInt32 maxFramesInFlight):
RHIPipeline(maxFramesInFlight), m_Device(device), m_VkDevice(static_cast<VkDevice>(device->GetHandle())), m_PipelineStateObject(pso)
{
    m_VkGraphicPipelines.resize(maxFramesInFlight);
    m_VkGraphicsPipelineLayouts.resize(maxFramesInFlight);
    for(int i = 0; i < maxFramesInFlight; ++i)
    {
        m_VkGraphicPipelines[i] = VK_NULL_HANDLE;
        m_VkGraphicsPipelineLayouts[i] = VK_NULL_HANDLE;
    }
}

void* ArisenEngine::RHI::RHIVkGPUPipeline::GetGraphicsPipeline(UInt32 frameIndex)
{
    ASSERT(m_VkGraphicPipelines[frameIndex % m_MaxFramesInFlight] != VK_NULL_HANDLE);
    return m_VkGraphicPipelines[frameIndex % m_MaxFramesInFlight];
}

void ArisenEngine::RHI::RHIVkGPUPipeline::AllocGraphicPipeline(UInt32 frameIndex, RHISubPass* subPass)
{
    FreePipeline(frameIndex);
    FreePipelineLayout(frameIndex);

    m_SubPass = subPass;
    ASSERT(m_PipelineStateObject != nullptr);
    {
        // Create Pipeline Layout
        VkPipelineLayoutCreateInfo pipelineLayoutInfo {};
        pipelineLayoutInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
        auto* vkPSO = static_cast<RHIVkGPUPipelineStateObject*>(m_PipelineStateObject);
        pipelineLayoutInfo.setLayoutCount = vkPSO->DescriptorSetLayoutCount();
        pipelineLayoutInfo.pSetLayouts = static_cast<const VkDescriptorSetLayout*>(vkPSO->GetDescriptorSetLayouts());
        pipelineLayoutInfo.pushConstantRangeCount = static_cast<uint32_t>(vkPSO->GetPushConstantRanges().size());
        pipelineLayoutInfo.pPushConstantRanges = vkPSO->GetPushConstantRanges().data();

        if (vkCreatePipelineLayout(m_VkDevice, &pipelineLayoutInfo,
            nullptr, &m_VkGraphicsPipelineLayouts[frameIndex % m_MaxFramesInFlight]) != VK_SUCCESS)
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

        // Depth Stencil State
        const auto& dsState = m_PipelineStateObject->GetDepthStencilState();
        VkPipelineDepthStencilStateCreateInfo depthStencilInfo {};
        depthStencilInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_DEPTH_STENCIL_STATE_CREATE_INFO;
        depthStencilInfo.depthTestEnable = static_cast<VkBool32>(dsState.depthTestEnable);
        depthStencilInfo.depthWriteEnable = static_cast<VkBool32>(dsState.depthWriteEnable);
        depthStencilInfo.depthCompareOp = static_cast<VkCompareOp>(dsState.depthCompareOp);
        depthStencilInfo.depthBoundsTestEnable = static_cast<VkBool32>(dsState.depthBoundsTestEnable);
        depthStencilInfo.stencilTestEnable = static_cast<VkBool32>(dsState.stencilTestEnable);
        
        depthStencilInfo.front.failOp = static_cast<VkStencilOp>(dsState.front.failOp);
        depthStencilInfo.front.passOp = static_cast<VkStencilOp>(dsState.front.passOp);
        depthStencilInfo.front.depthFailOp = static_cast<VkStencilOp>(dsState.front.depthFailOp);
        depthStencilInfo.front.compareOp = static_cast<VkCompareOp>(dsState.front.compareOp);
        depthStencilInfo.front.compareMask = dsState.front.compareMask;
        depthStencilInfo.front.writeMask = dsState.front.writeMask;
        depthStencilInfo.front.reference = dsState.front.reference;

        depthStencilInfo.back.failOp = static_cast<VkStencilOp>(dsState.back.failOp);
        depthStencilInfo.back.passOp = static_cast<VkStencilOp>(dsState.back.passOp);
        depthStencilInfo.back.depthFailOp = static_cast<VkStencilOp>(dsState.back.depthFailOp);
        depthStencilInfo.back.compareOp = static_cast<VkCompareOp>(dsState.back.compareOp);
        depthStencilInfo.back.compareMask = dsState.back.compareMask;
        depthStencilInfo.back.writeMask = dsState.back.writeMask;
        depthStencilInfo.back.reference = dsState.back.reference;

        depthStencilInfo.minDepthBounds = dsState.minDepthBounds;
        depthStencilInfo.maxDepthBounds = dsState.maxDepthBounds;
        
        auto stages = m_PipelineStateObject->GetStageCreateInfo();
        VkGraphicsPipelineCreateInfo pipelineInfo {};
        pipelineInfo.sType = VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO;
        pipelineInfo.stageCount = static_cast<uint32_t>(m_PipelineStateObject->GetStageCount());
        pipelineInfo.pStages = static_cast<const VkPipelineShaderStageCreateInfo*>(stages);
        
        auto* vkPSO = static_cast<RHIVkGPUPipelineStateObject*>(m_PipelineStateObject);
        if (vkPSO->IsMeshPipeline())
        {
            pipelineInfo.pVertexInputState = nullptr;
            pipelineInfo.pInputAssemblyState = nullptr;
        }
        else
        {
            pipelineInfo.pVertexInputState = &vertexInputInfo;
            pipelineInfo.pInputAssemblyState = &inputAssemblyInfo;
        }
        
        pipelineInfo.pViewportState = &viewportStateInfo;
        pipelineInfo.pRasterizationState = &rasterizerInfo;
        pipelineInfo.pMultisampleState = &multipleSampleInfo;
        pipelineInfo.pDepthStencilState = &depthStencilInfo;
        pipelineInfo.pColorBlendState = &blendState;
        pipelineInfo.pDynamicState = &dynamicStatesInfo;
        pipelineInfo.layout = m_VkGraphicsPipelineLayouts[frameIndex % m_MaxFramesInFlight];
        
        // Handle Dynamic Rendering vs RenderPass
        VkPipelineRenderingCreateInfoKHR renderingInfo {};

        if (subPass == nullptr || vkPSO->IsDynamicRendering())
        {
            if (vkPSO->IsDynamicRendering())
            {
                vkPSO->FillRenderingCreateInfo(renderingInfo);
                pipelineInfo.pNext = &renderingInfo;
                pipelineInfo.renderPass = VK_NULL_HANDLE;
                pipelineInfo.subpass = 0; // ignored when renderPass is NULL
            }
            else
            {
                 LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocGraphicPipeline]: SubPass is null but PSO not configured for dynamic rendering!");
            }
        }
        else
        {
            pipelineInfo.renderPass = static_cast<VkRenderPass>(subPass->GetOwner()->GetHandle(frameIndex));
            pipelineInfo.subpass = subPass->GetIndex();
            pipelineInfo.pNext = nullptr;
        }

        pipelineInfo.basePipelineHandle = VK_NULL_HANDLE;

        if (vkCreateGraphicsPipelines(m_VkDevice, VK_NULL_HANDLE, 1, &pipelineInfo, nullptr, &m_VkGraphicPipelines[frameIndex % m_MaxFramesInFlight]) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocPipeline]: failed to create GPU pipeline!");
        }
    }
}

void ArisenEngine::RHI::RHIVkGPUPipeline::AllocComputePipeline(UInt32 frameIndex)
{
    FreePipeline(frameIndex);
    FreePipelineLayout(frameIndex);

    auto* vkPso = static_cast<RHIVkGPUPipelineStateObject*>(m_PipelineStateObject);
    
    // Create Layout
    VkPipelineLayoutCreateInfo layoutInfo{};
    layoutInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
    layoutInfo.setLayoutCount = vkPso->DescriptorSetLayoutCount();
    layoutInfo.pSetLayouts = static_cast<const VkDescriptorSetLayout*>(vkPso->GetDescriptorSetLayouts());
    layoutInfo.pushConstantRangeCount = static_cast<uint32_t>(vkPso->GetPushConstantRanges().size());
    layoutInfo.pPushConstantRanges = vkPso->GetPushConstantRanges().data();

    if (vkCreatePipelineLayout(m_VkDevice, &layoutInfo, nullptr, &m_VkGraphicsPipelineLayouts[frameIndex % m_MaxFramesInFlight]) != VK_SUCCESS) {
        LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocComputePipeline]: failed to create pipeline layout!");
    }

    VkComputePipelineCreateInfo pipelineInfo{};
    pipelineInfo.sType = VK_STRUCTURE_TYPE_COMPUTE_PIPELINE_CREATE_INFO;
    pipelineInfo.layout = m_VkGraphicsPipelineLayouts[frameIndex % m_MaxFramesInFlight];
    
    // Compute stage is in m_PipelineStageCreateInfos[0]
    if (vkPso->m_PipelineStageCreateInfos.empty())
    {
        LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocComputePipeline]: No shader stages found in PSO!");
    }
    pipelineInfo.stage = vkPso->m_PipelineStageCreateInfos[0];

    if (vkCreateComputePipelines(m_VkDevice, VK_NULL_HANDLE, 1, &pipelineInfo, nullptr, &m_VkGraphicPipelines[frameIndex % m_MaxFramesInFlight]) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocComputePipeline]: failed to create compute pipeline!");
    }
}

const ArisenEngine::RHI::EPipelineBindPoint ArisenEngine::RHI::RHIVkGPUPipeline::GetBindPoint() const
{
    return m_PipelineStateObject->GetBindPoint();
}

void ArisenEngine::RHI::RHIVkGPUPipeline::FreePipelineLayout(UInt32 frameIndex)
{
    if (m_VkGraphicsPipelineLayouts[frameIndex % m_MaxFramesInFlight] != VK_NULL_HANDLE)
    {
        vkDestroyPipelineLayout(m_VkDevice, m_VkGraphicsPipelineLayouts[frameIndex % m_MaxFramesInFlight], nullptr);
        LOG_DEBUG("## Destroy Vulkan Pipeline Layout ##");
        m_VkGraphicsPipelineLayouts[frameIndex % m_MaxFramesInFlight] = VK_NULL_HANDLE;
    }
}

void ArisenEngine::RHI::RHIVkGPUPipeline::FreePipeline(UInt32 frameIndex)
{
    if (m_VkGraphicPipelines[frameIndex % m_MaxFramesInFlight] != VK_NULL_HANDLE)
    {
        vkDestroyPipeline(m_VkDevice, m_VkGraphicPipelines[frameIndex % m_MaxFramesInFlight], nullptr);
        LOG_DEBUG("## Destroy Vulkan Graphic Pipeline ##");
        m_VkGraphicPipelines[frameIndex % m_MaxFramesInFlight] = VK_NULL_HANDLE;
    }
}

void ArisenEngine::RHI::RHIVkGPUPipeline::BindPipelineStateObject(RHIPipelineState* pso)
{
    m_PipelineStateObject = pso;
}

void ArisenEngine::RHI::RHIVkGPUPipeline::FreeAllPipelineLayouts()
{
    for(int i = 0; i < m_MaxFramesInFlight; ++i)
    {
        if (m_VkGraphicsPipelineLayouts[i] != VK_NULL_HANDLE)
        {
            vkDestroyPipelineLayout(m_VkDevice, m_VkGraphicsPipelineLayouts[i], nullptr);
            m_VkGraphicsPipelineLayouts[i] = VK_NULL_HANDLE;
        }
    }
    LOG_DEBUG("## Destroy All Vulkan Pipeline Layouts ##");
    m_VkGraphicsPipelineLayouts.clear();
}

void ArisenEngine::RHI::RHIVkGPUPipeline::FreeAllPipelines()
{
    for(int i = 0; i < m_MaxFramesInFlight; ++i)
    {
        if (m_VkGraphicPipelines[i] != VK_NULL_HANDLE)
        {
            vkDestroyPipeline(m_VkDevice, m_VkGraphicPipelines[i], nullptr);
            m_VkGraphicPipelines[i] = VK_NULL_HANDLE;
        }
    }
    LOG_DEBUG("## Destroy All Vulkan Pipelines ##");
    m_VkGraphicPipelines.clear();
}







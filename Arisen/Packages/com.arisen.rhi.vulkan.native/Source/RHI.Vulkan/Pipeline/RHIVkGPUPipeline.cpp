#include "Pipeline/RHIVkGPUPipeline.h"

#include "Pipeline/RHIVkGPUPipelineStateObject.h"
#include "Pipeline/RHIVkPSOCache.h"
#include "RHI/Pipeline/RHIPipelineState.h"
#include "Core/RHIVkDevice.h"

namespace ArisenEngine::RHI
{
    RHIVkGPUPipeline::~RHIVkGPUPipeline() noexcept
    {
        LOG_DEBUG("[RHIVkGPUPipeline::~RHIVkGPUPipeline]: ~RHIVkGPUPipeline");

        FreeAllPipelines();
        FreeAllPipelineLayouts();

        m_Device = nullptr;
        m_PipelineStateObject = nullptr;
        m_SubPass = nullptr;
    }

    RHIVkGPUPipeline::RHIVkGPUPipeline(RHIVkDevice* device, RHIPipelineState* pso, UInt32 maxFramesInFlight):
        RHIPipeline(maxFramesInFlight), m_Device(device), m_VkDevice(static_cast<VkDevice>(device->GetHandle())),
        m_PipelineStateObject(pso), m_SubPass(nullptr)
    {
    }

    VkPipelineLayout RHIVkGPUPipeline::GetPipelineLayout(UInt32 frameIndex) const
    {
        auto* vkPSO = static_cast<RHIVkGPUPipelineStateObject*>(m_PipelineStateObject);
        auto* cache = static_cast<RHIVkGPUPipelineManager*>(m_Device->GetPipelineCache())->GetPSOCache();
        return cache->GetLayout(vkPSO->GetHash());
    }

    VkPipeline RHIVkGPUPipeline::GetVkPipeline(UInt32 frameIndex) const
    {
        // Need to cast away const for GetGraphics/Compute pipeline as they might need better access but we can stay const here by calling them directly or using cache
        auto* vkPSO = static_cast<RHIVkGPUPipelineStateObject*>(m_PipelineStateObject);
        RHIVkPSOCacheKey key;
        key.psoHash = vkPSO->GetHash();
        if (m_SubPass)
        {
            key.renderPass = static_cast<VkRenderPass>(m_SubPass->GetOwner()->GetHandle(frameIndex));
            key.subpassIndex = m_SubPass->GetIndex();
        }
        else
        {
            key.renderPass = VK_NULL_HANDLE;
            key.subpassIndex = 0;
        }

        auto* cache = static_cast<RHIVkGPUPipelineManager*>(m_Device->GetPipelineCache())->GetPSOCache();
        return cache->GetPipeline(key);
    }

    void* RHIVkGPUPipeline::GetGraphicsPipeline(UInt32 frameIndex)
    {
        auto* vkPSO = static_cast<RHIVkGPUPipelineStateObject*>(m_PipelineStateObject);
        RHIVkPSOCacheKey key;
        key.psoHash = vkPSO->GetHash();
        if (m_SubPass)
        {
            key.renderPass = static_cast<VkRenderPass>(m_SubPass->GetOwner()->GetHandle(frameIndex));
            key.subpassIndex = m_SubPass->GetIndex();
        }
        else
        {
            key.renderPass = VK_NULL_HANDLE;
            key.subpassIndex = 0;
        }

        auto* cache = static_cast<RHIVkGPUPipelineManager*>(m_Device->GetPipelineCache())->GetPSOCache();
        VkPipeline pipeline = cache->GetPipeline(key);

        // If not found, we might need to allocate it here if it wasn't pre-allocated, 
        // but the current architecture seems to expect Alloc to be called.
        // For safety, let's assert it exists or call Alloc if we had enough info.
        ASSERT(pipeline != VK_NULL_HANDLE);
        return pipeline;
    }

    void* RHIVkGPUPipeline::GetComputePipeline(UInt32 frameIndex)
    {
        // Compute pipelines don't have render passes
        auto* vkPSO = static_cast<RHIVkGPUPipelineStateObject*>(m_PipelineStateObject);
        RHIVkPSOCacheKey key;
        key.psoHash = vkPSO->GetHash();
        key.renderPass = VK_NULL_HANDLE;
        key.subpassIndex = 0;

        auto* cache = static_cast<RHIVkGPUPipelineManager*>(m_Device->GetPipelineCache())->GetPSOCache();
        VkPipeline pipeline = cache->GetPipeline(key);
        ASSERT(pipeline != VK_NULL_HANDLE);
        return pipeline;
    }

    void RHIVkGPUPipeline::AllocGraphicPipeline(UInt32 frameIndex, RHISubPass* subPass)
    {
        m_SubPass = subPass;
        ASSERT(m_PipelineStateObject != nullptr);

        auto* vkPSO = static_cast<RHIVkGPUPipelineStateObject*>(m_PipelineStateObject);
        auto* cache = static_cast<RHIVkGPUPipelineManager*>(m_Device->GetPipelineCache())->GetPSOCache();

        // 1. Get or Create Layout
        VkPipelineLayout layout = cache->GetLayout(vkPSO->GetHash());
        if (layout == VK_NULL_HANDLE)
        {
            VkPipelineLayoutCreateInfo pipelineLayoutInfo{};
            pipelineLayoutInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
            pipelineLayoutInfo.setLayoutCount = vkPSO->GetDescriptorSetLayoutCount();
            pipelineLayoutInfo.pSetLayouts = vkPSO->GetDescriptorSetLayouts();
            pipelineLayoutInfo.pushConstantRangeCount = static_cast<uint32_t>(vkPSO->GetPushConstantRanges().size());
            pipelineLayoutInfo.pPushConstantRanges = vkPSO->GetPushConstantRanges().data();

            if (vkCreatePipelineLayout(m_VkDevice, &pipelineLayoutInfo, nullptr, &layout) != VK_SUCCESS)
            {
                LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocGraphicPipeline]: failed to create pipeline layout!");
            }
            cache->StoreLayout(vkPSO->GetHash(), layout);
        }

        // 2. Build Cache Key for Pipeline
        RHIVkPSOCacheKey key;
        key.psoHash = vkPSO->GetHash();
        if (subPass)
        {
            key.renderPass = static_cast<VkRenderPass>(subPass->GetOwner()->GetHandle(frameIndex));
            key.subpassIndex = subPass->GetIndex();
        }
        else
        {
            key.renderPass = VK_NULL_HANDLE;
            key.subpassIndex = 0;
        }

        // 3. Check Cache
        VkPipeline pipeline = cache->GetPipeline(key);
        if (pipeline != VK_NULL_HANDLE) return;

        // 4. Create Pipeline if not found

        // Vertex input info
        VkPipelineVertexInputStateCreateInfo vertexInputInfo{};
        vertexInputInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO;
        vertexInputInfo.vertexBindingDescriptionCount = static_cast<uint32_t>(vkPSO->GetVertexInputBindingDescriptions()
            .size());
        vertexInputInfo.pVertexBindingDescriptions = vkPSO->GetVertexInputBindingDescriptions().data();
        vertexInputInfo.vertexAttributeDescriptionCount = static_cast<uint32_t>(vkPSO->
            GetVertexInputAttributeDescriptions().size());
        vertexInputInfo.pVertexAttributeDescriptions = vkPSO->GetVertexInputAttributeDescriptions().data();

        // Input Assembly info
        const auto& ia = m_PipelineStateObject->GetInputAssemblyState();
        VkPipelineInputAssemblyStateCreateInfo inputAssemblyInfo{};
        inputAssemblyInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO;
        inputAssemblyInfo.topology = static_cast<VkPrimitiveTopology>(ia.topology);
        inputAssemblyInfo.primitiveRestartEnable = static_cast<VkBool32>(ia.primitiveRestartEnable);

        // viewport state
        VkPipelineViewportStateCreateInfo viewportStateInfo{};
        viewportStateInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO;
        viewportStateInfo.scissorCount = 1;
        viewportStateInfo.viewportCount = 1;
        viewportStateInfo.pScissors = nullptr;
        viewportStateInfo.pViewports = nullptr;

        // rasterizer state
        const auto& rs = m_PipelineStateObject->GetRasterizationState();
        VkPipelineRasterizationStateCreateInfo rasterizerInfo{};
        rasterizerInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO;
        rasterizerInfo.depthClampEnable = static_cast<VkBool32>(rs.depthClampEnable);
        rasterizerInfo.rasterizerDiscardEnable = static_cast<VkBool32>(rs.rasterizerDiscardEnable);
        rasterizerInfo.polygonMode = static_cast<VkPolygonMode>(rs.polygonMode);
        rasterizerInfo.lineWidth = rs.lineWidth;
        rasterizerInfo.cullMode = static_cast<VkCullModeFlags>(rs.cullMode);
        rasterizerInfo.frontFace = static_cast<VkFrontFace>(rs.frontFace);
        rasterizerInfo.depthBiasEnable = static_cast<VkBool32>(rs.depthBiasEnable);
        rasterizerInfo.depthBiasConstantFactor = rs.depthBiasConstantFactor;
        rasterizerInfo.depthBiasClamp = rs.depthBiasClamp;
        rasterizerInfo.depthBiasSlopeFactor = rs.depthBiasSlopeFactor;

        // multiple sample
        const auto& ms = m_PipelineStateObject->GetMultisampleState();
        VkPipelineMultisampleStateCreateInfo multipleSampleInfo{};
        multipleSampleInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO;
        multipleSampleInfo.rasterizationSamples = static_cast<VkSampleCountFlagBits>(ms.rasterizationSamples);
        multipleSampleInfo.sampleShadingEnable = static_cast<VkBool32>(ms.sampleShadingEnable);
        multipleSampleInfo.minSampleShading = ms.minSampleShading;
        multipleSampleInfo.pSampleMask = reinterpret_cast<const VkSampleMask*>(ms.pSampleMask);
        multipleSampleInfo.alphaToCoverageEnable = static_cast<VkBool32>(ms.alphaToCoverageEnable);
        multipleSampleInfo.alphaToOneEnable = static_cast<VkBool32>(ms.alphaToOneEnable);

        // blend state
        const auto& cb = vkPSO->GetColorBlendState();
        Containers::Vector<VkPipelineColorBlendAttachmentState> vkAttachments;
        for (const auto& att : cb.attachments)
        {
            VkPipelineColorBlendAttachmentState vkAtt{};
            vkAtt.blendEnable = static_cast<VkBool32>(att.blendEnable);
            vkAtt.srcColorBlendFactor = static_cast<VkBlendFactor>(att.srcColorBlendFactor);
            vkAtt.dstColorBlendFactor = static_cast<VkBlendFactor>(att.dstColorBlendFactor);
            vkAtt.colorBlendOp = static_cast<VkBlendOp>(att.colorBlendOp);
            vkAtt.srcAlphaBlendFactor = static_cast<VkBlendFactor>(att.srcAlphaBlendFactor);
            vkAtt.dstAlphaBlendFactor = static_cast<VkBlendFactor>(att.dstAlphaBlendFactor);
            vkAtt.alphaBlendOp = static_cast<VkBlendOp>(att.alphaBlendOp);
            vkAtt.colorWriteMask = att.colorWriteMask;
            vkAttachments.emplace_back(vkAtt);
        }

        VkPipelineColorBlendStateCreateInfo blendState{};
        blendState.sType = VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO;
        blendState.attachmentCount = static_cast<uint32_t>(vkAttachments.size());
        blendState.pAttachments = vkAttachments.data();
        blendState.logicOpEnable = static_cast<VkBool32>(cb.logicOpEnable);
        blendState.logicOp = static_cast<VkLogicOp>(cb.logicOp);
        std::memcpy(blendState.blendConstants, cb.blendConstants, sizeof(float) * 4);

        // dynamic state
        Containers::Vector<VkDynamicState> dynamicStates;
        UInt64 mask = m_PipelineStateObject->GetDynamicStateMask();
        if (mask & DYNAMIC_STATE_VIEWPORT_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_VIEWPORT);
        if (mask & DYNAMIC_STATE_SCISSOR_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_SCISSOR);
        if (mask & DYNAMIC_STATE_LINE_WIDTH_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_LINE_WIDTH);
        if (mask & DYNAMIC_STATE_DEPTH_BIAS_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_DEPTH_BIAS);
        if (mask & DYNAMIC_STATE_BLEND_CONSTANTS_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_BLEND_CONSTANTS);
        if (mask & DYNAMIC_STATE_DEPTH_BOUNDS_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_DEPTH_BOUNDS);
        if (mask & DYNAMIC_STATE_STENCIL_COMPARE_MASK_BIT) dynamicStates.push_back(
            VK_DYNAMIC_STATE_STENCIL_COMPARE_MASK);
        if (mask & DYNAMIC_STATE_STENCIL_WRITE_MASK_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_STENCIL_WRITE_MASK);
        if (mask & DYNAMIC_STATE_STENCIL_REFERENCE_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_STENCIL_REFERENCE);
        if (mask & DYNAMIC_STATE_CULL_MODE_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_CULL_MODE);
        if (mask & DYNAMIC_STATE_FRONT_FACE_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_FRONT_FACE);
        if (mask & DYNAMIC_STATE_PRIMITIVE_TOPOLOGY_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_PRIMITIVE_TOPOLOGY);
        if (mask & DYNAMIC_STATE_DEPTH_TEST_ENABLE_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_DEPTH_TEST_ENABLE);
        if (mask & DYNAMIC_STATE_DEPTH_WRITE_ENABLE_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_DEPTH_WRITE_ENABLE);
        if (mask & DYNAMIC_STATE_DEPTH_COMPARE_OP_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_DEPTH_COMPARE_OP);
        if (mask & DYNAMIC_STATE_STENCIL_TEST_ENABLE_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_STENCIL_TEST_ENABLE);
        if (mask & DYNAMIC_STATE_STENCIL_OP_BIT) dynamicStates.push_back(VK_DYNAMIC_STATE_STENCIL_OP);
        if (mask & DYNAMIC_STATE_FRAGMENT_SHADING_RATE_BIT) dynamicStates.push_back(
            VK_DYNAMIC_STATE_FRAGMENT_SHADING_RATE_KHR);

        VkPipelineDynamicStateCreateInfo dynamicStatesInfo{};
        dynamicStatesInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO;
        dynamicStatesInfo.dynamicStateCount = static_cast<uint32_t>(dynamicStates.size());
        dynamicStatesInfo.pDynamicStates = dynamicStates.data();

        // Depth Stencil State
        const auto& dsState = m_PipelineStateObject->GetDepthStencilState();
        VkPipelineDepthStencilStateCreateInfo depthStencilInfo{};
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

        VkGraphicsPipelineCreateInfo pipelineInfo{};
        pipelineInfo.sType = VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO;
        pipelineInfo.stageCount = vkPSO->GetStageCount();
        pipelineInfo.pStages = vkPSO->GetStageCreateInfos();

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
        pipelineInfo.layout = layout;

        // Tessellation state
        const auto& tess = vkPSO->GetTessellationState();
        VkPipelineTessellationStateCreateInfo tessellationInfo{};
        bool hasTessellation = false;
        for (UInt32 i = 0; i < vkPSO->GetStageCount(); ++i)
        {
            if (vkPSO->GetStageCreateInfos()[i].stage == VK_SHADER_STAGE_TESSELLATION_CONTROL_BIT ||
                vkPSO->GetStageCreateInfos()[i].stage == VK_SHADER_STAGE_TESSELLATION_EVALUATION_BIT)
            {
                hasTessellation = true;
                break;
            }
        }
        if (hasTessellation)
        {
            tessellationInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_TESSELLATION_STATE_CREATE_INFO;
            tessellationInfo.patchControlPoints = tess.patchControlPoints;
            pipelineInfo.pTessellationState = &tessellationInfo;
        }
        else
        {
            pipelineInfo.pTessellationState = nullptr;
        }

        // Handle Dynamic Rendering vs RenderPass
        VkPipelineRenderingCreateInfoKHR renderingInfo{};

        if (subPass == nullptr || vkPSO->IsDynamicRendering())
        {
            if (vkPSO->IsDynamicRendering())
            {
                vkPSO->FillRenderingCreateInfo(renderingInfo);
                pipelineInfo.pNext = &renderingInfo;
                pipelineInfo.renderPass = VK_NULL_HANDLE;
                pipelineInfo.subpass = 0;
            }
            else
            {
                LOG_FATAL_AND_THROW(
                    "[RHIVkGPUPipeline::AllocGraphicPipeline]: SubPass is null but PSO not configured for dynamic rendering!")
                ;
            }
        }
        else
        {
            pipelineInfo.renderPass = key.renderPass;
            pipelineInfo.subpass = key.subpassIndex;
            pipelineInfo.pNext = nullptr;
        }

        pipelineInfo.basePipelineHandle = VK_NULL_HANDLE;

        if (vkCreateGraphicsPipelines(
            m_VkDevice, static_cast<RHIVkGPUPipelineManager*>(m_Device->GetPipelineCache())->GetVkPipelineCache(), 1,
            &pipelineInfo, nullptr, &pipeline) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocPipeline]: failed to create GPU pipeline!");
        }
        cache->StorePipeline(key, pipeline);
    }

    void RHIVkGPUPipeline::AllocComputePipeline(UInt32 frameIndex)
    {
        m_SubPass = nullptr;
        auto* vkPso = static_cast<RHIVkGPUPipelineStateObject*>(m_PipelineStateObject);
        auto* cache = static_cast<RHIVkGPUPipelineManager*>(m_Device->GetPipelineCache())->GetPSOCache();

        // 1. Get or Create Layout
        VkPipelineLayout layout = cache->GetLayout(vkPso->GetHash());
        if (layout == VK_NULL_HANDLE)
        {
            VkPipelineLayoutCreateInfo layoutInfo{};
            layoutInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
            layoutInfo.setLayoutCount = vkPso->GetDescriptorSetLayoutCount();
            layoutInfo.pSetLayouts = vkPso->GetDescriptorSetLayouts();
            layoutInfo.pushConstantRangeCount = static_cast<uint32_t>(vkPso->GetPushConstantRanges().size());
            layoutInfo.pPushConstantRanges = vkPso->GetPushConstantRanges().data();

            if (vkCreatePipelineLayout(m_VkDevice, &layoutInfo, nullptr, &layout) != VK_SUCCESS)
            {
                LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocComputePipeline]: failed to create pipeline layout!");
            }
            cache->StoreLayout(vkPso->GetHash(), layout);
        }

        // 2. Build Cache Key
        RHIVkPSOCacheKey key;
        key.psoHash = vkPso->GetHash();
        key.renderPass = VK_NULL_HANDLE;
        key.subpassIndex = 0;

        // 3. Check Cache
        VkPipeline pipeline = cache->GetPipeline(key);
        if (pipeline != VK_NULL_HANDLE) return;

        // 4. Create Compute Pipeline
        VkComputePipelineCreateInfo pipelineInfo{};
        pipelineInfo.sType = VK_STRUCTURE_TYPE_COMPUTE_PIPELINE_CREATE_INFO;
        pipelineInfo.layout = layout;

        if (vkPso->m_PipelineStageCreateInfos.empty())
        {
            LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocComputePipeline]: No shader stages found in PSO!");
        }
        pipelineInfo.stage = vkPso->m_PipelineStageCreateInfos[0];

        if (vkCreateComputePipelines(
            m_VkDevice, static_cast<RHIVkGPUPipelineManager*>(m_Device->GetPipelineCache())->GetVkPipelineCache(), 1,
            &pipelineInfo, nullptr, &pipeline) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocComputePipeline]: failed to create compute pipeline!");
        }
        cache->StorePipeline(key, pipeline);
    }

    void RHIVkGPUPipeline::AllocRayTracingPipeline(UInt32 frameIndex)
    {
        m_SubPass = nullptr;
        auto* vkPso = static_cast<RHIVkGPUPipelineStateObject*>(m_PipelineStateObject);
        auto* cache = static_cast<RHIVkGPUPipelineManager*>(m_Device->GetPipelineCache())->GetPSOCache();

        // 1. Get or Create Layout
        VkPipelineLayout layout = cache->GetLayout(vkPso->GetHash());
        if (layout == VK_NULL_HANDLE)
        {
            VkPipelineLayoutCreateInfo layoutInfo{};
            layoutInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
            layoutInfo.setLayoutCount = vkPso->GetDescriptorSetLayoutCount();
            layoutInfo.pSetLayouts = vkPso->GetDescriptorSetLayouts();
            layoutInfo.pushConstantRangeCount = static_cast<uint32_t>(vkPso->GetPushConstantRanges().size());
            layoutInfo.pPushConstantRanges = vkPso->GetPushConstantRanges().data();

            if (vkCreatePipelineLayout(m_VkDevice, &layoutInfo, nullptr, &layout) != VK_SUCCESS)
            {
                LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocRayTracingPipeline]: failed to create pipeline layout!");
            }
            cache->StoreLayout(vkPso->GetHash(), layout);
        }

        // 2. Build Cache Key
        RHIVkPSOCacheKey key;
        key.psoHash = vkPso->GetHash();
        key.renderPass = VK_NULL_HANDLE;
        key.subpassIndex = 0;

        // 3. Check Cache
        VkPipeline pipeline = cache->GetPipeline(key);
        if (pipeline != VK_NULL_HANDLE) return;

        // 4. Create Ray Tracing Pipeline
        VkRayTracingPipelineCreateInfoKHR pipelineInfo{};
        pipelineInfo.sType = VK_STRUCTURE_TYPE_RAY_TRACING_PIPELINE_CREATE_INFO_KHR;
        pipelineInfo.layout = layout;
        pipelineInfo.stageCount = vkPso->GetStageCount();
        pipelineInfo.pStages = vkPso->GetStageCreateInfos();
        pipelineInfo.groupCount = static_cast<uint32_t>(vkPso->m_RayTracingShaderGroups.size());
        pipelineInfo.pGroups = vkPso->m_RayTracingShaderGroups.data();
        pipelineInfo.maxPipelineRayRecursionDepth = vkPso->m_MaxRecursionDepth;

        if (m_Device->vkCreateRayTracingPipelinesKHR(m_VkDevice, VK_NULL_HANDLE,
                                                     static_cast<RHIVkGPUPipelineManager*>(m_Device->GetPipelineCache())
                                                     ->GetVkPipelineCache(), 1, &pipelineInfo, nullptr,
                                                     &pipeline) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkGPUPipeline::AllocRayTracingPipeline]: failed to create Ray Tracing pipeline!");
        }
        cache->StorePipeline(key, pipeline);
    }

    const EPipelineBindPoint RHIVkGPUPipeline::GetBindPoint() const
    {
        return m_PipelineStateObject->GetBindPoint();
    }

    void RHIVkGPUPipeline::FreePipelineLayout(UInt32 frameIndex)
    {
    }

    void RHIVkGPUPipeline::FreePipeline(UInt32 frameIndex)
    {
    }

    void RHIVkGPUPipeline::BindPipelineStateObject(RHIPipelineState* pso)
    {
        m_PipelineStateObject = pso;
    }

    void RHIVkGPUPipeline::FreeAllPipelineLayouts()
    {
    }

    void RHIVkGPUPipeline::FreeAllPipelines()
    {
    }
} // namespace ArisenEngine::RHI

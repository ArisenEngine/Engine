#pragma once
#include "../RHIRenderingTestBase.h"

namespace ArisenEngine::Testing
{
    class RHIVRSShadingRateTest : public RHIRenderingTestBase
    {
    private:
        RHI_PSOHandle m_Pso = nullptr;
        RHI_PipelineHandle m_Pipeline = 0;
        RHI_BufferHandle m_VertexBuffer = 0;
        RHI_BufferHandle m_IndexBuffer = 0;

    public:
        const char* GetName() const override { return "VRSShadingRateTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();
            InitShaderProgram(L"VRSShadingRate");

            CreateResources();
            CreatePipeline();

            return true;
        }

        void TeardownTest() override
        {
            if (m_VertexBuffer) RHI_Device_ReleaseBuffer(m_Device, m_VertexBuffer);
            if (m_IndexBuffer) RHI_Device_ReleaseBuffer(m_Device, m_IndexBuffer);
            if (m_Pso) RHI_PSO_Release(m_Pso);

            RHIRenderingTestBase::TeardownTest();
        }

    protected:
        void RenderFrame() override
        {
            auto currentIndex = GetCurrentFrameIndex();
            if (m_FrameTickets[currentIndex] > 0)
            {
                RHI_Device_WaitQueueTicket(m_Device, m_FrameTickets[currentIndex]);
            }

            RecordAndSubmit();
            NextFrame();
        }

    private:
        void CreateResources()
        {
            struct Vertex {
                float pos[3];
                float uv[2];
            };

            Vertex vertices[] = {
                {{-1.0f, -1.0f, 0.0f}, {0.0f, 0.0f}},
                {{ 1.0f, -1.0f, 0.0f}, {1.0f, 0.0f}},
                {{ 1.0f,  1.0f, 0.0f}, {1.0f, 1.0f}},
                {{-1.0f,  1.0f, 0.0f}, {0.0f, 1.0f}}
            };
            uint32_t indices[] = { 0, 1, 2, 2, 3, 0 };

            RHI::RHIBufferDescriptor vDesc = {};
            vDesc.size = sizeof(vertices);
            vDesc.usage = RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT;
            vDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_VertexBuffer = RHI_Device_CreateBuffer(m_Device, &vDesc, "VRS_VB");
            RHI_Buffer_MemoryCopy(m_Device, m_VertexBuffer, vertices, sizeof(vertices), 0);

            RHI::RHIBufferDescriptor iDesc = {};
            iDesc.size = sizeof(indices);
            iDesc.usage = RHI::BUFFER_USAGE_INDEX_BUFFER_BIT;
            iDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_IndexBuffer = RHI_Device_CreateBuffer(m_Device, &iDesc, "VRS_IB");
            RHI_Buffer_MemoryCopy(m_Device, m_IndexBuffer, indices, sizeof(indices), 0);
        }

        void CreatePipeline()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);
            m_Pso = RHI_PipelineManager_CreatePSO(pm);

            RHI_PSO_AddProgram(m_Pso, m_VertProgram);
            RHI_PSO_AddProgram(m_Pso, m_FragProgram);

            RHI_PSO_AddVertexBindingDescription(m_Pso, 0, 5 * sizeof(float), RHI::VERTEX_INPUT_RATE_VERTEX);
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 0, 0, RHI::FORMAT_R32G32B32_SFLOAT, 0);
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 1, 0, RHI::FORMAT_R32G32_SFLOAT, 3 * sizeof(float));

            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
            RHI_PSO_SetInputAssemblyState(m_Pso, &ia);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_NONE;
            rs.polygonMode = RHI::EPOLYGON_MODE_FILL;
            rs.lineWidth = 1.0f;
            RHI_PSO_SetRasterizationState(m_Pso, &rs);

            RHI::RHIMultisampleState ms{};
            ms.rasterizationSamples = RHI::SAMPLE_COUNT_1_BIT;
            RHI_PSO_SetMultisampleState(m_Pso, &ms);

            RHI::RHIDepthStencilState ds{};
            ds.depthTestEnable = false;
            ds.depthWriteEnable = false;
            RHI_PSO_SetDepthStencilState(m_Pso, &ds);

            RHI::RHIColorBlendState cb{};
            RHI::RHIColorBlendAttachmentState blendAttachment{};
            blendAttachment.blendEnable = false;
            blendAttachment.colorWriteMask = RHI::COLOR_COMPONENT_R_BIT | RHI::COLOR_COMPONENT_G_BIT | RHI::COLOR_COMPONENT_B_BIT | RHI::COLOR_COMPONENT_A_BIT;
            cb.attachments.push_back(blendAttachment);
            RHI_PSO_SetColorBlendState(m_Pso, &cb);

            RHI_PSO_SetDynamicStateMask(m_Pso, RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT | RHI::DYNAMIC_STATE_FRAGMENT_SHADING_RATE_BIT);

            Containers::Vector<RHI::EFormat> colorFormats = { RHI::FORMAT_B8G8R8A8_SRGB };
            RHI_PSO_SetRenderingFormats(m_Pso, &colorFormats, RHI::FORMAT_UNDEFINED, RHI::FORMAT_UNDEFINED);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);

            m_Pipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_Pso);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, currentIndex);

            RHI_Cmd_Begin(cmd, currentIndex, 0);

            auto colorBuffer = RHI_SwapChain_BeginFrame(m_SwapChain, currentIndex);
            if (colorBuffer)
            {
                auto colorView = RHI_SwapChain_GetImageView(m_SwapChain, currentIndex);
                RHI_Cmd_TransitionImageLayout(cmd, colorBuffer, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

                RHI::RHIRenderingAttachmentInfo colorAttachment {};
                colorAttachment.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&colorView);
                colorAttachment.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                colorAttachment.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                colorAttachment.storeOp = RHI::ATTACHMENT_STORE_OP_STORE;
                colorAttachment.clearValue.float32[0] = 0.05f;
                colorAttachment.clearValue.float32[1] = 0.05f;
                colorAttachment.clearValue.float32[2] = 0.05f;
                colorAttachment.clearValue.float32[3] = 1.0f;

                UInt32 width = HAL::GetWindowWidth(m_WindowId);
                UInt32 height = HAL::GetWindowHeight(m_WindowId);

                RHI::RHIRenderingInfo renderInfo {};
                renderInfo.RHIRenderArea = { 0, 0, width, height };
                renderInfo.layerCount = 1;
                renderInfo.colorAttachmentCount = 1;
                renderInfo.pColorAttachments = &colorAttachment;

                RHI_Cmd_BeginRendering(cmd, &renderInfo);
                RHI_Cmd_BindPipeline(cmd, m_Pipeline);
                RHI_Cmd_SetViewport(cmd, 0, 0, (float)width, (float)height, 0, 1);
                RHI_Cmd_SetScissor(cmd, 0, 0, width, height);
                
                RHI_Cmd_BindVertexBuffers(cmd, m_VertexBuffer, 0);
                RHI_Cmd_BindIndexBuffer(cmd, m_IndexBuffer, 0, RHI::INDEX_TYPE_UINT32);

                RHI::EShadingRate rates[] = {
                    RHI::EShadingRate::Rate1x1,
                    RHI::EShadingRate::Rate2x2,
                    RHI::EShadingRate::Rate4x4
                };
                RHI::EShadingRateCombiner combiners[2] = { RHI::EShadingRateCombiner::Keep, RHI::EShadingRateCombiner::Keep };

                for (int i = 0; i < 3; ++i)
                {
                    float quadWidth = (float)width / 3.0f;
                    RHI_Cmd_SetViewport(cmd, i * quadWidth, 0, quadWidth, (float)height, 0, 1);
                    RHI_Cmd_SetFragmentShadingRate(cmd, rates[i], combiners);
                    RHI_Cmd_DrawIndexed(cmd, 6, 1, 0, 0, 0, 0);
                }

                RHI_Cmd_EndRendering(cmd);
                RHI_Cmd_TransitionImageLayout(cmd, colorBuffer, RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);
            }

            RHI_Cmd_End(cmd);

            RHI::RHISubmitDescriptor submitDesc = {};
            submitDesc.WaitSwapChain = reinterpret_cast<RHI::RHISwapChain*>(m_SwapChain);
            submitDesc.SignalSwapChain = reinterpret_cast<RHI::RHISwapChain*>(m_SwapChain);
            
            m_FrameTickets[currentIndex] = RHI_Device_Submit(m_Device, cmd, reinterpret_cast<const ::RHISubmitDescriptor*>(&submitDesc));
            RHI_SwapChain_EndFrame(m_SwapChain, currentIndex);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, currentIndex, cmd);
        }
    };
}

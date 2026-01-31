#pragma once

#include "../RHIRenderingTestBase.h"

using namespace ArisenEngine;

namespace ArisenEngine::Testing
{
    class RHIBasicRenderingTest : public RHIRenderingTestBase
    {
    private:
        RHI_PSOHandle m_Pso = nullptr;
        RHI_PipelineHandle m_Pipeline = nullptr;
        Containers::Vector<RHI_BufferHandle> m_UboBuffer;

    public:
        const char* GetName() const override { return "BasicRenderingTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();
            InitShaderProgram(L"StandardTest");
            CreateResources();
            InitRenderContext();
            CreatePipeline();

            return true;
        }

        void TeardownTest() override
        {
            for (auto& ub : m_UboBuffer)
            {
                if (ub) RHI_Device_ReleaseBuffer(m_Device, ub);
            }
            m_UboBuffer.clear();

            if (m_Pso) RHI_PSO_Destroy(m_Pso);
            
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

            UpdateUniformBuffer();
            RecordAndSubmit();

            NextFrame();
        }

    private:
        void CreateResources()
        {
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = std::filesystem::path(exePathW).parent_path();
            
            m_Model = LoadGLTF((exeDir / "Assets" / "Buggy.gltf").string());

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(UniformBufferObject);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_UboBuffer.push_back(RHI_Device_CreateBuffer(m_Device, &ubDesc, "UBO"));
            }
        }

        void InitRenderContext()
        {
            RHI_RenderPass_AddAttachmentAction(m_Device, m_RenderPass,
                RHI::FORMAT_B8G8R8A8_SRGB,
                RHI::SAMPLE_COUNT_1_BIT,
                RHI::ATTACHMENT_LOAD_OP_CLEAR,
                RHI::ATTACHMENT_STORE_OP_STORE,
                RHI::ATTACHMENT_LOAD_OP_DONT_CARE,
                RHI::ATTACHMENT_STORE_OP_DONT_CARE,
                RHI::IMAGE_LAYOUT_UNDEFINED,
                RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);

            auto subpass = RHI_RenderPass_AddSubPass(m_Device, m_RenderPass);
            RHI_Subpass_SetBindPoint(subpass, RHI::PIPELINE_BIND_POINT_GRAPHICS);
            RHI_Subpass_AddColorReference(subpass, 0, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

            RHI_Subpass_SetDependency(subpass, VK_SUBPASS_EXTERNAL,
                RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
                RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
                RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
                RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT, 0);

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI_RenderPass_Alloc(m_Device, m_RenderPass, i);
            }

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types = { RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER };
                Containers::Vector<UInt32> counts = { 1 };
                m_DescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, 1));
            }
        }

        void CreatePipeline()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);
            m_Pso = RHI_PipelineManager_CreatePSO(pm);

            RHI_PSO_AddProgram(m_Pso, m_VertProgram);
            RHI_PSO_AddProgram(m_Pso, m_FragProgram);

            RHI_PSO_AddVertexBindingDescription(m_Pso, 0, sizeof(GLTFVertex), RHI::VERTEX_INPUT_RATE_VERTEX);
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 0, 0, RHI::FORMAT_R32G32B32_SFLOAT, offsetof(GLTFVertex, pos));
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 1, 0, RHI::FORMAT_R32G32B32_SFLOAT, offsetof(GLTFVertex, normal));
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 2, 0, RHI::FORMAT_R32G32_SFLOAT, offsetof(GLTFVertex, uv));

            Containers::Vector<RHI::RHIBufferHandle> buffers;
            buffers.push_back(*reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[0]));
            RHI_PSO_AddDescriptorSetLayoutBinding_Buffers(m_Pso, 0, 0, RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER, 1, RHI::SHADER_STAGE_VERTEX_BIT, &buffers);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);

            RHI_PSO_AddDynamicState(m_Pso, RHI::DYNAMIC_STATE_VIEWPORT);
            RHI_PSO_AddDynamicState(m_Pso, RHI::DYNAMIC_STATE_SCISSOR);

            m_Pipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_Pso);
            auto subpass = RHI_RenderPass_GetSubpass(m_RenderPass, 0);
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i) {
                RHI_Pipeline_AllocGraphics(m_Device, m_Pipeline, i, subpass);
            }
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            UniformBufferObject ubo;
            ubo.model = glm::mat4(1.0f);
            ubo.view = GetViewMatrix();
            ubo.proj = GetProjectionMatrix(1280.0f / 720.0f);
            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffer[m_FrameIndex], &ubo, 0);
        }

        void RecordAndSubmit()
        {
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, m_FrameIndex);

            // Update descriptors
            Containers::Vector<RHI::RHIBufferHandle> ubos = { *reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[m_FrameIndex]) };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 0, &ubos);

            RHI_DescriptorPool_Reset(m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex]);
            RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex], 0, m_Pso);
            RHI_DescriptorPool_UpdateDescriptorSets(m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex], m_Pso);

            RHI_Cmd_Begin(cmd, m_FrameIndex, 0);

            auto surface = RHI_Instance_GetSurface(m_Instance, m_WindowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            auto colorBuffer = RHI_SwapChain_AquireCurrentImage(swapchain, m_FrameIndex);
            if (colorBuffer)
            {
                auto colorView = RHI_SwapChain_GetImageView(swapchain, m_FrameIndex);
                RHI_RenderPass_Alloc(m_Device, m_RenderPass, m_FrameIndex);
                RHI_FrameBuffer_SetAttachment(m_Device, m_FrameBuffer, m_FrameIndex, colorView, m_RenderPass);

                RHI::RHIClearValue clearValues[1];
                clearValues[0].color[0] = 0.0f;
                clearValues[0].color[1] = 0.0f;
                clearValues[0].color[2] = 0.2f;
                clearValues[0].color[3] = 1.0f;

                RHI::RenderPassBeginDesc rpBegin = {
                    *reinterpret_cast<RHI::RHIRenderPassHandle*>(&m_RenderPass),
                    *reinterpret_cast<RHI::RHIFrameBufferHandle*>(&m_FrameBuffer),
                    RHI::SUBPASS_CONTENTS_INLINE,
                    1,
                    clearValues
                };

                RHI_Cmd_BeginRenderPass(cmd, m_FrameIndex, &rpBegin);
                RHI_Cmd_BindPipeline(cmd, m_FrameIndex, m_Pipeline);
                RHI_Cmd_SetViewport(cmd, 0, 0, 1280, 720, 0, 1);
                RHI_Cmd_SetScissor(cmd, 0, 0, 1280, 720);
                RHI_Cmd_BindDescriptorSets_FromPool(cmd, m_FrameIndex, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex]);
                RHI_Cmd_BindVertexBuffers(cmd, m_Model.vertexBuffer, 0);
                RHI_Cmd_BindIndexBuffer(cmd, m_Model.indexBuffer, 0, RHI::INDEX_TYPE_UINT32);
                RHI_Cmd_DrawIndexed(cmd, m_Model.indexCount, 1, 0, 0, 0, 0);
                RHI_Cmd_EndRenderPass(cmd);

                auto imageAvailableSem = RHI_SwapChain_GetImageAvailableSemaphore(swapchain, m_FrameIndex);
                auto renderFinishedSem = RHI_SwapChain_GetRenderFinishSemaphore(swapchain, m_FrameIndex);
                RHI_Cmd_WaitSemaphore(cmd, imageAvailableSem, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT);
                RHI_Cmd_SignalSemaphore(cmd, renderFinishedSem);
            }

            RHI_Cmd_End(cmd);
            m_FrameTickets[m_FrameIndex] = RHI_Device_Submit(m_Device, cmd, m_FrameIndex);
            RHI_SwapChain_Present(swapchain, m_FrameIndex);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, m_FrameIndex, cmd);
        }
    };
}

#pragma once

#include "../RHIRenderingTestBase.h"
#include <random>

namespace ArisenEngine::Testing
{
    class RHIGeometryShaderTest : public RHIRenderingTestBase
    {
    private:
        struct ParticleVertex
        {
            glm::vec3 pos;
            glm::vec4 color;
            glm::vec2 size;
        };

        struct ParticleSceneData
        {
            glm::mat4 view;
            glm::mat4 proj;
            glm::vec3 cameraPos;
            float padding;
        };

        RHI_PSOHandle m_Pso = nullptr;
        RHI_PipelineHandle m_Pipeline = 0;
        RHI_BufferHandle m_ParticleBuffer = 0;
        Containers::Vector<RHI_BufferHandle> m_UboBuffers;
        RHI_SubpassHandle m_Subpass = 0;
        
        RHI_GPUProgramHandle m_GsProgram = 0;

        const UInt32 m_ParticleCount = 1000;

    public:
        const char* GetName() const override { return "GeometryShaderTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();
            
            // Override programs to include GS
            auto shaderEnv = GetShaderEnvString();

            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            auto currentPath = exeDir.generic_wstring() + L"\\Shader";
            auto shaderPath = currentPath + L"\\GeometryShaderTest.hlsl";
            
            HAL::ShaderCompileParams vsParams;
            vsParams.input = shaderPath;
            vsParams.entry = L"vs_main";
            vsParams.stage = RHI::Vertex;
            vsParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput vsOut;
            if (!HAL::CompileShaderFromFile(std::move(vsParams), vsOut) || vsOut.codeSize == 0)
            {
                LOG_ERROR("Failed to compile VS for GeometryShaderTest");
                return false;
            }
            
            RHI::RHIShaderProgramDesc vsDesc;
            vsDesc.byteCode = vsOut.codePointer;
            vsDesc.codeSize = vsOut.codeSize;
            vsDesc.stage = RHI::SHADER_STAGE_VERTEX_BIT;
            vsDesc.entry = "vs_main";
            vsDesc.name = "GS_VS";
            m_VertProgram = RHI_Device_CreateGPUProgram(m_Device);
            RHI_Device_AttachProgramByteCode(m_Device, m_VertProgram, &vsDesc);
            if (vsOut.codePointer) std::free(vsOut.codePointer);

            HAL::ShaderCompileParams gsParams;
            gsParams.input = shaderPath;
            gsParams.entry = L"gs_main";
            gsParams.stage = RHI::Geometry;
            gsParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput gsOut;
            if (!HAL::CompileShaderFromFile(std::move(gsParams), gsOut) || gsOut.codeSize == 0)
            {
                LOG_ERROR("Failed to compile GS for GeometryShaderTest");
                return false;
            }

            RHI::RHIShaderProgramDesc gsDesc;
            gsDesc.byteCode = gsOut.codePointer;
            gsDesc.codeSize = gsOut.codeSize;
            gsDesc.stage = RHI::SHADER_STAGE_GEOMETRY_BIT;
            gsDesc.entry = "gs_main";
            gsDesc.name = "GS_GS";
            m_GsProgram = RHI_Device_CreateGPUProgram(m_Device);
            RHI_Device_AttachProgramByteCode(m_Device, m_GsProgram, &gsDesc);
            if (gsOut.codePointer) std::free(gsOut.codePointer);

            HAL::ShaderCompileParams psParams;
            psParams.input = shaderPath;
            psParams.entry = L"ps_main";
            psParams.stage = RHI::Fragment;
            psParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput psOut;
            if (!HAL::CompileShaderFromFile(std::move(psParams), psOut) || psOut.codeSize == 0)
            {
                LOG_ERROR("Failed to compile PS for GeometryShaderTest");
                return false;
            }

            RHI::RHIShaderProgramDesc psDesc;
            psDesc.byteCode = psOut.codePointer;
            psDesc.codeSize = psOut.codeSize;
            psDesc.stage = RHI::SHADER_STAGE_FRAGMENT_BIT;
            psDesc.entry = "ps_main";
            psDesc.name = "GS_PS";
            m_FragProgram = RHI_Device_CreateGPUProgram(m_Device);
            RHI_Device_AttachProgramByteCode(m_Device, m_FragProgram, &psDesc);
            if (psOut.codePointer) std::free(psOut.codePointer);

            CreateResources();
            InitRenderContext();
            CreatePipeline();

            return true;
        }

        void TeardownTest() override
        {
            if (m_ParticleBuffer) RHI_Device_ReleaseBuffer(m_Device, m_ParticleBuffer);
            for (auto& ub : m_UboBuffers)
            {
                if (ub) RHI_Device_ReleaseBuffer(m_Device, ub);
            }
            if (m_GsProgram) RHI_Device_ReleaseGPUProgram(m_Device, m_GsProgram);
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
            // Create Particles
            std::vector<ParticleVertex> particles(m_ParticleCount);
            std::default_random_engine rndEngine((unsigned)time(nullptr));
            std::uniform_real_distribution<float> distPos(-5.0f, 5.0f);
            std::uniform_real_distribution<float> distSize(0.5f, 2.0f);
            std::uniform_real_distribution<float> distColor(0.5f, 1.0f);

            for (auto& p : particles)
            {
                p.pos = glm::vec3(distPos(rndEngine), distPos(rndEngine), distPos(rndEngine));
                p.color = glm::vec4(distColor(rndEngine), distColor(rndEngine), distColor(rndEngine), 1.0f);
                p.size = glm::vec2(distSize(rndEngine));
            }

            RHI::RHIBufferDescriptor pDesc = {};
            pDesc.size = particles.size() * sizeof(ParticleVertex);
            pDesc.usage = RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT;
            pDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_ParticleBuffer = RHI_Device_CreateBuffer(m_Device, &pDesc, "ParticleBuffer");
            
            // Initial upload
            RHI_Buffer_MemoryCopy(m_Device, m_ParticleBuffer, particles.data(), pDesc.size, 0);

            // UBOs
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(ParticleSceneData);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_UboBuffers.push_back(RHI_Device_CreateBuffer(m_Device, &ubDesc, "ParticleUBO"));
            }

            m_CameraPos = glm::vec3(0.0f, 0.0f, 10.0f);
        }

        void InitRenderContext()
        {
            // Simple render pass with swapchain color attachment
            RHI_RenderPass_AddAttachmentAction(m_Device, m_RenderPass,
                RHI::FORMAT_B8G8R8A8_SRGB,
                RHI::SAMPLE_COUNT_1_BIT,
                RHI::ATTACHMENT_LOAD_OP_CLEAR,
                RHI::ATTACHMENT_STORE_OP_STORE,
                RHI::ATTACHMENT_LOAD_OP_DONT_CARE,
                RHI::ATTACHMENT_STORE_OP_DONT_CARE,
                RHI::IMAGE_LAYOUT_UNDEFINED,
                RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);

            m_Subpass = RHI_RenderPass_AddSubPass(m_Device, m_RenderPass);
            RHI_Subpass_SetBindPoint(m_Subpass, RHI::PIPELINE_BIND_POINT_GRAPHICS);
            RHI_Subpass_AddColorReference(m_Subpass, 0, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI_RenderPass_Alloc(m_Device, m_RenderPass, i);
            }

            Containers::Vector<RHI::EDescriptorType> types = { RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER };
            Containers::Vector<UInt32> counts = { 1 };
            m_DescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, 1));
        }

        void CreatePipeline()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);
            m_Pso = RHI_PipelineManager_CreatePSO(pm);

            RHI_PSO_AddProgram(m_Pso, m_VertProgram);
            RHI_PSO_AddProgram(m_Pso, m_GsProgram);
            RHI_PSO_AddProgram(m_Pso, m_FragProgram);

            RHI_PSO_AddVertexBindingDescription(m_Pso, 0, sizeof(ParticleVertex), RHI::VERTEX_INPUT_RATE_VERTEX);
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 0, 0, RHI::FORMAT_R32G32B32_SFLOAT, offsetof(ParticleVertex, pos));
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 1, 0, RHI::FORMAT_R32G32B32A32_SFLOAT, offsetof(ParticleVertex, color));
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 2, 0, RHI::FORMAT_R32G32_SFLOAT, offsetof(ParticleVertex, size));

            RHI_PSO_SetTopology(m_Pso, RHI::PRIMITIVE_TOPOLOGY_POINT_LIST);
            RHI_PSO_SetCullMode(m_Pso, RHI::CULL_MODE_NONE);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);

            RHI_PSO_AddBlendAttachmentState(m_Pso, true, 
                RHI::BLEND_FACTOR_SRC_ALPHA, RHI::BLEND_FACTOR_ONE_MINUS_SRC_ALPHA, RHI::BLEND_OP_ADD,
                RHI::BLEND_FACTOR_ONE, RHI::BLEND_FACTOR_ZERO, RHI::BLEND_OP_ADD,
                0xF);
            
            RHI_PSO_AddDynamicState(m_Pso, RHI::DYNAMIC_STATE_VIEWPORT);
            RHI_PSO_AddDynamicState(m_Pso, RHI::DYNAMIC_STATE_SCISSOR);

            m_Pipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_Pso);
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i) {
                RHI_Pipeline_AllocGraphics(m_Device, m_Pipeline, i, m_Subpass);
            }
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            ParticleSceneData ubo;
            ubo.view = GetViewMatrix();
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);
            ubo.proj = GetProjectionMatrix(width / height);
            ubo.cameraPos = m_CameraPos;
            
            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffers[GetCurrentFrameIndex()], &ubo, sizeof(ParticleSceneData), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, currentIndex);

            RHI_DescriptorPool_Reset(m_DescriptorPool, m_DescriptorPoolIds[0]);
            
            Containers::Vector<RHI_BufferHandle> ubos = { m_UboBuffers[currentIndex] };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 0, reinterpret_cast<ArisenEngine::Containers::Vector<ArisenEngine::RHI::RHIBufferHandle>*>(&ubos));

            UInt32 setIdx = RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[0], 0, m_Pso);
            RHI_DescriptorPool_UpdateDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[0], setIdx, m_Pso);

            RHI_Cmd_Begin(cmd, currentIndex, 0);

            auto surface = RHI_Instance_GetSurface(m_Instance, m_WindowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            auto colorBuffer = RHI_SwapChain_AquireCurrentImage(swapchain, currentIndex);
            
            if (colorBuffer)
            {
                auto colorView = RHI_SwapChain_GetImageView(swapchain, currentIndex);
                RHI_RenderPass_Alloc(m_Device, m_RenderPass, currentIndex);
                RHI_FrameBuffer_SetAttachment(m_Device, m_FrameBuffer, currentIndex, colorView, m_RenderPass, 0);
                
                RHI::RHIClearValue clearValue;
                clearValue.color[0] = 0.0f;
                clearValue.color[1] = 0.0f;
                clearValue.color[2] = 0.0f;
                clearValue.color[3] = 1.0f;

                RHI::RenderPassBeginDesc rpBegin{};
                rpBegin.renderPass = *reinterpret_cast<RHI::RHIRenderPassHandle*>(&m_RenderPass);
                rpBegin.frameBuffer = *reinterpret_cast<RHI::RHIFrameBufferHandle*>(&m_FrameBuffer);
                rpBegin.subpassContents = RHI::SUBPASS_CONTENTS_INLINE;
                rpBegin.clearValueCount = 1;
                rpBegin.pClearValues = &clearValue;

                RHI_Cmd_BeginRenderPass(cmd, currentIndex, &rpBegin);
                UInt32 width = HAL::GetWindowWidth(m_WindowId);
                UInt32 height = HAL::GetWindowHeight(m_WindowId);
                RHI_Cmd_BindPipeline(cmd, currentIndex, m_Pipeline);
                RHI_Cmd_SetViewport(cmd, 0, 0, (float)width, (float)height, 0, 1);
                RHI_Cmd_SetScissor(cmd, 0, 0, width, height);
                
                RHI_Cmd_BindVertexBuffers(cmd, m_ParticleBuffer, 0);
                RHI_Cmd_BindDescriptorSet_FromPool(cmd, currentIndex, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPool, m_DescriptorPoolIds[0], setIdx);
                
                RHI_Cmd_Draw(cmd, m_ParticleCount, 1, 0, 0, 0);
                RHI_Cmd_EndRenderPass(cmd);

                auto imageAvailableSem = RHI_SwapChain_GetImageAvailableSemaphore(swapchain, currentIndex);
                auto renderFinishedSem = RHI_SwapChain_GetRenderFinishSemaphore(swapchain, currentIndex);
                RHI_Cmd_WaitSemaphore(cmd, imageAvailableSem, static_cast<unsigned int>(RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT));
                RHI_Cmd_SignalSemaphore(cmd, renderFinishedSem);
            }

            RHI_Cmd_End(cmd);
            m_FrameTickets[currentIndex] = RHI_Device_Submit(m_Device, cmd, currentIndex);
            RHI_SwapChain_Present(swapchain, currentIndex);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, currentIndex, cmd);
        }
    };
}

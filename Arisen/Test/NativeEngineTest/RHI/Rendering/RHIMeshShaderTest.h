#pragma once

#include "../RHIRenderingTestBase.h"

namespace ArisenEngine::Testing
{
    using namespace ArisenEngine;
    class RHIMeshShaderTest : public RHIRenderingTestBase
    {
    private:
        RHI_PSOHandle m_MeshPso = nullptr;
        RHI_PipelineHandle m_MeshPipeline = 0;
        
        Containers::Vector<RHI_BufferHandle> m_UboBuffer;
        Containers::Vector<UInt32> m_DescriptorPoolIds;
        
        RHI_GPUProgramHandle m_MeshProgram = 0;
        RHI_GPUProgramHandle m_FragProgram = 0;

        RHI_GPUProgramHandle CreateProgram(const std::wstring& shaderName, RHI::EShaderStage stageFlag, const char* entryPoint, const Containers::Vector<String>& defines = {})
        {
            std::wstring envStr = GetShaderEnvString().ToWString();
            
            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            
            // Search in source directory relative to engine root (common in dev builds)
            auto shaderPath = exeDir / L"../../Arisen/Test/NativeEngineTest/Shader" / (shaderName + L".hlsl");
            if (!fs::exists(shaderPath))
            {
                // Fallback or generic path logic
                shaderPath = exeDir / L"Shader" / (shaderName + L".hlsl");
            }
            auto path = shaderPath.wstring();

            RHI::EProgramStage stagePoint;
            if (stageFlag == RHI::SHADER_STAGE_MESH_BIT_EXT) stagePoint = RHI::EProgramStage::Mesh;
            else if (stageFlag == RHI::SHADER_STAGE_TASK_BIT_EXT) stagePoint = RHI::EProgramStage::Amplification;
            else if (stageFlag == RHI::SHADER_STAGE_FRAGMENT_BIT) stagePoint = RHI::EProgramStage::Fragment;
            else stagePoint = RHI::EProgramStage::Mesh;

            HAL::ShaderCompileParams params;
            params.input = path;
            params.entry = String::StringToWString(entryPoint);
            params.shaderModel = L"6_5"; // Mesh shaders require 6.5+
            params.target = L"-spirv";
            params.targetEnv = envStr;
            params.optimizeLevel = L"0";
            params.stage = stagePoint;
            params.defines = defines;
            params.includes = {};
            params.output = std::nullopt;
            params.useDXLayout = true;

            HAL::ShaderCompilerOutput output;
            if (!HAL::CompileShaderFromFile(std::move(params), output) || output.codePointer == nullptr || output.codeSize == 0)
            {
                LOG_ERROR((std::string("Shader compilation failed for ") + entryPoint + ": " + output.msgOut.c_str()).c_str());
                return 0;
            }

            auto program = RHI_Device_CreateGPUProgram(m_Device);
            {
                std::string nameStr = String::WStringToString(path);
                RHI::RHIShaderProgramDesc desc = { output.codeSize, output.codePointer, entryPoint, nameStr.c_str(), stageFlag };
                RHI_Device_AttachProgramByteCode(m_Device, program, &desc);
            }
            if (output.codePointer) std::free(output.codePointer);
            return program;
        }

    public:
        const char* GetName() const override { return "MeshShaderTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();
            
            // Programs
            m_MeshProgram = CreateProgram(L"MeshShaderTest", RHI::SHADER_STAGE_MESH_BIT_EXT, "MSMain", { L"MESH_STAGE" });
            m_FragProgram = CreateProgram(L"MeshShaderTest", RHI::SHADER_STAGE_FRAGMENT_BIT, "PSMain", { L"PIXEL_STAGE" });

            if (m_MeshProgram == 0 || m_FragProgram == 0)
            {
                LOG_ERROR("MeshShaderTest: Shader compilation failed, skipping test setup.");
                return false;
            }

            CreateResources();
            CreatePipelines();

            return true;
        }

        void TeardownTest() override
        {
            for (auto& ub : m_UboBuffer) if (ub) RHI_Device_ReleaseBuffer(m_Device, ub);
            
            if (m_MeshProgram) RHI_Device_ReleaseGPUProgram(m_Device, m_MeshProgram);
            if (m_FragProgram) RHI_Device_ReleaseGPUProgram(m_Device, m_FragProgram);

            if (m_MeshPso) RHI_PSO_Release(m_MeshPso);

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
            // UBO
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                struct MeshUBO {
                    glm::mat4 model;
                    glm::mat4 view;
                    glm::mat4 projection;
                    float time;
                    float padding[3];
                };

                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(MeshUBO);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_UboBuffer.push_back(RHI_Device_CreateBuffer(m_Device, &ubDesc, "MeshUBO"));
            }

            // Descriptors
            m_DescriptorPoolIds.clear();
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types = { RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER };
                Containers::Vector<UInt32> counts = { 128 };
                m_DescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, 128));
            }
        }

        void CreatePipelines()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);

            // Mesh Pipeline
            m_MeshPso = RHI_PipelineManager_CreatePSO(pm);
            RHI_PSO_AddProgram(m_MeshPso, m_MeshProgram);
            RHI_PSO_AddProgram(m_MeshPso, m_FragProgram);
            
            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
            RHI_PSO_SetInputAssemblyState(m_MeshPso, &ia);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_NONE;
            RHI_PSO_SetRasterizationState(m_MeshPso, &rs);

            RHI::RHIColorBlendState cb{};
            RHI::RHIColorBlendAttachmentState att{};
            att.blendEnable = false;
            att.colorWriteMask = 0xF;
            cb.attachments.push_back(att);
            RHI_PSO_SetColorBlendState(m_MeshPso, &cb);

            RHI_PSO_BuildDescriptorSetLayout(m_MeshPso);
            RHI_PSO_SetDynamicStateMask(m_MeshPso, RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);
            
            Containers::Vector<RHI::EFormat> colorFormats = { RHI::FORMAT_B8G8R8A8_SRGB };
            RHI_PSO_SetRenderingFormats(m_MeshPso, &colorFormats, RHI::FORMAT_UNDEFINED, RHI::FORMAT_UNDEFINED);
            m_MeshPipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_MeshPso);
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);
            
            static auto startTime = std::chrono::high_resolution_clock::now();
            auto currentTime = std::chrono::high_resolution_clock::now();
            float time = std::chrono::duration<float, std::chrono::seconds::period>(currentTime - startTime).count();
            
            struct MeshUBO {
                glm::mat4 model;
                glm::mat4 view;
                glm::mat4 projection;
                float time;
                float padding[3];
            };
            
            MeshUBO ubo;
            ubo.model = glm::rotate(glm::mat4(1.0f), time * 0.5f, glm::vec3(0, 1, 0));
            ubo.view = GetViewMatrix();
            ubo.projection = GetProjectionMatrix(width / height);
            ubo.time = time;
            
            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffer[GetCurrentFrameIndex()], &ubo, sizeof(MeshUBO), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            
            // Update Descriptors
            {
                RHI_DescriptorPool_Reset(m_DescriptorPool, m_DescriptorPoolIds[currentIndex]);
                
                Containers::Vector<RHI_BufferHandle> ubos = { m_UboBuffer[currentIndex] };
                RHI_PSO_UpdateDescriptorSet_Buffers(m_MeshPso, 0, 0, &ubos);
                
                UInt32 setIdx = RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[currentIndex], 0, m_MeshPso);
                RHI_DescriptorPool_UpdateDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[currentIndex], setIdx, m_MeshPso);
            }

            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, currentIndex);

            RHI_Cmd_Begin(cmd, currentIndex, 0);

            // Graphics Render
            auto colorBuffer = RHI_SwapChain_BeginFrame(m_SwapChain, currentIndex);
            auto colorView = RHI_SwapChain_GetImageView(m_SwapChain, currentIndex);
            RHI::RHIImageHandle colorImage = *reinterpret_cast<RHI::RHIImageHandle*>(&colorBuffer);

            RHI::RHIRenderingInfo renderInfo = {};
            renderInfo.RHIRenderArea = { 0, 0, HAL::GetWindowWidth(m_WindowId), HAL::GetWindowHeight(m_WindowId) };
            renderInfo.layerCount = 1;
            renderInfo.colorAttachmentCount = 1;
            
            RHI::RHIRenderingAttachmentInfo att = {};
            att.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&colorView);
            att.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
            att.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
            att.storeOp = RHI::ATTACHMENT_STORE_OP_STORE;
            att.clearValue.float32[0] = 0.05f;
            att.clearValue.float32[1] = 0.05f;
            att.clearValue.float32[2] = 0.05f;
            att.clearValue.float32[3] = 1.0f;
            renderInfo.pColorAttachments = &att;

            // Transition: UNDEFINED -> COLOR_ATTACHMENT_OPTIMAL
            {
                RHI::RHIImageMemoryBarrier barrier = {};
                barrier.srcAccess = RHI::ACCESS_NONE;
                barrier.dstAccess = RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                barrier.oldLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
                barrier.newLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                barrier.srcQueueFamilyIndex = 0xFFFFFFFF;
                barrier.dstQueueFamilyIndex = 0xFFFFFFFF;
                barrier.image = colorImage;
                barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
                barrier.srcStageMask = RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT;
                barrier.dstStageMask = RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;

                Containers::Vector<RHI::RHIImageMemoryBarrier> barriers = { barrier };
                RHI_Cmd_PipelineBarrier_Image(cmd, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, 0, &barriers);
            }

            RHI_Cmd_BeginRendering(cmd, &renderInfo);
            RHI_Cmd_BindPipeline(cmd, m_MeshPipeline);
            RHI_Cmd_SetViewport(cmd, 0, 0, (float)renderInfo.RHIRenderArea.width, (float)renderInfo.RHIRenderArea.height, 0, 1);
            RHI_Cmd_SetScissor(cmd, 0, 0, renderInfo.RHIRenderArea.width, renderInfo.RHIRenderArea.height);
            RHI_Cmd_BindDescriptorSets_FromPool(cmd, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPool, m_DescriptorPoolIds[currentIndex]);
            
            // Draw 10 rings/groups of tasks
            RHI_Cmd_DrawMeshTasks(cmd, 10, 1, 1);
            
            RHI_Cmd_EndRendering(cmd);

            // Transition: COLOR_ATTACHMENT_OPTIMAL -> PRESENT_SRC_KHR
            {
                RHI::RHIImageMemoryBarrier barrier = {};
                barrier.srcAccess = RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                barrier.dstAccess = RHI::ACCESS_NONE;
                barrier.oldLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                barrier.newLayout = RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR;
                barrier.srcQueueFamilyIndex = 0xFFFFFFFF;
                barrier.dstQueueFamilyIndex = 0xFFFFFFFF;
                barrier.image = colorImage;
                barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
                barrier.srcStageMask = RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
                barrier.dstStageMask = RHI::PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT;

                Containers::Vector<RHI::RHIImageMemoryBarrier> barriers = { barrier };
                RHI_Cmd_PipelineBarrier_Image(cmd, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, RHI::PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, 0, &barriers);
            }

            // Sync


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

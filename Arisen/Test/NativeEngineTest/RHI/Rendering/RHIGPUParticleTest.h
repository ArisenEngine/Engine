#pragma once

#include "../RHIRenderingTestBase.h"

namespace ArisenEngine::Testing
{
    using namespace ArisenEngine;
    struct Particle {
        glm::vec4 position; // xyz, w = life
        glm::vec4 velocity; // xyz, w = maxLife
    };

    class RHIGPUParticleTest : public RHIRenderingTestBase
    {
    private:
        RHI_PSOHandle m_ComputePso = nullptr;
        RHI_PipelineHandle m_ComputePipeline = 0;
        
        RHI_PSOHandle m_GraphicsPso = nullptr;
        RHI_PipelineHandle m_GraphicsPipeline = 0;

        RHI_BufferHandle m_ParticleBuffer = 0;
        Containers::Vector<RHI_BufferHandle> m_UboBuffer;
        
        Containers::Vector<UInt32> m_ComputeDescriptorPoolIds;
        Containers::Vector<UInt32> m_GraphicsDescriptorPoolIds;
        
        RHI_GPUProgramHandle m_ComputeProgram = 0;
        
        const UInt32 m_ParticleCount = 1000000;

        RHI_GPUProgramHandle CreateProgram(const std::wstring& shaderName, RHI::EShaderStage stageFlag, const char* entryPoint)
        {
            std::wstring envStr = GetShaderEnvString().ToWString();
            
            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            auto shaderPath = exeDir / L"Shader" / (shaderName + L".hlsl");
            auto path = shaderPath.wstring();

            RHI::EProgramStage stagePoint;
            if (stageFlag == RHI::SHADER_STAGE_VERTEX_BIT) stagePoint = RHI::EProgramStage::Vertex;
            else if (stageFlag == RHI::SHADER_STAGE_FRAGMENT_BIT) stagePoint = RHI::EProgramStage::Fragment;
            else if (stageFlag == RHI::SHADER_STAGE_COMPUTE_BIT) stagePoint = RHI::EProgramStage::Compute;
            else stagePoint = RHI::EProgramStage::Vertex;

            HAL::ShaderCompileParams params;
            params.input = path;
            params.entry = String::StringToWString(entryPoint);
            params.shaderModel = L"6_0";
            params.target = L"-spirv";
            params.targetEnv = envStr;
            params.optimizeLevel = L"0";
            params.stage = stagePoint;
            params.defines = {};
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
        const char* GetName() const override { return "GPUParticleTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();
            
            // Programs
            m_ComputeProgram = CreateProgram(L"GPUParticle", RHI::SHADER_STAGE_COMPUTE_BIT, "CSMain");
            m_VertProgram = CreateProgram(L"GPUParticle", RHI::SHADER_STAGE_VERTEX_BIT, "VSMain");
            m_FragProgram = CreateProgram(L"GPUParticle", RHI::SHADER_STAGE_FRAGMENT_BIT, "PSMain");

            CreateResources();
            CreatePipelines();

            return true;
        }

        void TeardownTest() override
        {
            if (m_ParticleBuffer) RHI_Device_ReleaseBuffer(m_Device, m_ParticleBuffer);
            for (auto& ub : m_UboBuffer) if (ub) RHI_Device_ReleaseBuffer(m_Device, ub);
            
            if (m_ComputeProgram) RHI_Device_ReleaseGPUProgram(m_Device, m_ComputeProgram);
            if (m_VertProgram) RHI_Device_ReleaseGPUProgram(m_Device, m_VertProgram);
            if (m_FragProgram) RHI_Device_ReleaseGPUProgram(m_Device, m_FragProgram);

            if (m_ComputePso) RHI_PSO_Release(m_ComputePso);
            if (m_GraphicsPso) RHI_PSO_Release(m_GraphicsPso);

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
            // Particle Buffer
            RHI::RHIBufferDescriptor pDesc = {};
            pDesc.size = m_ParticleCount * sizeof(Particle);
            pDesc.usage = RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT | RHI::BUFFER_USAGE_TRANSFER_DST_BIT;
            pDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_ParticleBuffer = RHI_Device_CreateBuffer(m_Device, &pDesc, "ParticleBuffer");

            // Init particles
            Containers::Vector<Particle> particles(m_ParticleCount);
            for (auto& p : particles) {
                p.position = glm::vec4(
                    (rand() % 200 - 100) / 100.0f,  // x: -1 to 1
                    (rand() % 100) / 100.0f - 2.0f, // y: starts low
                    (rand() % 200 - 100) / 100.0f,  // z: -1 to 1
                    (rand() % 1000) / 100.0f        // life
                );
                p.velocity = glm::vec4(
                    (rand() % 40 - 20) / 100.0f,    // vx
                    (rand() % 100 + 50) / 100.0f,   // vy: upward
                    (rand() % 40 - 20) / 100.0f,    // vz
                    p.position.w                    // maxLife = initial life
                );
            }
            RHI_Buffer_MemoryCopy(m_Device, m_ParticleBuffer, particles.data(), pDesc.size, 0);

            // UBO
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                struct FireUBO {
                    glm::mat4 model;
                    glm::mat4 view;
                    glm::mat4 projection;
                    float mipmapBias;
                    float time;
                    float deltaTime;
                    float padding;
                };

                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(FireUBO);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_UboBuffer.push_back(RHI_Device_CreateBuffer(m_Device, &ubDesc, "UBO"));
            }

            // Descriptors
            m_ComputeDescriptorPoolIds.clear();
            m_GraphicsDescriptorPoolIds.clear();
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                // Compute Pool Family
                Containers::Vector<RHI::EDescriptorType> cTypes = { RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER, RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER };
                Containers::Vector<UInt32> cCounts = { 128, 128 };
                m_ComputeDescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &cTypes, &cCounts, 128));
                
                // Graphics Pool Family
                Containers::Vector<RHI::EDescriptorType> gTypes = { RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER, RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER };
                Containers::Vector<UInt32> gCounts = { 128, 128 };
                m_GraphicsDescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &gTypes, &gCounts, 128));
            }
        }

        void CreatePipelines()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);

            // Compute Pipeline
            m_ComputePso = RHI_PipelineManager_CreatePSO(pm);
            RHI_PSO_SetBindPoint(m_ComputePso, RHI::PIPELINE_BIND_POINT_COMPUTE);
            RHI_PSO_AddProgram(m_ComputePso, m_ComputeProgram);
            
            RHI_PSO_BuildDescriptorSetLayout(m_ComputePso);
            
            m_ComputePipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_ComputePso);

            // Graphics Pipeline
            m_GraphicsPso = RHI_PipelineManager_CreatePSO(pm);
            RHI_PSO_AddProgram(m_GraphicsPso, m_VertProgram);
            RHI_PSO_AddProgram(m_GraphicsPso, m_FragProgram);
            
            RHI_PSO_SetBindPoint(m_GraphicsPso, RHI::PIPELINE_BIND_POINT_GRAPHICS);

            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_POINT_LIST;
            RHI_PSO_SetInputAssemblyState(m_GraphicsPso, &ia);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_NONE;
            RHI_PSO_SetRasterizationState(m_GraphicsPso, &rs);

            RHI::RHIColorBlendState cb{};
            // Additive blending: SrcColor * 1 + DstColor * 1
            RHI::RHIColorBlendAttachmentState att{};
            att.blendEnable = true;
            att.colorWriteMask = 0xF;
            att.srcColorBlendFactor = RHI::BLEND_FACTOR_ONE;
            att.dstColorBlendFactor = RHI::BLEND_FACTOR_ONE;
            att.colorBlendOp = RHI::BLEND_OP_ADD;
            att.srcAlphaBlendFactor = RHI::BLEND_FACTOR_ONE;
            att.dstAlphaBlendFactor = RHI::BLEND_FACTOR_ZERO;
            att.alphaBlendOp = RHI::BLEND_OP_ADD;
            cb.attachments.push_back(att);
            RHI_PSO_SetColorBlendState(m_GraphicsPso, &cb);

            RHI_PSO_BuildDescriptorSetLayout(m_GraphicsPso);
            RHI_PSO_SetDynamicStateMask(m_GraphicsPso, RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);

            Containers::Vector<RHI::EFormat> colorFormats = { RHI::FORMAT_B8G8R8A8_SRGB };
            RHI_PSO_SetRenderingFormats(m_GraphicsPso, &colorFormats, RHI::FORMAT_UNDEFINED, RHI::FORMAT_UNDEFINED);

            m_GraphicsPipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_GraphicsPso);
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);
            
            static auto startTime = std::chrono::high_resolution_clock::now();
            auto currentTime = std::chrono::high_resolution_clock::now();
            float time = std::chrono::duration<float, std::chrono::seconds::period>(currentTime - startTime).count();
            
            struct FireUBO {
                glm::mat4 model;
                glm::mat4 view;
                glm::mat4 projection;
                float mipmapBias;
                float time;
                float deltaTime;
                float padding;
            };
            
            FireUBO fireUbo;
            fireUbo.model = glm::mat4(1.0f);
            fireUbo.view = GetViewMatrix();
            fireUbo.projection = GetProjectionMatrix(width / height);
            fireUbo.mipmapBias = 0.0f;
            fireUbo.time = time;
            fireUbo.deltaTime = (float)frameTime;
            
            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffer[GetCurrentFrameIndex()], &fireUbo, sizeof(FireUBO), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            
            // Update Descriptors
            {
                RHI_DescriptorPool_Reset(m_DescriptorPool, m_ComputeDescriptorPoolIds[currentIndex]);
                RHI_DescriptorPool_Reset(m_DescriptorPool, m_GraphicsDescriptorPoolIds[currentIndex]);

                Containers::Vector<RHI::RHIBufferHandle> pBuffers = { *reinterpret_cast<RHI::RHIBufferHandle*>(&m_ParticleBuffer) };
                RHI_PSO_UpdateDescriptorSet_Buffers(m_ComputePso, 0, 0, &pBuffers);
                
                Containers::Vector<RHI::RHIBufferHandle> ubos = { *reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[currentIndex]) };
                RHI_PSO_UpdateDescriptorSet_Buffers(m_ComputePso, 0, 1, &ubos);
                
                UInt32 setIdx = RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, m_ComputeDescriptorPoolIds[currentIndex], 0, m_ComputePso);
                RHI_DescriptorPool_UpdateDescriptorSet(m_DescriptorPool, m_ComputeDescriptorPoolIds[currentIndex], setIdx, m_ComputePso);
            }
            {
                Containers::Vector<RHI::RHIBufferHandle> pBuffers = { *reinterpret_cast<RHI::RHIBufferHandle*>(&m_ParticleBuffer) };
                RHI_PSO_UpdateDescriptorSet_Buffers(m_GraphicsPso, 0, 0, &pBuffers);
                
                Containers::Vector<RHI::RHIBufferHandle> ubos = { *reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[currentIndex]) };
                RHI_PSO_UpdateDescriptorSet_Buffers(m_GraphicsPso, 0, 1, &ubos);
                
                UInt32 setIdx = RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, m_GraphicsDescriptorPoolIds[currentIndex], 0, m_GraphicsPso);
                RHI_DescriptorPool_UpdateDescriptorSet(m_DescriptorPool, m_GraphicsDescriptorPoolIds[currentIndex], setIdx, m_GraphicsPso);
            }

            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, currentIndex);

            RHI_Cmd_Begin(cmd, currentIndex, 0);

            // Compute Update
            RHI_Cmd_BindPipeline(cmd, m_ComputePipeline);
            RHI_Cmd_BindDescriptorSets_FromPool(cmd, RHI::PIPELINE_BIND_POINT_COMPUTE, 0, m_DescriptorPool, m_ComputeDescriptorPoolIds[currentIndex]);
            RHI_Cmd_Dispatch(cmd, (m_ParticleCount + 255) / 256, 1, 1);

            // Barrier: Compute Write -> Vertex Read
            RHI::RHIBufferMemoryBarrier barrier = {};
            barrier.buffer = *reinterpret_cast<RHI::RHIBufferHandle*>(&m_ParticleBuffer);
            barrier.srcAccessMask = RHI::ACCESS_SHADER_WRITE_BIT;
            barrier.dstAccessMask = RHI::ACCESS_SHADER_READ_BIT;
            barrier.srcStageMask = RHI::PIPELINE_STAGE_COMPUTE_SHADER_BIT;
            barrier.dstStageMask = RHI::PIPELINE_STAGE_VERTEX_SHADER_BIT;
            barrier.srcQueueFamilyIndex = 0xFFFFFFFF;
            barrier.dstQueueFamilyIndex = 0xFFFFFFFF;
            
            Containers::Vector<RHI::RHIBufferMemoryBarrier> barriers = { barrier };
            RHI_Cmd_PipelineBarrier_Buffer(cmd, RHI::PIPELINE_STAGE_COMPUTE_SHADER_BIT, RHI::PIPELINE_STAGE_VERTEX_SHADER_BIT, 0, &barriers);

            // Graphics Render
            // Acquire the image
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
            att.clearValue.float32[0] = 0.0f;
            att.clearValue.float32[1] = 0.0f;
            att.clearValue.float32[2] = 0.0f;
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
            RHI_Cmd_BindPipeline(cmd, m_GraphicsPipeline);
            RHI_Cmd_SetViewport(cmd, 0, 0, (float)renderInfo.RHIRenderArea.width, (float)renderInfo.RHIRenderArea.height, 0, 1);
            RHI_Cmd_SetScissor(cmd, 0, 0, renderInfo.RHIRenderArea.width, renderInfo.RHIRenderArea.height);
            RHI_Cmd_BindDescriptorSets_FromPool(cmd, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPool, m_GraphicsDescriptorPoolIds[currentIndex]);
            RHI_Cmd_Draw(cmd, m_ParticleCount, 1, 0, 0, 0);
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

#pragma once

#include "../RHIRenderingTestBase.h"

namespace ArisenEngine::Testing
{
    class RHITessellationShaderTest : public RHIRenderingTestBase
    {
    private:
        struct TessellationUBO {
            glm::mat4 model;
            glm::mat4 view;
            glm::mat4 projection;
            float time;
            float tessLevel;
            float waveAmplitude;
            float waveFrequency;
        };

        RHI_PSOHandle m_Pso = nullptr;
        RHI_PSOHandle m_WireframePso = nullptr;
        RHI_PipelineHandle m_Pipeline = 0;
        RHI_PipelineHandle m_WireframePipeline = 0;
        
        Containers::Vector<RHI_BufferHandle> m_UboBuffers;
        RHI_SubpassHandle m_Subpass = 0;
        
        RHI_GPUProgramHandle m_HsProgram = 0;
        RHI_GPUProgramHandle m_DsProgram = 0;

        RHI_ImageHandle m_DepthImage = 0;
        RHI_ImageViewHandle m_DepthView = 0;

        float m_AccumulatedTime = 0.0f;
        bool m_ShowWireframe = true;

    public:
        const char* GetName() const override { return "TessellationShaderTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();
            
            auto shaderEnv = GetShaderEnvString();

            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            auto currentPath = exeDir.generic_wstring() + L"\\Shader";
            auto shaderPath = currentPath + L"\\TessellationShaderTest.hlsl";
            
            // VS
            HAL::ShaderCompileParams vsParams;
            vsParams.input = shaderPath;
            vsParams.entry = L"vs_main";
            vsParams.stage = RHI::Vertex;
            vsParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput vsOut;
            if (!HAL::CompileShaderFromFile(std::move(vsParams), vsOut) || vsOut.codeSize == 0) return false;
            
            RHI::RHIShaderProgramDesc vsDesc;
            vsDesc.byteCode = vsOut.codePointer;
            vsDesc.codeSize = vsOut.codeSize;
            vsDesc.stage = RHI::SHADER_STAGE_VERTEX_BIT;
            vsDesc.entry = "vs_main";
            vsDesc.name = "Tess_VS";
            m_VertProgram = RHI_Device_CreateGPUProgram(m_Device);
            RHI_Device_AttachProgramByteCode(m_Device, m_VertProgram, &vsDesc);
            if (vsOut.codePointer) std::free(vsOut.codePointer);

            // HS
            HAL::ShaderCompileParams hsParams;
            hsParams.input = shaderPath;
            hsParams.entry = L"hs_main";
            hsParams.stage = RHI::Hull;
            hsParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput hsOut;
            if (!HAL::CompileShaderFromFile(std::move(hsParams), hsOut) || hsOut.codeSize == 0) return false;

            RHI::RHIShaderProgramDesc hsDesc;
            hsDesc.byteCode = hsOut.codePointer;
            hsDesc.codeSize = hsOut.codeSize;
            hsDesc.stage = RHI::SHADER_STAGE_TESSELLATION_CONTROL_BIT;
            hsDesc.entry = "hs_main";
            hsDesc.name = "Tess_HS";
            m_HsProgram = RHI_Device_CreateGPUProgram(m_Device);
            RHI_Device_AttachProgramByteCode(m_Device, m_HsProgram, &hsDesc);
            if (hsOut.codePointer) std::free(hsOut.codePointer);

            // DS
            HAL::ShaderCompileParams dsParams;
            dsParams.input = shaderPath;
            dsParams.entry = L"ds_main";
            dsParams.stage = RHI::Domain;
            dsParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput dsOut;
            if (!HAL::CompileShaderFromFile(std::move(dsParams), dsOut) || dsOut.codeSize == 0) return false;

            RHI::RHIShaderProgramDesc dsDesc;
            dsDesc.byteCode = dsOut.codePointer;
            dsDesc.codeSize = dsOut.codeSize;
            dsDesc.stage = RHI::SHADER_STAGE_TESSELLATION_EVALUATION_BIT;
            dsDesc.entry = "ds_main";
            dsDesc.name = "Tess_DS";
            m_DsProgram = RHI_Device_CreateGPUProgram(m_Device);
            RHI_Device_AttachProgramByteCode(m_Device, m_DsProgram, &dsDesc);
            if (dsOut.codePointer) std::free(dsOut.codePointer);

            // PS
            HAL::ShaderCompileParams psParams;
            psParams.input = shaderPath;
            psParams.entry = L"ps_main";
            psParams.stage = RHI::Fragment;
            psParams.targetEnv = shaderEnv;
            HAL::ShaderCompilerOutput psOut;
            if (!HAL::CompileShaderFromFile(std::move(psParams), psOut) || psOut.codeSize == 0) return false;

            RHI::RHIShaderProgramDesc psDesc;
            psDesc.byteCode = psOut.codePointer;
            psDesc.codeSize = psOut.codeSize;
            psDesc.stage = RHI::SHADER_STAGE_FRAGMENT_BIT;
            psDesc.entry = "ps_main";
            psDesc.name = "Tess_PS";
            m_FragProgram = RHI_Device_CreateGPUProgram(m_Device);
            RHI_Device_AttachProgramByteCode(m_Device, m_FragProgram, &psDesc);
            if (psOut.codePointer) std::free(psOut.codePointer);

            CreateResources();
            InitRenderContext();
            CreatePipelines();

            return true;
        }

        void TeardownTest() override
        {
            if (m_DepthImage) RHI_Device_ReleaseImage(m_Device, m_DepthImage);
            for (auto& ub : m_UboBuffers) if (ub) RHI_Device_ReleaseBuffer(m_Device, ub);
            if (m_HsProgram) RHI_Device_ReleaseGPUProgram(m_Device, m_HsProgram);
            if (m_DsProgram) RHI_Device_ReleaseGPUProgram(m_Device, m_DsProgram);
            if (m_Pso) RHI_PSO_Release(m_Pso);
            if (m_WireframePso) RHI_PSO_Release(m_WireframePso);

            m_Model.Release(m_Device);
            RHIRenderingTestBase::TeardownTest();
        }

    protected:
        void RenderFrame() override
        {
            auto currentIndex = GetCurrentFrameIndex();
            if (m_FrameTickets[currentIndex] > 0) RHI_Device_WaitQueueTicket(m_Device, m_FrameTickets[currentIndex]);

            m_AccumulatedTime += (float)frameTime;
            UpdateUniformBuffer();
            RecordAndSubmit();

            NextFrame();
        }

    private:
        void CreateResources()
        {
            // Create a Grid of Patches (Quads)
            struct Vertex {
                glm::vec3 pos;
                glm::vec2 uv;
            };

            const int gridDim = 20;
            const float size = 10.0f;
            Containers::Vector<Vertex> vertices;
            Containers::Vector<UInt32> indices;

            for (int y = 0; y < gridDim; ++y) {
                for (int x = 0; x < gridDim; ++x) {
                    float xPos = (x / (float)gridDim) * size - size * 0.5f;
                    float yPos = (y / (float)gridDim) * size - size * 0.5f;
                    float step = size / (float)gridDim;

                    // 4 Control points for a quad patch
                    vertices.push_back({ {xPos, 0, yPos}, {x / (float)gridDim, y / (float)gridDim} });
                    vertices.push_back({ {xPos + step, 0, yPos}, {(x + 1) / (float)gridDim, y / (float)gridDim} });
                    vertices.push_back({ {xPos + step, 0, yPos + step}, {(x + 1) / (float)gridDim, (y + 1) / (float)gridDim} });
                    vertices.push_back({ {xPos, 0, yPos + step}, {x / (float)gridDim, (y + 1) / (float)gridDim} });

                    UInt32 base = (y * gridDim + x) * 4;
                    indices.push_back(base);
                    indices.push_back(base + 1);
                    indices.push_back(base + 2);
                    indices.push_back(base + 3);
                }
            }

            m_Model.vertexCount = (UInt32)vertices.size();
            m_Model.indexCount = (UInt32)indices.size();

            RHI::RHIBufferDescriptor vbDesc = {};
            vbDesc.size = vertices.size() * sizeof(Vertex);
            vbDesc.usage = RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT;
            vbDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_Model.vertexBuffer = RHI_Device_CreateBuffer(m_Device, &vbDesc, "PatchVB");
            RHI_Buffer_MemoryCopy(m_Device, m_Model.vertexBuffer, vertices.data(), vbDesc.size, 0);

            RHI::RHIBufferDescriptor ibDesc = {};
            ibDesc.size = indices.size() * sizeof(UInt32);
            ibDesc.usage = RHI::BUFFER_USAGE_INDEX_BUFFER_BIT;
            ibDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_Model.indexBuffer = RHI_Device_CreateBuffer(m_Device, &ibDesc, "PatchIB");
            RHI_Buffer_MemoryCopy(m_Device, m_Model.indexBuffer, indices.data(), ibDesc.size, 0);

            m_Model.layout.stride = sizeof(Vertex);
            m_Model.layout.attributes.push_back({ "pos", RHI::FORMAT_R32G32B32_SFLOAT, 0, 0 });
            m_Model.layout.attributes.push_back({ "uv", RHI::FORMAT_R32G32_SFLOAT, sizeof(glm::vec3), 1 });

            // UBOs
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(TessellationUBO);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_UboBuffers.push_back(RHI_Device_CreateBuffer(m_Device, &ubDesc, "TessUBO"));
            }

            UInt32 width = HAL::GetWindowWidth(m_WindowId);
            UInt32 height = HAL::GetWindowHeight(m_WindowId);

            // Depth Image
            RHI::RHIImageDescriptor dimgDesc = {};
            dimgDesc.imageType = RHI::IMAGE_TYPE_2D;
            dimgDesc.width = width; dimgDesc.height = height; dimgDesc.depth = 1;
            dimgDesc.mipLevels = 1; dimgDesc.arrayLayers = 1;
            dimgDesc.format = RHI::FORMAT_D32_SFLOAT;
            dimgDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
            dimgDesc.usage = RHI::IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT;
            dimgDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
            dimgDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            m_DepthImage = RHI_Device_CreateImage(m_Device, &dimgDesc, "DepthBuffer");

            RHI::RHIImageViewDesc dviewDesc = {};
            dviewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            dviewDesc.format = RHI::FORMAT_D32_SFLOAT;
            dviewDesc.aspectMask = RHI::IMAGE_ASPECT_DEPTH_BIT;
            dviewDesc.levelCount = 1; dviewDesc.layerCount = 1;
            dviewDesc.width = width; dviewDesc.height = height;
            m_DepthView = RHI_Image_AddImageView(m_Device, m_DepthImage, &dviewDesc);

            m_CameraPos = glm::vec3(0.0f, 5.0f, 10.0f);
        }

        void InitRenderContext()
        {
            RHI_RenderPass_AddAttachmentAction(m_Device, m_RenderPass, RHI::FORMAT_B8G8R8A8_SRGB, RHI::SAMPLE_COUNT_1_BIT, RHI::ATTACHMENT_LOAD_OP_CLEAR, RHI::ATTACHMENT_STORE_OP_STORE, RHI::ATTACHMENT_LOAD_OP_DONT_CARE, RHI::ATTACHMENT_STORE_OP_DONT_CARE, RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);
            RHI_RenderPass_AddAttachmentAction(m_Device, m_RenderPass, RHI::FORMAT_D32_SFLOAT, RHI::SAMPLE_COUNT_1_BIT, RHI::ATTACHMENT_LOAD_OP_CLEAR, RHI::ATTACHMENT_STORE_OP_DONT_CARE, RHI::ATTACHMENT_LOAD_OP_DONT_CARE, RHI::ATTACHMENT_STORE_OP_DONT_CARE, RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL);
            m_Subpass = RHI_RenderPass_AddSubPass(m_Device, m_RenderPass);
            RHI_Subpass_SetBindPoint(m_Subpass, RHI::PIPELINE_BIND_POINT_GRAPHICS);
            RHI_Subpass_AddColorReference(m_Subpass, 0, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);
            RHI_Subpass_SetDepthStencilReference(m_Subpass, 1, RHI::IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL);

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i) RHI_RenderPass_Alloc(m_Device, m_RenderPass, i);

            Containers::Vector<RHI::EDescriptorType> types = { RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER };
            Containers::Vector<UInt32> counts = { 1 };
            m_DescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, 1));
        }

        void CreatePipelines()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);
            m_Pso = RHI_PipelineManager_CreatePSO(pm);
            RHI_PSO_AddProgram(m_Pso, m_VertProgram);
            RHI_PSO_AddProgram(m_Pso, m_HsProgram);
            RHI_PSO_AddProgram(m_Pso, m_DsProgram);
            RHI_PSO_AddProgram(m_Pso, m_FragProgram);
            
            RHI_PSO_AddVertexBindingDescription(m_Pso, 0, m_Model.layout.stride, RHI::VERTEX_INPUT_RATE_VERTEX);
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 0, 0, RHI::FORMAT_R32G32B32_SFLOAT, 0);
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 1, 0, RHI::FORMAT_R32G32_SFLOAT, sizeof(glm::vec3));

            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_PATCH_LIST;
            RHI_PSO_SetInputAssemblyState(m_Pso, &ia);

            RHI::RHITessellationState ts{};
            ts.patchControlPoints = 4;
            RHI_PSO_SetTessellationState(m_Pso, &ts);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_NONE;
            rs.polygonMode = RHI::EPOLYGON_MODE_FILL;
            RHI_PSO_SetRasterizationState(m_Pso, &rs);

            RHI::RHIColorBlendState cb{};
            RHI::RHIColorBlendAttachmentState att{};
            att.blendEnable = false;
            att.colorWriteMask = 0xF;
            cb.attachments.push_back(att);
            RHI_PSO_SetColorBlendState(m_Pso, &cb);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);
            RHI_PSO_SetDynamicStateMask(m_Pso, RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);

            RHI::RHIDepthStencilState ds{};
            ds.depthTestEnable = true; ds.depthWriteEnable = true;
            ds.depthCompareOp = RHI::COMPARE_OP_LESS;
            RHI_PSO_SetDepthStencilState(m_Pso, &ds);

            m_Pipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_Pso);
            
            // Wireframe PSO
            m_WireframePso = RHI_PipelineManager_CreatePSO(pm);
            // Copy state from m_Pso - manually for now
            RHI_PSO_AddProgram(m_WireframePso, m_VertProgram);
            RHI_PSO_AddProgram(m_WireframePso, m_HsProgram);
            RHI_PSO_AddProgram(m_WireframePso, m_DsProgram);
            RHI_PSO_AddProgram(m_WireframePso, m_FragProgram);
            RHI_PSO_AddVertexBindingDescription(m_WireframePso, 0, m_Model.layout.stride, RHI::VERTEX_INPUT_RATE_VERTEX);
            RHI_PSO_AddVertexInputAttributeDescription(m_WireframePso, 0, 0, RHI::FORMAT_R32G32B32_SFLOAT, 0);
            RHI_PSO_AddVertexInputAttributeDescription(m_WireframePso, 1, 0, RHI::FORMAT_R32G32_SFLOAT, sizeof(glm::vec3));
            RHI_PSO_SetInputAssemblyState(m_WireframePso, &ia);
            RHI_PSO_SetTessellationState(m_WireframePso, &ts);

            rs.polygonMode = RHI::EPOLYGON_MODE_LINE;
            RHI_PSO_SetRasterizationState(m_WireframePso, &rs);

            RHI_PSO_SetColorBlendState(m_WireframePso, &cb);

            RHI_PSO_BuildDescriptorSetLayout(m_WireframePso);
            RHI_PSO_SetDynamicStateMask(m_WireframePso, RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);
            RHI_PSO_SetDepthStencilState(m_WireframePso, &ds);

            m_WireframePipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_WireframePso);

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i) {
                RHI_Pipeline_AllocGraphics(m_Device, m_Pipeline, i, m_Subpass);
                RHI_Pipeline_AllocGraphics(m_Device, m_WireframePipeline, i, m_Subpass);
            }
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            TessellationUBO ubo;
            ubo.model = glm::mat4(1.0f);
            ubo.view = GetViewMatrix();
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);
            ubo.projection = GetProjectionMatrix(width / height);
            ubo.time = m_AccumulatedTime;
            ubo.tessLevel = 32.0f; // Could be dynamic
            ubo.waveAmplitude = 0.5f;
            ubo.waveFrequency = 2.0f;
            
            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffers[GetCurrentFrameIndex()], &ubo, sizeof(TessellationUBO), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, currentIndex);

            RHI_DescriptorPool_Reset(m_DescriptorPool, m_DescriptorPoolIds[0]);
            
            RHI_Cmd_Begin(cmd, currentIndex, 0);

            auto colorBuffer = RHI_SwapChain_BeginFrame(m_SwapChain, currentIndex);
            
            if (colorBuffer)
            {
                auto colorView = RHI_SwapChain_GetImageView(m_SwapChain, currentIndex);
                RHI_RenderPass_Alloc(m_Device, m_RenderPass, currentIndex);
                RHI_FrameBuffer_SetAttachment(m_Device, m_FrameBuffer, currentIndex, colorView, m_RenderPass, 0);
                RHI_FrameBuffer_SetAttachment(m_Device, m_FrameBuffer, currentIndex, m_DepthView, m_RenderPass, 1);
                
                RHI::RHIClearValue clearValues[2];
                clearValues[0].color[0] = 0.05f; clearValues[0].color[1] = 0.05f; clearValues[0].color[2] = 0.1f; clearValues[0].color[3] = 1.0f;
                clearValues[1].depthStencil.depth = 1.0f; clearValues[1].depthStencil.stencil = 0;

                RHI::RenderPassBeginDesc rpBegin{};
                rpBegin.renderPass = *reinterpret_cast<RHI::RHIRenderPassHandle*>(&m_RenderPass);
                rpBegin.frameBuffer = *reinterpret_cast<RHI::RHIFrameBufferHandle*>(&m_FrameBuffer);
                rpBegin.subpassContents = RHI::SUBPASS_CONTENTS_INLINE;
                rpBegin.clearValueCount = 2;
                rpBegin.pClearValues = clearValues;

                RHI_Cmd_BeginRenderPass(cmd, &rpBegin);
                UInt32 width = HAL::GetWindowWidth(m_WindowId);
                UInt32 height = HAL::GetWindowHeight(m_WindowId);
                
                RHI_Cmd_BindPipeline(cmd, m_ShowWireframe ? m_WireframePipeline : m_Pipeline);
                RHI_Cmd_SetViewport(cmd, 0, 0, (float)width, (float)height, 0, 1);
                RHI_Cmd_SetScissor(cmd, 0, 0, width, height);
                
                RHI_Cmd_BindVertexBuffers(cmd, m_Model.vertexBuffer, 0);
                RHI_Cmd_BindIndexBuffer(cmd, m_Model.indexBuffer, 0, RHI::INDEX_TYPE_UINT32);

                Containers::Vector<RHI::RHIBufferHandle> ubos = { *reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffers[currentIndex]) };
                RHI_PSO_UpdateDescriptorSet_Buffers(m_ShowWireframe ? m_WireframePso : m_Pso, 0, 0, &ubos);

                UInt32 setIdx = RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[0], 0, m_ShowWireframe ? m_WireframePso : m_Pso);
                RHI_DescriptorPool_UpdateDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[0], setIdx, m_ShowWireframe ? m_WireframePso : m_Pso);

                RHI_Cmd_BindDescriptorSet_FromPool(cmd, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPool, m_DescriptorPoolIds[0], setIdx);
                RHI_Cmd_DrawIndexed(cmd, m_Model.indexCount, 1, 0, 0, 0, 0);

                RHI_Cmd_EndRenderPass(cmd);


            }

            RHI_Cmd_End(cmd);
            m_FrameTickets[currentIndex] = RHI_Device_Submit(m_Device, cmd, currentIndex);
            RHI_SwapChain_EndFrame(m_SwapChain, currentIndex);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, currentIndex, cmd);
        }
    };
}

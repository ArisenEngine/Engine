#pragma once

#include "../RHIRenderingTestBase.h"

namespace ArisenEngine::Testing
{
    class RHIGeometryShaderTest : public RHIRenderingTestBase
    {
    private:
        RHI_PSOHandle m_Pso = nullptr;
        RHI_PipelineHandle m_Pipeline = 0;
        Containers::Vector<RHI_BufferHandle> m_UboBuffers;
        RHI_SubpassHandle m_Subpass = 0;
        
        RHI_GPUProgramHandle m_GsProgram = 0;

        RHI_ImageHandle m_DepthImage = 0;
        RHI_ImageViewHandle m_DepthView = 0;

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
            if (m_DepthImage) RHI_Device_ReleaseImage(m_Device, m_DepthImage);
            for (auto& ub : m_UboBuffers)
            {
                if (ub) RHI_Device_ReleaseBuffer(m_Device, ub);
            }
            if (m_GsProgram) RHI_Device_ReleaseGPUProgram(m_Device, m_GsProgram);
            if (m_Pso) RHI_PSO_Destroy(m_Pso);

            m_Model.Release(m_Device);

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
            
            std::filesystem::path duckPath = exeDir / "Assets" / "glTF-Sample-Models" / "2.0" / "Duck" / "glTF" / "Duck.gltf";
            m_Model = LoadGLTF(duckPath.string());

            // UBOs
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(UniformBufferObject);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_UboBuffers.push_back(RHI_Device_CreateBuffer(m_Device, &ubDesc, "UBO"));
            }

            UInt32 width = HAL::GetWindowWidth(m_WindowId);
            UInt32 height = HAL::GetWindowHeight(m_WindowId);

            // Depth Image
            RHI::RHIImageDescriptor dimgDesc = {};
            dimgDesc.imageType = RHI::IMAGE_TYPE_2D;
            dimgDesc.width = width;
            dimgDesc.height = height;
            dimgDesc.depth = 1;
            dimgDesc.mipLevels = 1;
            dimgDesc.arrayLayers = 1;
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
            dviewDesc.levelCount = 1;
            dviewDesc.layerCount = 1;
            dviewDesc.width = width;
            dviewDesc.height = height;
            m_DepthView = RHI_Image_AddImageView(m_Device, m_DepthImage, &dviewDesc);

            m_CameraPos = glm::vec3(0.0f, 1.0f, 3.0f);
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

            // Depth attachment
            RHI_RenderPass_AddAttachmentAction(m_Device, m_RenderPass,
                RHI::FORMAT_D32_SFLOAT,
                RHI::SAMPLE_COUNT_1_BIT,
                RHI::ATTACHMENT_LOAD_OP_CLEAR,
                RHI::ATTACHMENT_STORE_OP_DONT_CARE,
                RHI::ATTACHMENT_LOAD_OP_DONT_CARE,
                RHI::ATTACHMENT_STORE_OP_DONT_CARE,
                RHI::IMAGE_LAYOUT_UNDEFINED,
                RHI::IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL);

            m_Subpass = RHI_RenderPass_AddSubPass(m_Device, m_RenderPass);
            RHI_Subpass_SetBindPoint(m_Subpass, RHI::PIPELINE_BIND_POINT_GRAPHICS);
            RHI_Subpass_AddColorReference(m_Subpass, 0, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);
            RHI_Subpass_SetDepthStencilReference(m_Subpass, 1, RHI::IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL);

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI_RenderPass_Alloc(m_Device, m_RenderPass, i);
            }

            Containers::Vector<RHI::EDescriptorType> types = { 
                RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                RHI::DESCRIPTOR_TYPE_SAMPLER
            };
            UInt32 matCount = (UInt32)m_Model.materials.size();
            if (matCount == 0) matCount = 1;
            Containers::Vector<UInt32> counts = { matCount, matCount, matCount };
            m_DescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, matCount));
        }

        void CreatePipeline()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);
            m_Pso = RHI_PipelineManager_CreatePSO(pm);

            RHI_PSO_AddProgram(m_Pso, m_VertProgram);
            RHI_PSO_AddProgram(m_Pso, m_GsProgram);
            RHI_PSO_AddProgram(m_Pso, m_FragProgram);

            RHI_PSO_AddVertexBindingDescription(m_Pso, 0, m_Model.layout.stride, RHI::VERTEX_INPUT_RATE_VERTEX);
            for (const auto& attr : m_Model.layout.attributes)
            {
                RHI_PSO_AddVertexInputAttributeDescription(m_Pso, attr.location, 0, attr.format, attr.offset);
            }

            RHI::RHIInputAssemblyState ia{};
            ia.topology = RHI::PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
            RHI_PSO_SetInputAssemblyState(m_Pso, &ia);

            RHI::RHIRasterizationState rs{};
            rs.cullMode = RHI::CULL_MODE_NONE;
            RHI_PSO_SetRasterizationState(m_Pso, &rs);

            RHI::RHIColorBlendState cb{};
            RHI::RHIColorBlendAttachmentState att{};
            att.blendEnable = false;
            att.colorWriteMask = RHI::COLOR_COMPONENT_R_BIT | RHI::COLOR_COMPONENT_G_BIT | RHI::COLOR_COMPONENT_B_BIT | RHI::COLOR_COMPONENT_A_BIT;
            cb.attachments.push_back(att);
            RHI_PSO_SetColorBlendState(m_Pso, &cb);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);
            RHI_PSO_SetDynamicStateMask(m_Pso, RHI::DYNAMIC_STATE_VIEWPORT_BIT | RHI::DYNAMIC_STATE_SCISSOR_BIT);

            RHI::RHIDepthStencilState ds{};
            ds.depthTestEnable = true;
            ds.depthWriteEnable = true;
            ds.depthCompareOp = RHI::COMPARE_OP_LESS;
            RHI_PSO_SetDepthStencilState(m_Pso, &ds);

            m_Pipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_Pso);
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i) {
                RHI_Pipeline_AllocGraphics(m_Device, m_Pipeline, i, m_Subpass);
            }
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            UniformBufferObject ubo;
            ubo.model = glm::mat4(1.0f); // Disabled rotation
            ubo.view = GetViewMatrix();
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);
            ubo.projection = GetProjectionMatrix(width / height);
            ubo.mipmapBias = 0.0f;
            
            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffers[GetCurrentFrameIndex()], &ubo, sizeof(UniformBufferObject), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, currentIndex);

            RHI_DescriptorPool_Reset(m_DescriptorPool, m_DescriptorPoolIds[0]);
            
            RHI_Cmd_Begin(cmd, currentIndex, 0);

            auto surface = RHI_Instance_GetSurface(m_Instance, m_WindowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            auto colorBuffer = RHI_SwapChain_AquireCurrentImage(swapchain, currentIndex);
            
            if (colorBuffer)
            {
                auto colorView = RHI_SwapChain_GetImageView(swapchain, currentIndex);
                RHI_RenderPass_Alloc(m_Device, m_RenderPass, currentIndex);
                RHI_FrameBuffer_SetAttachment(m_Device, m_FrameBuffer, currentIndex, colorView, m_RenderPass, 0);
                RHI_FrameBuffer_SetAttachment(m_Device, m_FrameBuffer, currentIndex, m_DepthView, m_RenderPass, 1);
                
                RHI::RHIClearValue clearValues[2];
                clearValues[0].color[0] = 0.1f;
                clearValues[0].color[1] = 0.1f;
                clearValues[0].color[2] = 0.1f;
                clearValues[0].color[3] = 1.0f;
                clearValues[1].depthStencil.depth = 1.0f;
                clearValues[1].depthStencil.stencil = 0;

                RHI::RenderPassBeginDesc rpBegin{};
                rpBegin.renderPass = *reinterpret_cast<RHI::RHIRenderPassHandle*>(&m_RenderPass);
                rpBegin.frameBuffer = *reinterpret_cast<RHI::RHIFrameBufferHandle*>(&m_FrameBuffer);
                rpBegin.subpassContents = RHI::SUBPASS_CONTENTS_INLINE;
                rpBegin.clearValueCount = 2;
                rpBegin.pClearValues = clearValues;

                RHI_Cmd_BeginRenderPass(cmd, currentIndex, &rpBegin);
                UInt32 width = HAL::GetWindowWidth(m_WindowId);
                UInt32 height = HAL::GetWindowHeight(m_WindowId);
                RHI_Cmd_BindPipeline(cmd, currentIndex, m_Pipeline);
                RHI_Cmd_SetViewport(cmd, 0, 0, (float)width, (float)height, 0, 1);
                RHI_Cmd_SetScissor(cmd, 0, 0, width, height);
                
                RHI_Cmd_BindVertexBuffers(cmd, m_Model.vertexBuffer, 0);
                RHI_Cmd_BindIndexBuffer(cmd, m_Model.indexBuffer, 0, RHI::INDEX_TYPE_UINT32);

                for (const auto& prim : m_Model.primitives)
                {
                    auto& mat = m_Model.materials[prim.materialIndex >= 0 ? prim.materialIndex : 0];
                    
                    Containers::Vector<RHI::RHIBufferHandle> ubos = { *reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffers[currentIndex]) };
                    RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 0, &ubos);

                    RHI::RHIDescriptorImageInfo texInfo = {};
                    texInfo.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&mat.baseColorView);
                    texInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                    Containers::Vector<RHI::RHIDescriptorImageInfo> texInfos = { texInfo };
                    RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 1, &texInfos);

                    RHI::RHIDescriptorImageInfo samInfo = {};
                    samInfo.sampler = *reinterpret_cast<RHI::RHISamplerHandle*>(&mat.sampler);
                    samInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                    Containers::Vector<RHI::RHIDescriptorImageInfo> samInfos = { samInfo };
                    RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 2, &samInfos);

                    UInt32 setIdx = RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[0], 0, m_Pso);
                    RHI_DescriptorPool_UpdateDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[0], setIdx, m_Pso);

                    RHI_Cmd_BindDescriptorSet_FromPool(cmd, currentIndex, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPool, m_DescriptorPoolIds[0], setIdx);
                    RHI_Cmd_DrawIndexed(cmd, prim.indexCount, 1, prim.firstIndex, 0, 0, 0);
                }

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

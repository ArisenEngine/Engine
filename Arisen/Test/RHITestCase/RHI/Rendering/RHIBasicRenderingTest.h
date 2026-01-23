#pragma once

#include "../RHITestBase.h"
#include <chrono>
#include <iostream>

// RHI Includes
#include "RHI/Enums/Pipeline/EAccessFlag.h"
#include "RHI/Enums/Memory/EBufferUsage.h"
#include "RHI/Enums/Pipeline/EColorComponentFlag.h"
#include "RHI/Enums/Pipeline/ECommandBufferUsageFlagBits.h"
#include "RHI/Enums/Pipeline/EIndexType.h"
#include "RHI/Enums/Attachment/AttachmentLoadOp.h"
#include "RHI/Enums/Attachment/AttachmentStoreOp.h"
#include "RHI/Enums/Image/EImageAspectFlagBits.h"
#include "RHI/Enums/Subpass/ESubpassContents.h"
#include "RHI/Surfaces/Surface.h"
#include "RHI/Surfaces/FrameBuffer.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/RHICommon.h"
#include "RHI/Synchronization/RHIImageMemoryBarrier.h"
#include "RHI/CommandBuffer/RHICommandBuffer.h"
#include "RHI/CommandBuffer/RHICommandBufferPool.h"
#include "RHI/Program/GPUPipelineManager.h"
#include "RHI/Program/GPURenderPass.h"
#include "RHI/Program/GPUSubPass.h"
#include "RHI/Program/GPUPipelineStateObject.h"

// Engine Exports
#include "../../Engine/NativeEngine/RHI/RHIExports.h"
#include "../../Engine/NativeEngine/RHI/InstanceExports.h"
#include "../../Engine/NativeEngine/RHI/DeviceExports.h"
#include "../../Engine/NativeEngine/RHI/SurfaceExports.h"
#include "../../Engine/NativeEngine/RHI/HandlesExports.h"
#include "../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../Engine/NativeEngine/RHI/PipelineExports.h"
#include "../../Engine/NativeEngine/RHI/DescriptorExports.h"
#include "../../Engine/NativeEngine/RHI/SyncExports.h"
#include "ShaderCompiler/ShaderCompilerAPI.h"

// Third Party
#define GLM_FORCE_RADIANS
#include <glm/glm.hpp>
#include <glm/gtc/matrix_transform.hpp>
#include <cstdlib>
#include "stb_image.h"
#include "vulkan_core.h"

using namespace ArisenEngine;

namespace ArisenEngine::Testing
{
    class RHIBasicRenderingTest : public RHITestBase
    {
    public:
        using RHIGpuTicket = ArisenEngine::UInt64;
    private:
        struct RenderContext
        {
            UInt32 windowId;
            UInt32 newWidth;
            UInt32 newHeight;
            RHI_DeviceHandle device;
            RHI_RenderPassHandle renderPass;
            RHI_FrameBufferHandle frameBuffer;
            RHI_BufferHandle vertexBufferHandle;
            RHI_BufferHandle indicesBufferHandle;
            Containers::Vector<RHI_BufferHandle> uniformBuffers;
            RHI_ImageHandle textureHandle;
            RHI_CommandBufferPoolHandle commandPool;
            RHI_DescriptorPoolHandle descriptorPool;
            RHI_SubpassHandle subpass;
            RHI_PSOHandle pipelineState;
            RHI_PipelineHandle pipeline;
            Containers::Vector<RHI_GPUProgramHandle> gpuPrograms;
            Containers::Vector<UInt32> descriptorPoolIds;
            Containers::Vector<RHIGpuTicket> frameTickets;
            bool bShouldResize;
        };

        struct Vertex
        {
            glm::vec2 pos;
            glm::vec3 color;
        };

        struct UniformBufferObject
        {
            alignas(16) glm::mat4 model;
            alignas(16) glm::mat4 view;
            alignas(16) glm::mat4 proj;
        };

        RenderContext m_Context{};
        
        // Data
        const std::vector<Vertex> vertices = {
            {{-0.5f, -0.5f}, {1.0f, 0.0f, 0.0f}},
            {{0.5f, -0.5f}, {0.0f, 1.0f, 0.0f}},
            {{0.5f, 0.5f}, {0.0f, 0.0f, 1.0f}},
            {{-0.5f, 0.5f}, {1.0f, 1.0f, 1.0f}}
        };

        const std::vector<uint16_t> indices = {
            0, 1, 2, 2, 3, 0
        };

        // Timing
        using Clock = std::chrono::high_resolution_clock;
        Clock::time_point lastTime = Clock::now();
        double frameTime = 0.0;
        double fps = 0.0;
        Float32 s_FrameTimeSpacing = 0.0;

    public:
        const char* GetName() const override { return "BasicRenderingTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            // Helper to initialize context from base class resources
            m_Context.windowId = m_WindowId;
            // Get initial size - assuming 640x480 as per base default
            m_Context.newWidth = 640; 
            m_Context.newHeight = 480;
            m_Context.device = this->m_Device;
            m_Context.bShouldResize = false;

            InitRenderContext();
            Platforms::InitDXC();
            InitShaderProgram();
            InitPipelineStates();
            InitBuffer();
            CreateImage();
            
            return true;
        }

        bool Run() override
        {
            MSG msg{};
            bool isRunning = true;
            lastTime = Clock::now();

            while (isRunning)
            {
                while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE))
                {
                    TranslateMessage(&msg);
                    DispatchMessage(&msg);
                    if (msg.message == WM_QUIT)
                    {
                        isRunning = false;
                    }
                }

                if (!isRunning) break;

                RenderFrame();
            }

            return true;
        }

        void TeardownTest() override
        {
            // Wait for device before destruction
            RHI_Device_WaitIdle(this->m_Device);
            
            // Resources are largely managed by RHI handles or will be cleaned up when Device/Instance is destroyed in base
            // But we should clean up what we explicitly allocated if needed.
            // In the legacy code, Shutdown() was empty or relied on destructors.
            // RHI handles often need explicit release if not strictly RAII wrapped. 
            // Explicitly release resources held by global KeepAlive maps in FFI layer
            if (m_Context.frameBuffer)
            {
                RHI_Device_ReleaseFrameBuffer(m_Context.device, m_Context.frameBuffer);
                m_Context.frameBuffer = 0ULL;
            }
          
            if (m_Context.frameBuffer)
            {
                RHI_Device_ReleaseFrameBuffer(m_Context.device, m_Context.frameBuffer);
                m_Context.frameBuffer = 0ULL;
            }
            
            if (m_Context.renderPass)
            {
                RHI_Device_ReleaseRenderPass(m_Context.device, m_Context.renderPass);
                m_Context.renderPass = 0ULL;
            }

            // Release Buffers
            if (m_Context.vertexBufferHandle)
            {
                RHI_Device_ReleaseBuffer(m_Context.device, m_Context.vertexBufferHandle);
                m_Context.vertexBufferHandle = 0ULL;
            }
            if (m_Context.indicesBufferHandle)
            {
                RHI_Device_ReleaseBuffer(m_Context.device, m_Context.indicesBufferHandle);
                m_Context.indicesBufferHandle = 0ULL;
            }
            for (auto& ub : m_Context.uniformBuffers)
            {
                if (ub) RHI_Device_ReleaseBuffer(m_Context.device, ub);
            }
            m_Context.uniformBuffers.clear();

            // Release Texture
            if (m_Context.textureHandle)
            {
                RHI_Device_ReleaseImage(m_Context.device, m_Context.textureHandle);
                m_Context.textureHandle = 0ULL;
            }

            // Release Pipeline State (This destroys VK Descriptor Set Layouts)
            if (m_Context.pipelineState)
            {
                RHI_PSO_Destroy(m_Context.pipelineState);
                m_Context.pipelineState = nullptr;
            }
            
            // Release Programs
            for (auto& program : m_Context.gpuPrograms)
            {
                if (program)
                    RHI_Device_ReleaseGPUProgram(m_Context.device, program);
            }
            m_Context.gpuPrograms.clear();

            // Release Command Pool
            if (m_Context.commandPool)
            {
                RHI_Device_ReleaseCommandBufferPool(m_Context.device, m_Context.commandPool);
                m_Context.commandPool = 0;
            }

            // Note: DescriptorPool is seemingly owned by Device or not exposed for explicit release in Exports.
            // Assuming Device destruction handles it or it's a non-owning handle.
            // Update: DescriptorExports has no Release/Destroy for pool.

        }

    private:
        void RenderFrame()
        {
            // Wait for the previous submission of this frame index to complete
            if (m_Context.frameTickets.size() > m_FrameIndex)
            {
                RHI_Device_WaitQueueTicket(m_Context.device, m_Context.frameTickets[m_FrameIndex]);
            }
            // RHI_Device_WaitFrameFence(m_Context.device, m_FrameIndex);  <-- Removed
            UploadUniformBuffer(m_Context);
            RecordSubmitPresent(m_Context);
        
            if (m_Context.bShouldResize)
            {
                RHI_Device_SetResolution(m_Context.device, m_Context.newWidth, m_Context.newHeight);
                m_Context.bShouldResize = false;
            }

            NextFrame(); // Increment m_FrameIndex

            // FPS Calculation
            auto currentTime = Clock::now();
            std::chrono::duration<double> delta = currentTime - lastTime;
            lastTime = currentTime;

            frameTime = delta.count();
            fps = (1.0 / frameTime) * 0.1 + fps * 0.9;
            s_FrameTimeSpacing += (Float32)frameTime;
            if (s_FrameTimeSpacing >= 1.0)
            {
                s_FrameTimeSpacing = 0.0;
                std::cout << "FPS:" << fps << ", Delta Time:"<< frameTime << std::endl;
            }
        }

        void InitRenderContext()
        {
            m_Context.commandPool = RHI_Device_CreateCommandBufferPool(m_Context.device);
            m_Context.renderPass = RHI_Device_CreateRenderPass(m_Context.device);
            
            // Configure RenderPass
            RHI_RenderPass_AddAttachmentAction(m_Context.device, m_Context.renderPass, 
                RHI::EFormat::FORMAT_B8G8R8A8_SRGB,
                RHI::SAMPLE_COUNT_1_BIT,
                RHI::ATTACHMENT_LOAD_OP_CLEAR,
                RHI::ATTACHMENT_STORE_OP_STORE,
                RHI::ATTACHMENT_LOAD_OP_DONT_CARE,
                RHI::ATTACHMENT_STORE_OP_DONT_CARE,
                RHI::IMAGE_LAYOUT_UNDEFINED,
                RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);

            auto subpass = RHI_RenderPass_AddSubPass(m_Context.device, m_Context.renderPass);
            RHI_Subpass_SetBindPoint(subpass, RHI::PIPELINE_BIND_POINT_GRAPHICS);
            RHI_Subpass_AddColorReference(subpass, 0, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);
            RHI_Subpass_SetDependency(subpass, VK_SUBPASS_EXTERNAL, 
                RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, 0,
                RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT, 0);
            m_Context.subpass = subpass;

            // Allocate RenderPass for all frames
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI_RenderPass_Alloc(m_Context.device, m_Context.renderPass, i);
            }

            m_Context.frameBuffer = RHI_Device_GetFrameBuffer(m_Context.device);
            m_Context.descriptorPool = RHI_Device_GetDescriptorPool(m_Context.device);
            m_Context.pipelineState = nullptr;
            
            for(int i = 0; i < (int)m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types { RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER };
                Containers::Vector<unsigned int> counts { 1 };
                unsigned int poolId = RHI_DescriptorPool_AddPool(m_Context.descriptorPool, &types, &counts, 1);
                m_Context.descriptorPoolIds.emplace_back(poolId);
                m_Context.frameTickets.emplace_back(0); // Init ticket to 0
            }
        }

        void InitPipelineStates()
        {
            auto pipelineManager = RHI_Device_GetPipelineManager(m_Context.device);
            m_Context.pipelineState = RHI_PipelineManager_CreatePSO(pipelineManager);

            auto pipelineState = m_Context.pipelineState;
            RHI_PSO_AddVertexBindingDescription(pipelineState, 0, sizeof(Vertex), RHI::VERTEX_INPUT_RATE_VERTEX);
            RHI_PSO_AddVertexInputAttributeDescription(pipelineState, 0, 0, RHI::EFormat::FORMAT_R32G32_SFLOAT, offsetof(Vertex, pos));
            RHI_PSO_AddVertexInputAttributeDescription(pipelineState, 1, 0, RHI::EFormat::FORMAT_R32G32B32_SFLOAT, offsetof(Vertex, color));

            for (auto program : m_Context.gpuPrograms)
            {
                RHI_PSO_AddProgram(pipelineState, program);
            }
            RHI_PSO_BuildDescriptorSetLayout(pipelineState);

            RHI_PSO_AddDynamicState(pipelineState, RHI::DYNAMIC_STATE_SCISSOR);
            RHI_PSO_AddDynamicState(pipelineState, RHI::DYNAMIC_STATE_VIEWPORT);
            RHI_PSO_SetPrimitiveState(pipelineState, RHI::PRIMITIVE_TOPOLOGY_TRIANGLE_LIST, false);
            RHI_PSO_SetDepthClampEnable(pipelineState, false);
            RHI_PSO_SetRasterizerDiscardEnable(pipelineState, false);
            RHI_PSO_SetPolygonMode(pipelineState, RHI::EPOLYGON_MODE_FILL);
            RHI_PSO_SetLineWidth(pipelineState, 1.0F);
            RHI_PSO_SetCullMode(pipelineState, RHI::CULL_MODE_NONE);
            RHI_PSO_SetFrontFace(pipelineState, RHI::FRONT_FACE_CLOCKWISE);
            RHI_PSO_SetDepthBiasEnable(pipelineState, false);
            RHI_PSO_SetSampleShading(pipelineState, false);
            RHI_PSO_SetSampleCount(pipelineState, RHI::SAMPLE_COUNT_1_BIT); // Fixed typo from SAMPLE_COUNT_1_BIT
            RHI_PSO_AddBlendAttachmentState_Simple(pipelineState, false,
                                                   RHI::EColorComponentFlagBits::COLOR_COMPONENT_R_BIT |
                                                   RHI::EColorComponentFlagBits::COLOR_COMPONENT_G_BIT |
                                                   RHI::EColorComponentFlagBits::COLOR_COMPONENT_B_BIT |
                                                   RHI::EColorComponentFlagBits::COLOR_COMPONENT_A_BIT);
            RHI_PSO_SetLogicOp(pipelineState, false, RHI::LOGIC_OP_COPY);
            RHI_PSO_SetBlendConstants(pipelineState, 0.0f, 0.0f, 0.0f, 0.0f);

            // Pre-allocate graphics pipeline for all frames to avoid per-frame recreation overhead
            m_Context.pipeline = RHI_PipelineManager_GetGraphicsPipeline(pipelineManager, pipelineState);
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI_Pipeline_AllocGraphics(m_Context.device, m_Context.pipeline, i, m_Context.subpass);
            }
        }

        void InitShaderProgram()
        {
            // Retrieve environment string from instance
            std::wstring envStr;
            {
                unsigned int len = RHI_Instance_GetEnvStringW(this->m_Instance, nullptr, 0);
                if (len > 0)
                {
                    std::wstring tmp;
                    tmp.resize(len ? (len - 1) : 0);
                    if (len > 1)
                    {
                        RHI_Instance_GetEnvStringW(this->m_Instance, tmp.data(), len);
                    }
                    envStr = std::move(tmp);
                }
            }
            
            auto shaderFileName = L"UniformBuffers";
            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            auto currentPath = exeDir.generic_wstring() + L"\\Shader";
            auto path = currentPath + L"\\" + shaderFileName + L".hlsl";

            // Vertex Shader
            Platforms::ShaderCompileParams vertexParams
            {
                path,
                L"Vert",
                L"6_0",
                L"-spirv",
                envStr,
                L"0",
                RHI::ProgramStage::Vertex,
                {},
                {},
                currentPath + L"\\"+ shaderFileName + L".vert.spirv",
                true
            };

            Platforms::ShaderCompilerOutput outputVertex;
            if (!Platforms::CompileShaderFromFile(std::move(vertexParams), outputVertex) || outputVertex.codePointer == nullptr || outputVertex.codeSize == 0)
            {
                LOG_ERROR("Vertex shader compilation failed.");
                throw std::exception("Vertex shader compilation failed.");
            }
            LOG_DEBUG("Vertex Shader Compilation done.");

            {
                auto program = RHI_Device_CreateGPUProgram(m_Context.device);
                std::string nameStr = String::WStringToString(path);
                auto desc = RHI::GPUProgramDesc
                {
                    outputVertex.codeSize,
                    outputVertex.codePointer,
                    "Vert",
                    nameStr.c_str(),
                    RHI::SHADER_STAGE_VERTEX_BIT
                };
                RHI_Device_AttachProgramByteCode(m_Context.device, program, &desc);
                m_Context.gpuPrograms.emplace_back(program);
            }
            if (outputVertex.codePointer)
            {
                std::free(outputVertex.codePointer);
            }

            // Fragment Shader
            Platforms::ShaderCompileParams fragmentParams
            {
                path,
                L"Frag",
                L"6_0",
                L"-spirv",
                envStr,
                L"0",
                RHI::ProgramStage::Fragment,
                {},
                {},
                currentPath + L"\\" + shaderFileName + L".frag.spirv",
                true
            };

            Platforms::ShaderCompilerOutput outputfragment;
            if (!Platforms::CompileShaderFromFile(std::move(fragmentParams), outputfragment) || outputfragment.codePointer == nullptr || outputfragment.codeSize == 0)
            {
                LOG_ERROR("Fragment shader compilation failed.");
                throw std::exception("Fragment shader compilation failed.");
            }
            LOG_DEBUG("Fragment Shader Compilation done.");

            {
                auto program = RHI_Device_CreateGPUProgram(m_Context.device);
                std::string nameStr = String::WStringToString(path);
                auto desc = RHI::GPUProgramDesc
                {
                    outputfragment.codeSize,
                    outputfragment.codePointer,
                    "Frag",
                    nameStr.c_str(),
                    RHI::SHADER_STAGE_FRAGMENT_BIT
                };
                RHI_Device_AttachProgramByteCode(m_Context.device, program, &desc);
                m_Context.gpuPrograms.emplace_back(program);
            }
            if (outputfragment.codePointer)
            {
                std::free(outputfragment.codePointer);
            }
        }

        void InitBuffer()
        {
            RHI::BufferDescriptor vbDesc{
                0,
                sizeof(vertices[0]) * (UInt64)vertices.size(),
                RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT,
                RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT
            };
            m_Context.vertexBufferHandle = RHI_Device_CreateBuffer(m_Context.device, &vbDesc, "Vertex Buffer");

            RHI::BufferDescriptor ibDesc{
                0,
                sizeof(indices[0]) * (UInt64)indices.size(),
                RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_INDEX_BUFFER_BIT,
                RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT
            };
            m_Context.indicesBufferHandle = RHI_Device_CreateBuffer(m_Context.device, &ibDesc, "Indices Buffer");

            for (int i = 0; i < (int)m_MaxFramesInFlight; ++i)
            {
                RHI::BufferDescriptor ubDesc{
                    0,
                    sizeof(UniformBufferObject),
                    RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT,
                    RHI::SHARING_MODE_EXCLUSIVE,
                    0, nullptr,
                    RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT
                };
                auto name = std::string("Uniform Buffer ") + std::to_string(i);
                m_Context.uniformBuffers.emplace_back(RHI_Device_CreateBuffer(m_Context.device, &ubDesc, name.c_str()));
            }
            
            UploadVertex();
        }

        void CreateImage()
        {
            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            const fs::path exeDir = fs::path(exePathW).parent_path();
            const fs::path assetPath = exeDir / "Assets" / "Arisen.png";

            int texWidth = 0, texHeight = 0, texChannels = 0;
            stbi_uc* pixels = stbi_load(assetPath.string().c_str(),
                &texWidth, &texHeight, &texChannels, STBI_rgb_alpha);

            if (!pixels || texWidth <= 0 || texHeight <= 0)
            {
                std::string msg = "Failed to load texture image: " + assetPath.string();
                LOG_ERROR(msg.c_str());
                throw std::exception(msg.c_str());
            }

            const UInt64 imageSize = static_cast<UInt64>(texWidth) * static_cast<UInt64>(texHeight) * 4ull;

            RHI::ImageDescriptor imgDesc{
                RHI::IMAGE_TYPE_2D, static_cast<UInt32>(texWidth), static_cast<UInt32>(texHeight), 1,
                1, 1, RHI::FORMAT_R8G8B8A8_SRGB, RHI::IMAGE_TILING_OPTIMAL,
                RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_USAGE_SAMPLED_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT,
                RHI::SAMPLE_COUNT_1_BIT, RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT
            };
            m_Context.textureHandle = RHI_Device_CreateImage(m_Context.device, &imgDesc, "Texture Image");
            RHI::ImageViewDesc imageViewDesc {
                RHI::IMAGE_VIEW_TYPE_2D, RHI::FORMAT_R8G8B8A8_SRGB, RHI::IMAGE_ASPECT_COLOR_BIT,
                0, 1, 0, 1,
            };
            imageViewDesc.width = static_cast<UInt32>(texWidth);
            imageViewDesc.height = static_cast<UInt32>(texHeight);
            RHI_Image_AddImageView(m_Context.device, m_Context.textureHandle, &imageViewDesc);
            UploadImage(imageSize, pixels, texWidth, texHeight);
            
            stbi_image_free(pixels);
        }

        void UploadVertex()
        {
            auto device = m_Context.device;
            auto vertexBufferHandle = m_Context.vertexBufferHandle;
            auto indicesBufferHandle = m_Context.indicesBufferHandle;
            
            RHI::BufferDescriptor vsb{
                0,
                sizeof(vertices[0]) * vertices.size(),
                RHI::BUFFER_USAGE_TRANSFER_SRC_BIT,
                RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT
            };
            auto vertexStagingBufferHandle = RHI_Device_CreateBuffer(device, &vsb, "Vertex Staging Buffer");
            RHI_Buffer_MemoryCopy(device, vertexStagingBufferHandle, vertices.data(), 0);

            RHI::BufferDescriptor isb{
                0,
                sizeof(indices[0]) * indices.size(),
                RHI::BUFFER_USAGE_TRANSFER_SRC_BIT,
                RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT
            };
            auto indicesStagingBufferHandle = RHI_Device_CreateBuffer(device, &isb, "Indices Staging Buffer");
            RHI_Buffer_MemoryCopy(device, indicesStagingBufferHandle, indices.data(), 0);

            auto commandBuffer = RHI_Device_GetCommandBuffer(device, m_Context.commandPool, m_FrameIndex);
            RHI_Cmd_Begin(commandBuffer, m_FrameIndex, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            RHI_Cmd_CopyBuffer(commandBuffer, vertexStagingBufferHandle, 0, vertexBufferHandle, 0, RHI_Buffer_Size(device, vertexBufferHandle));
            RHI_Cmd_CopyBuffer(commandBuffer, indicesStagingBufferHandle, 0, indicesBufferHandle, 0, RHI_Buffer_Size(device, indicesBufferHandle));
            
            RHI_Cmd_End(commandBuffer);
            RHI_Device_Submit(device, commandBuffer, m_FrameIndex);
            
            // Sync one-time setup transfers immediately to avoid command buffer reuse conflicts with first frame
            RHI_Device_WaitIdle(device);

            RHI_Device_ReleaseBuffer(device, vertexStagingBufferHandle);
            RHI_Device_ReleaseBuffer(device, indicesStagingBufferHandle);

            RHI_Device_ReleaseCommandBuffer(device, m_Context.commandPool, m_FrameIndex, commandBuffer);
        }

        void UploadImage(UInt64 textureSize, void* data, UInt32 texWidth, UInt32 texHeight)
        {
            auto device = m_Context.device;
            RHI::BufferDescriptor tsb{
                0,
                textureSize,
                RHI::BUFFER_USAGE_TRANSFER_SRC_BIT,
                RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT
            };
            auto textureStagingBufferHandle = RHI_Device_CreateBuffer(device, &tsb, "Texture Staging Buffer");
            RHI_Buffer_MemoryCopy(device, textureStagingBufferHandle, data, 0);

            // Transfer commands
            auto commandBuffer = RHI_Device_GetCommandBuffer(device, m_Context.commandPool, m_FrameIndex);
            RHI_Cmd_Begin(commandBuffer, m_FrameIndex, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            {
                Containers::Vector<RHI::RHIImageMemoryBarrier> barriers {
                    {
                        RHI::ACCESS_NONE,
                        RHI::ACCESS_TRANSFER_WRITE_BIT,
                        RHI::IMAGE_LAYOUT_UNDEFINED,
                        RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                        VK_QUEUE_FAMILY_IGNORED,
                        VK_QUEUE_FAMILY_IGNORED,
                        *reinterpret_cast<RHI::RHIImageHandle*>(&m_Context.textureHandle),
                        {
                            RHI::IMAGE_ASPECT_COLOR_BIT,
                            0, 1, 0, 1
                        }
                    }
                };
                RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_TRANSFER_BIT, 0, &barriers);
            }

            {
                ArisenEngine::Containers::Vector<RHI::BufferImageCopy> regions{
                    { 0, 0, 0, { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 }, 0, 0, 0, texWidth, texHeight, 1 }
                };
                RHI_Cmd_CopyBufferToImage(commandBuffer, textureStagingBufferHandle, m_Context.textureHandle,
                    RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, &regions);
            }

            {
                 Containers::Vector<RHI::RHIImageMemoryBarrier> barriers{
                    {
                        RHI::ACCESS_TRANSFER_WRITE_BIT,
                        RHI::ACCESS_SHADER_READ_BIT,
                        RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                        RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                        ~0U,
                        ~0U,
                        *reinterpret_cast<RHI::RHIImageHandle*>(&m_Context.textureHandle),
                        {
                            RHI::IMAGE_ASPECT_COLOR_BIT,
                            0, 1, 0, 1
                        }
                    }
                };
                RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_TRANSFER_BIT, RHI::PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, &barriers);
            }

            RHI_Cmd_End(commandBuffer);
            RHI_Device_Submit(device, commandBuffer, m_FrameIndex);

            // Sync one-time setup transfers immediately to avoid command buffer reuse conflicts with first frame
            RHI_Device_WaitIdle(device);

            RHI_Device_ReleaseBuffer(device, textureStagingBufferHandle);
            RHI_Device_ReleaseCommandBuffer(device, m_Context.commandPool, m_FrameIndex, commandBuffer);
        }

        void UploadUniformBuffer(RenderContext const& context)
        {
            static auto startTime = std::chrono::high_resolution_clock::now();

            auto currentTime = std::chrono::high_resolution_clock::now();
            float time = std::chrono::duration<float, std::chrono::seconds::period>(currentTime - startTime).count();
            
            UniformBufferObject ubo{};
            ubo.model = glm::rotate(glm::mat4(1.0f), time * glm::radians(90.0f), glm::vec3(0.0f, 0.0f, 1.0f));
            ubo.view = glm::lookAt(glm::vec3(2.0f, 2.0f, 2.0f), glm::vec3(0.0f, 0.0f, 0.0f),
                glm::vec3(0.0f, 0.0f, 1.0f));
            ubo.proj = glm::perspective(glm::radians(45.0f),
                context.newWidth / (float) context.newHeight, 0.1f, 10.0f);
            ubo.proj[1][1] *= -1;

            auto currentIndex = GetCurrentFrameIndex();
            RHI_Buffer_MemoryCopy(context.device, context.uniformBuffers[currentIndex], &ubo, 0);
        }

        void RecordSubmitPresent(RenderContext& context)
        {
            auto currentIndex = GetCurrentFrameIndex();
            
            auto commandBuffer = RHI_Device_GetCommandBuffer(context.device, context.commandPool, m_FrameIndex);
            
            auto pipelineState = context.pipelineState;
            Containers::Vector<RHI::RHIBufferHandle> ubos;
            auto rawHandle = context.uniformBuffers[currentIndex];
            auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&rawHandle);
            ubos.emplace_back(h);
            RHI_PSO_UpdateDescriptorSet_Buffers(pipelineState, 0, 0, &ubos);
            
            RHI_DescriptorPool_Reset(context.descriptorPool, context.descriptorPoolIds[currentIndex]);
            RHI_DescriptorPool_AllocDescriptorSet(context.descriptorPool, context.descriptorPoolIds[currentIndex], 0, pipelineState);
            RHI_DescriptorPool_UpdateDescriptorSets(context.descriptorPool, context.descriptorPoolIds[currentIndex], pipelineState);

            RHI_Cmd_Begin(commandBuffer, m_FrameIndex, 0);
            {
                auto renderPass = reinterpret_cast<RHI::GPURenderPass*>(context.renderPass);
                auto frameBuffer = reinterpret_cast<RHI::FrameBuffer*>(context.frameBuffer);
                auto surface = RHI_Instance_GetSurface(this->m_Instance, context.windowId);
                auto swapchain = RHI_Surface_GetSwapChain(surface);
                RHI_ImageHandle backBuffer = RHI_SwapChain_AquireCurrentImage(swapchain, m_FrameIndex);
                if (backBuffer == 0)
                {
                   NextFrame(); // Avoid hanging on acquire failure
                   return;
                }
                auto backBufferView = RHI_SwapChain_GetImageView(swapchain, m_FrameIndex);
                
                // Allocate the render pass for the current frame
                RHI_RenderPass_Alloc(context.device, context.renderPass, m_FrameIndex);

                RHI_FrameBuffer_SetAttachment(context.device, context.frameBuffer, m_FrameIndex, backBufferView, context.renderPass);

                RHI::RenderPassBeginDesc desc
                {
                    *reinterpret_cast<RHI::RHIRenderPassHandle*>(&context.renderPass),
                    *reinterpret_cast<RHI::RHIFrameBufferHandle*>(&context.frameBuffer),
                    RHI::SUBPASS_CONTENTS_INLINE
                };

                RHI_Cmd_BeginRenderPass(commandBuffer, m_FrameIndex, &desc);

                {
                    // Use pre-allocated pipeline
                    auto pipeline = context.pipeline;

                    // RHI_Pipeline_AllocGraphics is now called in InitPipelineStates for all frames
                    RHI_Cmd_BindPipeline(commandBuffer, m_FrameIndex, pipeline);

                    RHI_Cmd_SetViewport(commandBuffer, 0.0f, 0.0f, static_cast<Float32>(RHI_ImageView_GetWidth(m_Context.device, backBufferView)), static_cast<Float32>(RHI_ImageView_GetHeight(m_Context.device, backBufferView)), 0.0f, 1.0f);
                    RHI_Cmd_SetScissor(commandBuffer, 0, 0, RHI_ImageView_GetWidth(m_Context.device, backBufferView), RHI_ImageView_GetHeight(m_Context.device, backBufferView));

                    RHI_Cmd_BindDescriptorSets_FromPool(commandBuffer, m_FrameIndex, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, context.descriptorPool, context.descriptorPoolIds[currentIndex]);

                    RHI_Cmd_BindVertexBuffers(commandBuffer, context.vertexBufferHandle, 0);
                    RHI_Cmd_BindIndexBuffer(commandBuffer, context.indicesBufferHandle, 0, RHI::INDEX_TYPE_UINT16);
                   
                    RHI_Cmd_DrawIndexed(commandBuffer, static_cast<UInt32>(indices.size()), 1, 0, 0, 0, 0);
                }

                RHI_Cmd_EndRenderPass(commandBuffer);
            }

            // Explicitly wait for the image to be available and signal when rendering is finished
            {
                auto surface = RHI_Instance_GetSurface(this->m_Instance, context.windowId);
                auto swapchain = RHI_Surface_GetSwapChain(surface);
                auto imageAvailableSem = RHI_SwapChain_GetImageAvailableSemaphore(swapchain, m_FrameIndex);
                auto renderFinishedSem = RHI_SwapChain_GetRenderFinishSemaphore(swapchain, m_FrameIndex);
                
                if (imageAvailableSem && renderFinishedSem)
                {
                    RHI_Cmd_WaitSemaphore(commandBuffer, imageAvailableSem, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT);
                    RHI_Cmd_SignalSemaphore(commandBuffer, renderFinishedSem);
                }
            }

            RHI_Cmd_End(commandBuffer);
            
            auto surface = RHI_Instance_GetSurface(m_Instance, context.windowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            RHIGpuTicket ticket = RHI_Device_Submit(context.device, commandBuffer, m_FrameIndex);
            
            // Store ticket for next time we encounter this frame index
            if (context.frameTickets.size() > m_FrameIndex)
            {
                context.frameTickets[m_FrameIndex] = ticket;
            }
            
            RHI_SwapChain_Present(swapchain, m_FrameIndex);

            RHI_Device_ReleaseCommandBuffer(context.device, context.commandPool, m_FrameIndex, commandBuffer);
        }
    };
}

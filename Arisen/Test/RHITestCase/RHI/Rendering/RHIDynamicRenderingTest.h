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
#include "RHI/Surfaces/Surface.h"
#include "RHI/Surfaces/FrameBuffer.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Memory/ImageView.h"
#include "RHI/Synchronization/RHIImageMemoryBarrier.h"
#include "RHI/CommandBuffer/RHICommandBuffer.h"
#include "RHI/CommandBuffer/RHICommandBufferPool.h"
#include "RHI/Program/GPUPipelineManager.h"
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
    class RHIDynamicRenderingTest : public RHITestBase
    {
    private:
        struct RenderContext
        {
            UInt32 windowId;
            UInt32 newWidth;
            UInt32 newHeight;
            RHI_DeviceHandle device;
            // No RenderPass or FrameBuffer needed for dynamic rendering logic
            // But SwapChain images still need views.
            RHI_BufferHandle vertexBufferHandle;
            RHI_BufferHandle indicesBufferHandle;
            Containers::Vector<RHI_BufferHandle> uniformBuffers;
            RHI_ImageHandle textureHandle;
            RHI_CommandBufferPoolHandle commandPool;
            RHI_DescriptorPoolHandle descriptorPool;
            RHI_PSOHandle pipelineState;
            RHI_PipelineHandle pipeline;
            Containers::Vector<RHI_GPUProgramHandle> gpuPrograms;
            Containers::Vector<UInt32> descriptorPoolIds;
            bool bShouldResize;

            // Cached vectors and structures to avoid per-frame heap allocations
            Containers::Vector<RHI::RHIImageMemoryBarrier> cachedBarriers;
            RHI::RHIRenderingInfo cachedRenderingInfo;
            RHI::RHIRenderingAttachmentInfo cachedColorAtt;
            Containers::Vector<RHI::RHIBufferHandle> cachedUbos;
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
        const char* GetName() const override { return "DynamicRenderingTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            m_Context.windowId = m_WindowId;
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
            RHI_Device_WaitIdle(this->m_Device);
            
            // Cleanup standard resources
            if (m_Context.vertexBufferHandle)
            {
                RHI_Device_ReleaseBufferHandle(m_Context.device, m_Context.vertexBufferHandle);
                m_Context.vertexBufferHandle = nullptr;
            }
            if (m_Context.indicesBufferHandle)
            {
                RHI_Device_ReleaseBufferHandle(m_Context.device, m_Context.indicesBufferHandle);
                m_Context.indicesBufferHandle = nullptr;
            }
            for (auto& ub : m_Context.uniformBuffers)
            {
                if (ub) RHI_Device_ReleaseBufferHandle(m_Context.device, ub);
            }
            m_Context.uniformBuffers.clear();

            if (m_Context.textureHandle)
            {
                RHI_Device_ReleaseImageHandle(m_Context.device, m_Context.textureHandle);
                m_Context.textureHandle = nullptr;
            }

            if (m_Context.pipelineState)
            {
                RHI_PSO_Destroy(m_Context.pipelineState);
                m_Context.pipelineState = nullptr;
            }
            
            for (auto& program : m_Context.gpuPrograms)
            {
                if (program)
                    RHI_Device_ReleaseGPUProgram(m_Context.device, program);
            }
            m_Context.gpuPrograms.clear();

            if (m_Context.commandPool)
            {
                RHI_Device_ReleaseCommandBufferPool(m_Context.device, m_Context.commandPool);
                m_Context.commandPool = nullptr;
            }
        }

    private:
        void RenderFrame()
        {
            RHI_Device_WaitFrameFence(m_Context.device, m_FrameIndex);
            UploadUniformBuffer(m_Context);
            RecordSubmitPresent(m_Context);
        
            if (m_Context.bShouldResize)
            {
                RHI_Device_SetResolution(m_Context.device, m_Context.newWidth, m_Context.newHeight);
                m_Context.bShouldResize = false;
            }

            NextFrame();
            
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
            // No RenderPass/FrameBuffer creation here!
            
            m_Context.vertexBufferHandle = RHI_Device_GetBufferHandle(m_Context.device, "Vertex Buffer");
            m_Context.indicesBufferHandle = RHI_Device_GetBufferHandle(m_Context.device, "Indices Buffer");
            m_Context.textureHandle = RHI_Device_GetImageHandle(m_Context.device, "Texture Image");
            m_Context.descriptorPool = RHI_Device_GetDescriptorPool(m_Context.device);
            m_Context.pipelineState = nullptr;
            
            for(int i = 0; i < (int)m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types { RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER };
                Containers::Vector<unsigned int> counts { 1 };
                unsigned int poolId = RHI_DescriptorPool_AddPool(m_Context.descriptorPool, &types, &counts, 1);
                m_Context.descriptorPoolIds.emplace_back(poolId);
                auto name = std::string("Uniform Buffer ") + std::to_string(i);
                m_Context.uniformBuffers.emplace_back(RHI_Device_GetBufferHandle(m_Context.device, name.c_str()));
            }
        }

        void InitPipelineStates()
        {
            auto pipelineManager = RHI_Device_GetPipelineManager(m_Context.device);
            m_Context.pipelineState = RHI_PipelineManager_CreatePSO(pipelineManager);
            auto pipelineState = m_Context.pipelineState;

            // Set Rendering Formats for Dynamic Rendering
            Containers::Vector<RHI::EFormat> colorFormats = { RHI::EFormat::FORMAT_B8G8R8A8_SRGB }; // SwapChain format
            RHI_PSO_SetRenderingFormats(pipelineState, &colorFormats, RHI::EFormat::FORMAT_UNDEFINED, RHI::EFormat::FORMAT_UNDEFINED);

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
            RHI_PSO_SetSampleCount(pipelineState, RHI::SAMPLE_COUNT_1_BIT); 
            RHI_PSO_AddBlendAttachmentState_Simple(pipelineState, false,
                                                   RHI::EColorComponentFlagBits::COLOR_COMPONENT_R_BIT |
                                                   RHI::EColorComponentFlagBits::COLOR_COMPONENT_G_BIT |
                                                   RHI::EColorComponentFlagBits::COLOR_COMPONENT_B_BIT |
                                                   RHI::EColorComponentFlagBits::COLOR_COMPONENT_A_BIT);
            RHI_PSO_SetLogicOp(pipelineState, false, RHI::LOGIC_OP_COPY);
            RHI_PSO_SetBlendConstants(pipelineState, 0.0f, 0.0f, 0.0f, 0.0f);

            m_Context.pipeline = RHI_PipelineManager_GetGraphicsPipeline(pipelineManager, pipelineState);
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                // nullptr subpass triggers dynamic rendering logic path in AllocGraphicPipeline
                RHI_Pipeline_AllocGraphics(m_Context.pipeline, i, nullptr);
            }
        }
        
        // ... (InitShaderProgram, InitBuffer, CreateImage, UploadVertex, UploadImage, UploadUniformBuffer same as BasicTest)
        // Copying these helper methods since they are identical and independent of render pass
        void InitShaderProgram()
        {
             std::wstring envStr;
            {
                unsigned int len = RHI_Instance_GetEnvStringW(this->m_Instance, nullptr, 0);
                if (len > 0) {
                    std::wstring tmp; tmp.resize(len ? (len - 1) : 0);
                    if (len > 1) RHI_Instance_GetEnvStringW(this->m_Instance, tmp.data(), len);
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

            Platforms::ShaderCompileParams vertexParams{ path, L"Vert", L"6_0", L"-spirv", envStr, L"0", RHI::ProgramStage::Vertex, {}, {}, currentPath + L"\\"+ shaderFileName + L".vert.spirv", true };
            Platforms::ShaderCompilerOutput outputVertex;
            if (!Platforms::CompileShaderFromFile(std::move(vertexParams), outputVertex) || outputVertex.codePointer == nullptr || outputVertex.codeSize == 0) throw std::exception("Vertex shader compilation failed.");

            {
                auto program = RHI_Device_CreateGPUProgram(m_Context.device);
                std::string nameStr = String::WStringToString(path);
                auto desc = RHI::GPUProgramDesc{ outputVertex.codeSize, outputVertex.codePointer, "Vert", nameStr.c_str(), RHI::SHADER_STAGE_VERTEX_BIT };
                RHI_Device_AttachProgramByteCode(m_Context.device, program, &desc);
                m_Context.gpuPrograms.emplace_back(program);
            }
            if (outputVertex.codePointer) std::free(outputVertex.codePointer);

            Platforms::ShaderCompileParams fragmentParams{ path, L"Frag", L"6_0", L"-spirv", envStr, L"0", RHI::ProgramStage::Fragment, {}, {}, currentPath + L"\\" + shaderFileName + L".frag.spirv", true };
            Platforms::ShaderCompilerOutput outputfragment;
            if (!Platforms::CompileShaderFromFile(std::move(fragmentParams), outputfragment) || outputfragment.codePointer == nullptr || outputfragment.codeSize == 0) throw std::exception("Fragment shader compilation failed.");

             {
                auto program = RHI_Device_CreateGPUProgram(m_Context.device);
                std::string nameStr = String::WStringToString(path);
                auto desc = RHI::GPUProgramDesc{ outputfragment.codeSize, outputfragment.codePointer, "Frag", nameStr.c_str(), RHI::SHADER_STAGE_FRAGMENT_BIT };
                RHI_Device_AttachProgramByteCode(m_Context.device, program, &desc);
                m_Context.gpuPrograms.emplace_back(program);
            }
            if (outputfragment.codePointer) std::free(outputfragment.codePointer);
        }

        void InitBuffer()
        {
            RHI::BufferDescriptor vbDesc{ 0, sizeof(vertices[0]) * vertices.size(), RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE };
            RHI_Buffer_Alloc(m_Context.vertexBufferHandle, &vbDesc);
            RHI_Buffer_AllocDeviceMemory(m_Context.vertexBufferHandle, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            RHI::BufferDescriptor ibDesc{ 0, sizeof(indices[0]) * indices.size(), RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_INDEX_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE };
            RHI_Buffer_Alloc(m_Context.indicesBufferHandle, &ibDesc);
            RHI_Buffer_AllocDeviceMemory(m_Context.indicesBufferHandle, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            for (const auto& uniformBuffer : m_Context.uniformBuffers) {
                RHI::BufferDescriptor ubDesc{ 0, sizeof(UniformBufferObject), RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE };
                RHI_Buffer_Alloc(uniformBuffer, &ubDesc);
                RHI_Buffer_AllocDeviceMemory(uniformBuffer, RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
            }
            UploadVertex();
        }

        void CreateImage() {
            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            const fs::path exeDir = fs::path(exePathW).parent_path();
            const fs::path assetPath = exeDir / "Assets" / "Arisen.png";
            int texWidth = 0, texHeight = 0, texChannels = 0;
            stbi_uc* pixels = stbi_load(assetPath.string().c_str(), &texWidth, &texHeight, &texChannels, STBI_rgb_alpha);
            if (!pixels || texWidth <= 0 || texHeight <= 0) throw std::exception("Failed to load texture image");
            const UInt64 imageSize = static_cast<UInt64>(texWidth) * static_cast<UInt64>(texHeight) * 4ull;
            RHI::ImageDescriptor imgDesc{ RHI::IMAGE_TYPE_2D, static_cast<UInt32>(texWidth), static_cast<UInt32>(texHeight), 1, 1, 1, RHI::FORMAT_R8G8B8A8_SRGB, RHI::IMAGE_TILING_OPTIMAL, RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_USAGE_SAMPLED_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT, RHI::SAMPLE_COUNT_1_BIT, RHI::SHARING_MODE_EXCLUSIVE };
            RHI_Image_Alloc(m_Context.textureHandle, &imgDesc);
            RHI_Image_AllocDeviceMemory(m_Context.textureHandle, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
            RHI::ImageViewDesc imageViewDesc{ RHI::IMAGE_VIEW_TYPE_2D, RHI::FORMAT_R8G8B8A8_SRGB, RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
            imageViewDesc.width = static_cast<UInt32>(texWidth); imageViewDesc.height = static_cast<UInt32>(texHeight);
            RHI_Image_AddImageView(m_Context.textureHandle, &imageViewDesc);
            UploadImage(imageSize, pixels, texWidth, texHeight);
            stbi_image_free(pixels);
        }

        void UploadVertex() {
            auto device = m_Context.device;
            auto vertexBufferHandle = m_Context.vertexBufferHandle;
            auto indicesBufferHandle = m_Context.indicesBufferHandle;
            auto vertexStagingBufferHandle = RHI_Device_GetBufferHandle(device, "Vertex Staging Buffer");
            RHI::BufferDescriptor vsb{ 0, sizeof(vertices[0]) * vertices.size(), RHI::BUFFER_USAGE_TRANSFER_SRC_BIT, RHI::SHARING_MODE_EXCLUSIVE };
            RHI_Buffer_Alloc(vertexStagingBufferHandle, &vsb);
            RHI_Buffer_AllocDeviceMemory(vertexStagingBufferHandle, RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
            RHI_Buffer_MemoryCopy(vertexStagingBufferHandle, vertices.data(), 0);
            auto indicesStagingBufferHandle = RHI_Device_GetBufferHandle(device, "Indices Staging Buffer");
            RHI::BufferDescriptor isb{ 0, sizeof(indices[0]) * indices.size(), RHI::BUFFER_USAGE_TRANSFER_SRC_BIT, RHI::SHARING_MODE_EXCLUSIVE };
            RHI_Buffer_Alloc(indicesStagingBufferHandle, &isb);
            RHI_Buffer_AllocDeviceMemory(indicesStagingBufferHandle, RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
            RHI_Buffer_MemoryCopy(indicesStagingBufferHandle, indices.data(), 0);
            auto commandBuffer = RHI_Device_GetCommandBuffer(device, m_Context.commandPool, m_FrameIndex);
            RHI_Cmd_Begin(commandBuffer, m_FrameIndex, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            RHI_Cmd_CopyBuffer(commandBuffer, vertexStagingBufferHandle, 0, vertexBufferHandle, 0, RHI_Buffer_Size(vertexBufferHandle));
            RHI_Cmd_CopyBuffer(commandBuffer, indicesStagingBufferHandle, 0, indicesBufferHandle, 0, RHI_Buffer_Size(indicesBufferHandle));
            RHI_Cmd_End(commandBuffer);
            RHI_Device_Submit(device, commandBuffer, m_FrameIndex);
            RHI_Device_ReleaseBufferHandle(device, vertexStagingBufferHandle);
            RHI_Device_ReleaseBufferHandle(device, indicesStagingBufferHandle);
            RHI_Device_ReleaseCommandBuffer(device, m_Context.commandPool, m_FrameIndex, commandBuffer);
        }

        void UploadImage(UInt64 textureSize, void* data, UInt32 texWidth, UInt32 texHeight) {
            auto device = m_Context.device;
            auto textureStagingBufferHandle = RHI_Device_GetBufferHandle(device, "Texture Staging Buffer");
            RHI::BufferDescriptor tsb{ 0, textureSize, RHI::BUFFER_USAGE_TRANSFER_SRC_BIT, RHI::SHARING_MODE_EXCLUSIVE };
            RHI_Buffer_Alloc(textureStagingBufferHandle, &tsb);
            RHI_Buffer_AllocDeviceMemory(textureStagingBufferHandle, RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
            RHI_Buffer_MemoryCopy(textureStagingBufferHandle, data, 0);
            auto commandBuffer = RHI_Device_GetCommandBuffer(device, m_Context.commandPool, m_FrameIndex);
            RHI_Cmd_Begin(commandBuffer, m_FrameIndex, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            {
                Containers::Vector<RHI::RHIImageMemoryBarrier> barriers { { RHI::ACCESS_NONE, RHI::ACCESS_TRANSFER_WRITE_BIT, RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VK_QUEUE_FAMILY_IGNORED, VK_QUEUE_FAMILY_IGNORED, reinterpret_cast<RHI::ImageHandle*>(m_Context.textureHandle), { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 } } };
                RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_TRANSFER_BIT, 0, &barriers);
            }
            {
                ArisenEngine::Containers::Vector<RHI::BufferImageCopy> regions{ { 0, 0, 0, { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 }, 0, 0, 0, texWidth, texHeight, 1 } };
                RHI_Cmd_CopyBufferToImage(commandBuffer, textureStagingBufferHandle, m_Context.textureHandle, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, &regions);
            }
            {
                 Containers::Vector<RHI::RHIImageMemoryBarrier> barriers{ { RHI::ACCESS_TRANSFER_WRITE_BIT, RHI::ACCESS_SHADER_READ_BIT, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL, ~0U, ~0U, reinterpret_cast<RHI::ImageHandle*>(m_Context.textureHandle), { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 } } };
                RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_TRANSFER_BIT, RHI::PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, &barriers);
            }
            RHI_Cmd_End(commandBuffer);
            RHI_Device_Submit(device, commandBuffer, m_FrameIndex);
            RHI_Device_ReleaseBufferHandle(device, textureStagingBufferHandle);
            RHI_Device_ReleaseCommandBuffer(device, m_Context.commandPool, m_FrameIndex, commandBuffer);
        }

        void UploadUniformBuffer(RenderContext const& context) {
            static auto startTime = std::chrono::high_resolution_clock::now();
            auto currentTime = std::chrono::high_resolution_clock::now();
            float time = std::chrono::duration<float, std::chrono::seconds::period>(currentTime - startTime).count();
            UniformBufferObject ubo{};
            ubo.model = glm::rotate(glm::mat4(1.0f), time * glm::radians(90.0f), glm::vec3(0.0f, 0.0f, 1.0f));
            ubo.view = glm::lookAt(glm::vec3(2.0f, 2.0f, 2.0f), glm::vec3(0.0f, 0.0f, 0.0f), glm::vec3(0.0f, 0.0f, 1.0f));
            ubo.proj = glm::perspective(glm::radians(45.0f), context.newWidth / (float) context.newHeight, 0.1f, 10.0f);
            ubo.proj[1][1] *= -1;
            auto currentIndex = GetCurrentFrameIndex();
            RHI_Buffer_MemoryCopy(context.uniformBuffers[currentIndex], &ubo, 0);
        }

        void RecordSubmitPresent(RenderContext& context)
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto commandBuffer = RHI_Device_GetCommandBuffer(context.device, context.commandPool, m_FrameIndex);
            auto pipelineState = context.pipelineState;
            context.cachedUbos.clear();
            auto rawHandle = context.uniformBuffers[currentIndex];
            auto h = *reinterpret_cast<RHI::RHIBufferHandle*>(&rawHandle);
            context.cachedUbos.emplace_back(h);
            RHI_PSO_UpdateDescriptorSet_Buffers(pipelineState, 0, 0, &context.cachedUbos);
            RHI_DescriptorPool_Reset(context.descriptorPool, context.descriptorPoolIds[currentIndex]);
            RHI_DescriptorPool_AllocDescriptorSet(context.descriptorPool, context.descriptorPoolIds[currentIndex], 0, pipelineState);
            RHI_DescriptorPool_UpdateDescriptorSets(context.descriptorPool, context.descriptorPoolIds[currentIndex], pipelineState);

            RHI_Cmd_Begin(commandBuffer, m_FrameIndex, 0);
            {
                auto surface = RHI_Instance_GetSurface(this->m_Instance, context.windowId);
                auto swapchain = RHI_Surface_GetSwapChain(surface);
                ArisenEngine::RHI::ImageHandle* backBuffer = RHI_SwapChain_AquireCurrentImage(swapchain, m_FrameIndex);
                if (backBuffer == nullptr) return;
                auto backBufferView = RHI_Image_GetView(backBuffer); // Returns ImageView*
                
            // --- Dynamic Rendering Begin ---
            
            // 1. Transition Image to Color Attachment Optimal
            {
                context.cachedBarriers.assign({
                    {
                        RHI::ACCESS_NONE,
                        RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
                        RHI::IMAGE_LAYOUT_UNDEFINED,
                        RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        VK_QUEUE_FAMILY_IGNORED,
                        VK_QUEUE_FAMILY_IGNORED,
                        backBuffer,
                        { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 }
                    }
                });
                RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, 0, &context.cachedBarriers);
            }

            // 2. Begin Rendering
            {
                context.cachedColorAtt.imageView = backBufferView;
                context.cachedColorAtt.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                context.cachedColorAtt.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                context.cachedColorAtt.storeOp = RHI::ATTACHMENT_STORE_OP_STORE;
                context.cachedColorAtt.clearValue.float32[0] = 0.0f;
                context.cachedColorAtt.clearValue.float32[1] = 0.0f;
                context.cachedColorAtt.clearValue.float32[2] = 0.0f;
                context.cachedColorAtt.clearValue.float32[3] = 1.0f;

                context.cachedRenderingInfo.renderArea = { 0, 0, RHI_ImageView_GetWidth(backBufferView), RHI_ImageView_GetHeight(backBufferView) };
                context.cachedRenderingInfo.layerCount = 1;
                context.cachedRenderingInfo.pColorAttachments = &context.cachedColorAtt;
                context.cachedRenderingInfo.colorAttachmentCount = 1;
                context.cachedRenderingInfo.pDepthAttachment = nullptr;
                context.cachedRenderingInfo.pStencilAttachment = nullptr;
                
                RHI_Cmd_BeginRendering(commandBuffer, &context.cachedRenderingInfo);
            }

            // 3. Draw
            {
                auto pipeline = context.pipeline;
                RHI_Cmd_BindPipeline(commandBuffer, m_FrameIndex, pipeline);
                RHI_Cmd_SetViewport(commandBuffer, 0.0f, 0.0f, static_cast<Float32>(RHI_ImageView_GetWidth(backBufferView)), static_cast<Float32>(RHI_ImageView_GetHeight(backBufferView)), 0.0f, 1.0f);
                RHI_Cmd_SetScissor(commandBuffer, 0, 0, RHI_ImageView_GetWidth(backBufferView), RHI_ImageView_GetHeight(backBufferView));
                RHI_Cmd_BindDescriptorSets_FromPool(commandBuffer, m_FrameIndex, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, context.descriptorPool, context.descriptorPoolIds[currentIndex]);
                RHI_Cmd_BindVertexBuffers(commandBuffer, context.vertexBufferHandle, 0);
                RHI_Cmd_BindIndexBuffer(commandBuffer, context.indicesBufferHandle, 0, RHI::INDEX_TYPE_UINT16);
                RHI_Cmd_DrawIndexed(commandBuffer, static_cast<UInt32>(indices.size()), 1, 0, 0, 0, 0);
            }

            // 4. End Rendering
            RHI_Cmd_EndRendering(commandBuffer);

            // 5. Transition to Present
            {
                context.cachedBarriers.assign({
                    {
                        RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
                        RHI::ACCESS_NONE,
                        RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR,
                        VK_QUEUE_FAMILY_IGNORED,
                        VK_QUEUE_FAMILY_IGNORED,
                        backBuffer,
                        { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 }
                    }
                });
                RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, RHI::PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, 0, &context.cachedBarriers);
            }
            }

            {
                auto surface = RHI_Instance_GetSurface(this->m_Instance, context.windowId);
                auto swapchain = RHI_Surface_GetSwapChain(surface);
                auto imageAvailableSem = RHI_SwapChain_GetImageAvailableSemaphore(swapchain, m_FrameIndex);
                auto renderFinishedSem = RHI_SwapChain_GetRenderFinishSemaphore(swapchain, m_FrameIndex);
                
                if (imageAvailableSem && renderFinishedSem)
                {
                    RHI_Cmd_WaitSemaphore(commandBuffer, reinterpret_cast<RHI_SemaphoreHandle>(imageAvailableSem), RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT);
                    RHI_Cmd_SignalSemaphore(commandBuffer, reinterpret_cast<RHI_SemaphoreHandle>(renderFinishedSem));
                }
            }

            RHI_Cmd_End(commandBuffer);
            RHI_Device_Submit(context.device, commandBuffer, m_FrameIndex);
            
            auto surface = RHI_Instance_GetSurface(m_Instance, context.windowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            RHI_SwapChain_Present(swapchain, m_FrameIndex);
            RHI_Device_ReleaseCommandBuffer(context.device, context.commandPool, m_FrameIndex, commandBuffer);
        }
    };
}

#pragma once

#include "../RHITestBase.h"
#include <chrono>
#include <iostream>

// RHI Includes
#include "RHI/Enums/Pipeline/EAccessFlag.h"
#include "RHI/Enums/Buffer/EBufferUsage.h"
#include "RHI/Enums/Pipeline/EColorComponentFlag.h"
#include "RHI/Enums/Pipeline/ECommandBufferUsageFlagBits.h"
#include "RHI/Enums/Pipeline/EIndexType.h"
#include "RHI/Enums/Attachment/EAttachmentLoadOp.h"
#include "RHI/Enums/Attachment/EAttachmentStoreOp.h"
#include "RHI/Enums/Image/EImageAspectFlagBits.h"
#include "RHI/Presentation/RHISurface.h"
#include "RHI/RenderPass/RHIFrameBuffer.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Core/RHICommon.h"
#include "RHI/Sync/RHIImageMemoryBarrier.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Pipeline/RHIPipeline.h"
#include "RHI/Pipeline/RHIPipelineState.h"

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
    public:
        using RHIGpuTicket = ArisenEngine::UInt64;
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
            Containers::Vector<RHIGpuTicket> frameTickets;
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

    public:
        const char* GetName() const override { return "DynamicRenderingTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            m_Context.windowId = m_WindowId;
            m_Context.newWidth = 640; 
            m_Context.newHeight = 480;
            m_Context.device = this->m_Device;
            m_Context.vertexBufferHandle = 0ULL;
            m_Context.indicesBufferHandle = 0ULL;
            m_Context.textureHandle = 0ULL;
            m_Context.pipelineState = nullptr;
            m_Context.pipeline = 0ULL;
            m_Context.bShouldResize = false;

            InitRenderContext();
            HAL::InitDXC();
            InitShaderProgram();
            InitPipelineStates();
            InitBuffer();
            CreateImage();
            
            return true;
        }

        void TeardownTest() override
        {
            RHI_Device_WaitIdle(this->m_Device);
            
            // Cleanup standard resources
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

            if (m_Context.textureHandle)
            {
                RHI_Device_ReleaseImage(m_Context.device, m_Context.textureHandle);
                m_Context.textureHandle = 0ULL;
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
                m_Context.commandPool = 0;
            }
        }

    protected:
        void RenderFrame() override
        {
            // Wait for the previous submission of this frame index to complete
            auto currentIndex = GetCurrentFrameIndex();
            if (m_Context.frameTickets.size() > currentIndex)
            {
                RHI_Device_WaitQueueTicket(m_Context.device, m_Context.frameTickets[currentIndex]);
            }
            UploadUniformBuffer(m_Context);
            RecordSubmitPresent(m_Context);
        
            if (m_Context.bShouldResize)
            {
                RHI_Device_SetResolution(m_Context.device, m_Context.newWidth, m_Context.newHeight);
                m_Context.bShouldResize = false;
            }

            NextFrame();
        }
    private:
        void InitRenderContext()
        {
            m_Context.commandPool = RHI_Device_CreateCommandBufferPool(m_Context.device);
            // No RenderPass/FrameBuffer creation here!
            
            m_Context.descriptorPool = RHI_Device_GetDescriptorPool(m_Context.device);
            m_Context.pipelineState = nullptr;
            
            for(int i = 0; i < (int)m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types { RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER };
                Containers::Vector<unsigned int> counts { 1 };
                unsigned int poolId = RHI_DescriptorPool_AddPool(m_Context.descriptorPool, &types, &counts, 1);
                m_Context.descriptorPoolIds.emplace_back(poolId);
                m_Context.frameTickets.emplace_back(0);
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
                RHI_Pipeline_AllocGraphics(m_Context.device, m_Context.pipeline, i, nullptr);
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

            HAL::ShaderCompileParams vertexParams{ path, L"Vert", L"6_0", L"-spirv", envStr, L"0", RHI::EProgramStage::Vertex, {}, {}, currentPath + L"\\"+ shaderFileName + L".vert.spirv", true };
            HAL::ShaderCompilerOutput outputVertex;
            if (!HAL::CompileShaderFromFile(std::move(vertexParams), outputVertex) || outputVertex.codePointer == nullptr || outputVertex.codeSize == 0) throw std::exception("Vertex shader compilation failed.");

            {
                auto program = RHI_Device_CreateGPUProgram(m_Context.device);
                std::string nameStr = String::WStringToString(path);
                auto desc = RHI::RHIShaderProgramDesc{ outputVertex.codeSize, outputVertex.codePointer, "Vert", nameStr.c_str(), RHI::SHADER_STAGE_VERTEX_BIT };
                RHI_Device_AttachProgramByteCode(m_Context.device, program, &desc);
                m_Context.gpuPrograms.emplace_back(program);
            }
            if (outputVertex.codePointer) std::free(outputVertex.codePointer);

            HAL::ShaderCompileParams fragmentParams{ path, L"Frag", L"6_0", L"-spirv", envStr, L"0", RHI::EProgramStage::Fragment, {}, {}, currentPath + L"\\" + shaderFileName + L".frag.spirv", true };
            HAL::ShaderCompilerOutput outputfragment;
            if (!HAL::CompileShaderFromFile(std::move(fragmentParams), outputfragment) || outputfragment.codePointer == nullptr || outputfragment.codeSize == 0) throw std::exception("Fragment shader compilation failed.");

             {
                auto program = RHI_Device_CreateGPUProgram(m_Context.device);
                std::string nameStr = String::WStringToString(path);
                auto desc = RHI::RHIShaderProgramDesc{ outputfragment.codeSize, outputfragment.codePointer, "Frag", nameStr.c_str(), RHI::SHADER_STAGE_FRAGMENT_BIT };
                RHI_Device_AttachProgramByteCode(m_Context.device, program, &desc);
                m_Context.gpuPrograms.emplace_back(program);
            }
            if (outputfragment.codePointer) std::free(outputfragment.codePointer);
        }

        void InitBuffer()
        {
            RHI::RHIBufferDescriptor vbDesc{
                0,
                sizeof(vertices[0]) * (UInt64)vertices.size(),
                RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT,
                RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT
            };
            m_Context.vertexBufferHandle = RHI_Device_CreateBuffer(m_Context.device, &vbDesc, "Vertex Buffer");

            RHI::RHIBufferDescriptor ibDesc{
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
                RHI::RHIBufferDescriptor ubDesc{
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
            RHI::RHIImageDescriptor imgDesc{
                RHI::IMAGE_TYPE_2D, static_cast<UInt32>(texWidth), static_cast<UInt32>(texHeight), 1,
                1, 1, RHI::FORMAT_R8G8B8A8_SRGB, RHI::IMAGE_TILING_OPTIMAL,
                RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_USAGE_SAMPLED_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT,
                RHI::SAMPLE_COUNT_1_BIT, RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT
            };
            m_Context.textureHandle = RHI_Device_CreateImage(m_Context.device, &imgDesc, "Texture Image");
            RHI::RHIImageViewDesc RHIImageViewDesc{ RHI::IMAGE_VIEW_TYPE_2D, RHI::FORMAT_R8G8B8A8_SRGB, RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
            RHIImageViewDesc.width = static_cast<UInt32>(texWidth); RHIImageViewDesc.height = static_cast<UInt32>(texHeight);
            RHI_Image_AddImageView(m_Context.device, m_Context.textureHandle, &RHIImageViewDesc);
            UploadImage(imageSize, pixels, texWidth, texHeight);
            stbi_image_free(pixels);
        }

        void UploadVertex() {
            auto device = m_Context.device;
            auto vertexBufferHandle = m_Context.vertexBufferHandle;
            auto indicesBufferHandle = m_Context.indicesBufferHandle;
            RHI::RHIBufferDescriptor vsb{
                0,
                sizeof(vertices[0]) * vertices.size(),
                RHI::BUFFER_USAGE_TRANSFER_SRC_BIT,
                RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT
            };
            auto vertexStagingBufferHandle = RHI_Device_CreateBuffer(device, &vsb, "Vertex Staging Buffer");
            RHI_Buffer_MemoryCopy(device, vertexStagingBufferHandle, vertices.data(), 0);
            
            RHI::RHIBufferDescriptor isb{
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

        void UploadImage(UInt64 textureSize, void* data, UInt32 texWidth, UInt32 texHeight) {
            auto device = m_Context.device;
            RHI::RHIBufferDescriptor tsb{
                0,
                textureSize,
                RHI::BUFFER_USAGE_TRANSFER_SRC_BIT,
                RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT
            };
            auto textureStagingBufferHandle = RHI_Device_CreateBuffer(device, &tsb, "Texture Staging Buffer");
            RHI_Buffer_MemoryCopy(device, textureStagingBufferHandle, data, 0);
            auto commandBuffer = RHI_Device_GetCommandBuffer(device, m_Context.commandPool, m_FrameIndex);
            RHI_Cmd_Begin(commandBuffer, m_FrameIndex, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            {
                RHI::RHIImageMemoryBarrier barrier{};
                barrier.srcAccess = RHI::ACCESS_NONE;
                barrier.dstAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
                barrier.oldLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
                barrier.newLayout = RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
                barrier.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
                barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
                barrier.image = *reinterpret_cast<RHI::RHIImageHandle*>(&m_Context.textureHandle);
                barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
                
                Containers::Vector<RHI::RHIImageMemoryBarrier> barriers { barrier };
                RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_TRANSFER_BIT, 0, &barriers);
            }
            {
                ArisenEngine::Containers::Vector<RHI::RHIBufferImageCopy> regions{ { 0, 0, 0, { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 }, 0, 0, 0, texWidth, texHeight, 1 } };
                RHI_Cmd_CopyBufferToImage(commandBuffer, textureStagingBufferHandle, m_Context.textureHandle, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, &regions);
            }
            {
                RHI::RHIImageMemoryBarrier barrier{};
                barrier.srcAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
                barrier.dstAccess = RHI::ACCESS_SHADER_READ_BIT;
                barrier.oldLayout = RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
                barrier.newLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                barrier.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
                barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
                barrier.image = *reinterpret_cast<RHI::RHIImageHandle*>(&m_Context.textureHandle);
                barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };

                Containers::Vector<RHI::RHIImageMemoryBarrier> barriers{ barrier };
                RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_TRANSFER_BIT, RHI::PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, &barriers);
            }
            RHI_Cmd_End(commandBuffer);
            RHI_Device_Submit(device, commandBuffer, m_FrameIndex);

            // Sync one-time setup transfers immediately to avoid command buffer reuse conflicts with first frame
            RHI_Device_WaitIdle(device);

            RHI_Device_ReleaseBuffer(device, textureStagingBufferHandle);
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
            RHI_Buffer_MemoryCopy(context.device, context.uniformBuffers[currentIndex], &ubo, 0);
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
            
            auto surface = RHI_Instance_GetSurface(this->m_Instance, context.windowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            RHI_ImageHandle backBuffer = RHI_SwapChain_AquireCurrentImage(swapchain, m_FrameIndex);
            
            if (backBuffer == 0)
            {
                NextFrame();
                return;
            }

            if (backBuffer != 0)
            {
                auto backBufferView = RHI_SwapChain_GetImageView(swapchain, m_FrameIndex);
                
                // 1. Transition Image to Color Attachment Optimal
                {
                    RHI::RHIImageMemoryBarrier barrier{};
                    barrier.srcAccess = RHI::ACCESS_NONE;
                    barrier.dstAccess = RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                    barrier.oldLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
                    barrier.newLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                    barrier.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
                    barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
                    barrier.image = *reinterpret_cast<RHI::RHIImageHandle*>(&backBuffer);
                    barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
                    barrier.srcStageMask = RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT;
                    barrier.dstStageMask = RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
                    
                    context.cachedBarriers.clear();
                    context.cachedBarriers.push_back(barrier);
                    RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, 0, &context.cachedBarriers);
                }

                // 2. Begin Rendering
                {
                    context.cachedColorAtt.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&backBufferView);
                    context.cachedColorAtt.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                    context.cachedColorAtt.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                    context.cachedColorAtt.storeOp = RHI::ATTACHMENT_STORE_OP_STORE;
                    context.cachedColorAtt.clearValue.float32[0] = 0.0f;
                    context.cachedColorAtt.clearValue.float32[1] = 0.0f;
                    context.cachedColorAtt.clearValue.float32[2] = 0.0f;
                    context.cachedColorAtt.clearValue.float32[3] = 1.0f;

                    context.cachedRenderingInfo.RHIRenderArea = { 0, 0, RHI_ImageView_GetWidth(context.device, backBufferView), RHI_ImageView_GetHeight(context.device, backBufferView) };
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
                    RHI_Cmd_SetViewport(commandBuffer, 0.0f, 0.0f, static_cast<Float32>(RHI_ImageView_GetWidth(context.device, backBufferView)), static_cast<Float32>(RHI_ImageView_GetHeight(context.device, backBufferView)), 0.0f, 1.0f);
                    RHI_Cmd_SetScissor(commandBuffer, 0, 0, RHI_ImageView_GetWidth(context.device, backBufferView), RHI_ImageView_GetHeight(context.device, backBufferView));
                    RHI_Cmd_BindDescriptorSets_FromPool(commandBuffer, m_FrameIndex, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, context.descriptorPool, context.descriptorPoolIds[currentIndex]);
                    RHI_Cmd_BindVertexBuffers(commandBuffer, context.vertexBufferHandle, 0);
                    RHI_Cmd_BindIndexBuffer(commandBuffer, context.indicesBufferHandle, 0, RHI::INDEX_TYPE_UINT16);
                    RHI_Cmd_DrawIndexed(commandBuffer, static_cast<UInt32>(indices.size()), 1, 0, 0, 0, 0);
                }

                // 4. End Rendering
                RHI_Cmd_EndRendering(commandBuffer);

                // 5. Transition to Present
                {
                    RHI::RHIImageMemoryBarrier barrier{};
                    barrier.srcAccess = RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                    barrier.dstAccess = RHI::ACCESS_NONE;
                    barrier.oldLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                    barrier.newLayout = RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR;
                    barrier.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
                    barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
                    barrier.image = *reinterpret_cast<RHI::RHIImageHandle*>(&backBuffer);
                    barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
                    barrier.srcStageMask = RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
                    barrier.dstStageMask = RHI::PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT;
                    
                    context.cachedBarriers.clear();
                    context.cachedBarriers.push_back(barrier);
                    RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, RHI::PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, 0, &context.cachedBarriers);
                }
            }

            {
                auto imageAvailableSem = RHI_SwapChain_GetImageAvailableSemaphore(swapchain, m_FrameIndex);
                auto renderFinishedSem = RHI_SwapChain_GetRenderFinishSemaphore(swapchain, m_FrameIndex);
                
                if (imageAvailableSem && renderFinishedSem)
                {
                    RHI_Cmd_WaitSemaphore(commandBuffer, imageAvailableSem, (unsigned int)RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT);
                    RHI_Cmd_SignalSemaphore(commandBuffer, renderFinishedSem);
                }
            }

            RHI_Cmd_End(commandBuffer);
            RHIGpuTicket ticket = RHI_Device_Submit(context.device, commandBuffer, m_FrameIndex);
            
             if (context.frameTickets.size() > currentIndex)
            {
                context.frameTickets[currentIndex] = ticket;
            }
            
            RHI_SwapChain_Present(swapchain, m_FrameIndex);
            RHI_Device_ReleaseCommandBuffer(context.device, context.commandPool, m_FrameIndex, commandBuffer);
        }
    };
}


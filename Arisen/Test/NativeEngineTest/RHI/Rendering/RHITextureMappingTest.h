#pragma once

#include "../RHITestBase.h"
#include "../../../Engine/NativeEngine/RHI/PipelineExports.h"
#include "../../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../../Engine/NativeEngine/RHI/SyncExports.h"
#include "../../../Engine/NativeEngine/RHI/SurfaceExports.h"
#include "../../../Engine/NativeEngine/RHI/HandlesExports.h"
#include "../../../Engine/NativeEngine/RHI/DescriptorExports.h"
#include "ShaderCompiler/ShaderCompilerAPI.h"

#include <stb_image.h>
#include <windows.h>
#include <filesystem>
#include <string>

namespace ArisenEngine::Testing
{
    class RHITextureMappingTest : public RHITestBase
    {
        struct Vertex {
            float pos[3];
            float uv[2];
        };

        struct alignas(16) UBO {
            float model[16];
            float view[16];
            float proj[16];
        };

        RHI_BufferHandle m_VertexBuffer = 0;
        RHI_BufferHandle m_IndexBuffer = 0;
        RHI_BufferHandle m_UboBuffer[3] = {0, 0, 0};
        
        RHI_ImageHandle m_TextureImage = 0;
        RHI_ImageViewHandle m_TextureView = 0;
        RHI_SamplerHandle m_Sampler = 0;

        RHI_PSOHandle m_Pso = nullptr;
        RHI_PipelineHandle m_Pipeline = 0;
        
        RHI_CommandBufferPoolHandle m_CmdPool = 0;

        RHI_GPUProgramHandle m_VertProgram = 0;
        RHI_GPUProgramHandle m_FragProgram = 0;

        RHI_RenderPassHandle m_RenderPass = 0;
        RHI_FrameBufferHandle m_FrameBuffer = 0;
        RHI_SubpassHandle m_Subpass = 0;
        
        Containers::Vector<UInt32> m_DescriptorPoolIds;
        Containers::Vector<UInt64> m_FrameTickets;
        RHI_DescriptorPoolHandle m_DescriptorPool = 0;

    public:
        TestCategory GetCategory() const override { return TestCategory::Rendering; }
        const char* GetName() const override { return "TextureMappingTest"; }

        bool SetupTest() override
        {
            HAL::InitDXC();
            InitRenderContext();
            CreateResources();
            InitShaderProgram();
            CreatePipeline();
            return true;
        }

        void TeardownTest() override
        {
            if (m_Device)
            {
                RHI_Device_WaitIdle(m_Device);

                RHI_Device_ReleaseBuffer(m_Device, m_VertexBuffer);
                RHI_Device_ReleaseBuffer(m_Device, m_IndexBuffer);
                for(int i=0; i<3; ++i) RHI_Device_ReleaseBuffer(m_Device, m_UboBuffer[i]);
                
                RHI_Device_ReleaseImage(m_Device, m_TextureImage);
                RHI_Device_ReleaseSampler(m_Device, m_Sampler);
                
                if (m_Pso) RHI_PSO_Destroy(m_Pso);
                
                RHI_Device_ReleaseGPUProgram(m_Device, m_VertProgram);
                RHI_Device_ReleaseGPUProgram(m_Device, m_FragProgram);

                if (m_RenderPass) RHI_Device_ReleaseRenderPass(m_Device, m_RenderPass);
                if (m_CmdPool) RHI_Device_ReleaseCommandBufferPool(m_Device, m_CmdPool);
                // m_FrameBuffer is usually a non-owning handle from Device in this test framework?
                // RHIBasicRenderingTest releases it though.
                if (m_FrameBuffer) RHI_Device_ReleaseFrameBuffer(m_Device, m_FrameBuffer);
            }
        }

        bool Run() override
        {
            MSG msg{};
            bool isRunning = true;
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

                Render();
            }
            return true;
        }

    private:
        void CreateResources()
        {
            // 1. Vertex Buffer
            Vertex vertices[] = {
                {{-0.5f, -0.5f, 0.0f}, {0.0f, 0.0f}},
                {{ 0.5f, -0.5f, 0.0f}, {1.0f, 0.0f}},
                {{ 0.5f,  0.5f, 0.0f}, {1.0f, 1.0f}},
                {{-0.5f,  0.5f, 0.0f}, {0.0f, 1.0f}}
            };
            
            RHI::RHIBufferDescriptor vDesc = {};
            vDesc.size = sizeof(vertices);
            vDesc.usage = RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT;
            vDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_VertexBuffer = RHI_Device_CreateBuffer(m_Device, &vDesc, "VertexBuffer");
            RHI_Buffer_MemoryCopy(m_Device, m_VertexBuffer, vertices, 0);

            // 2. Index Buffer
            uint16_t indices[] = { 0, 1, 2, 2, 3, 0 };
            RHI::RHIBufferDescriptor iDesc = {};
            iDesc.size = sizeof(indices);
            iDesc.usage = RHI::BUFFER_USAGE_INDEX_BUFFER_BIT;
            iDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_IndexBuffer = RHI_Device_CreateBuffer(m_Device, &iDesc, "IndexBuffer");
            RHI_Buffer_MemoryCopy(m_Device, m_IndexBuffer, indices, 0);

            // 3. UBO
            RHI::RHIBufferDescriptor uDesc = {};
            uDesc.size = sizeof(UBO);
            uDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
            uDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            for (int i = 0; i < 3; ++i) {
                m_UboBuffer[i] = RHI_Device_CreateBuffer(m_Device, &uDesc, "UBO");
            }

            // 4. Texture
            int texWidth, texHeight, texChannels;
            
            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            auto texturePath = (exeDir / "Assets" / "Arisen.png").string();

            stbi_uc* pixels = stbi_load(texturePath.c_str(), &texWidth, &texHeight, &texChannels, STBI_rgb_alpha);
            if (!pixels) {
                LOG_WARNF("Failed to load texture from {0}, falling back to white texture.", texturePath);
                texWidth = 16; texHeight = 16;
                pixels = (stbi_uc*)malloc(16 * 16 * 4);
                memset(pixels, 255, 16 * 16 * 4);
            }

            RHI::RHIImageDescriptor imgDesc = {};
            imgDesc.imageType = RHI::IMAGE_TYPE_2D;
            imgDesc.width = (UInt32)texWidth;
            imgDesc.height = (UInt32)texHeight;
            imgDesc.depth = 1;
            imgDesc.mipLevels = 1;
            imgDesc.arrayLayers = 1;
            imgDesc.format = RHI::FORMAT_R8G8B8A8_UNORM;
            imgDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
            imgDesc.imageLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
            imgDesc.usage = RHI::IMAGE_USAGE_TRANSFER_DST_BIT | RHI::IMAGE_USAGE_SAMPLED_BIT;
            imgDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
            imgDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;

            m_TextureImage = RHI_Device_CreateImage(m_Device, &imgDesc, "Texture");

            RHI::RHIImageViewDesc viewDesc = {};
            viewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            viewDesc.format = RHI::FORMAT_R8G8B8A8_UNORM;
            viewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
            viewDesc.baseMipLevel = 0;
            viewDesc.levelCount = 1;
            viewDesc.baseArrayLayer = 0;
            viewDesc.layerCount = 1;
            m_TextureView = RHI_Image_AddImageView(m_Device, m_TextureImage, &viewDesc);


            // 5. Sampler
            RHI::RHISamplerDesc samplerDesc = {};
            samplerDesc.magFilter = RHI::FILTER_LINEAR;
            samplerDesc.minFilter = RHI::FILTER_LINEAR;
            samplerDesc.addressModeU = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            samplerDesc.addressModeV = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            samplerDesc.addressModeW = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            m_Sampler = RHI_Device_CreateSampler(m_Device, &samplerDesc);

            // Upload image to GPU
            UploadImage(pixels, texWidth, texHeight);
            
            stbi_image_free(pixels);
        }

        void InitRenderContext()
        {
            m_CmdPool = RHI_Device_CreateCommandBufferPool(m_Device);
            m_RenderPass = RHI_Device_CreateRenderPass(m_Device);
            
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
            RHI_Subpass_SetDependency(m_Subpass, VK_SUBPASS_EXTERNAL, 
                RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, 0,
                RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT, 0);

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI_RenderPass_Alloc(m_Device, m_RenderPass, i);
                m_FrameTickets.emplace_back(0);
            }

            m_FrameBuffer = RHI_Device_GetFrameBuffer(m_Device);
            m_DescriptorPool = RHI_Device_GetDescriptorPool(m_Device);
            
            for(UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types { 
                    RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                    RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                    RHI::DESCRIPTOR_TYPE_SAMPLER
                };
                Containers::Vector<unsigned int> counts { 1, 1, 1 };
                unsigned int poolId = RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, 1);
                m_DescriptorPoolIds.emplace_back(poolId);
            }
        }

        void UploadImage(void* data, UInt32 width, UInt32 height)
        {
            UInt64 imageSize = (UInt64)width * height * 4;
            RHI::RHIBufferDescriptor tsb = {};
            tsb.size = imageSize;
            tsb.usage = RHI::BUFFER_USAGE_TRANSFER_SRC_BIT;
            tsb.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            
            auto stagingBuffer = RHI_Device_CreateBuffer(m_Device, &tsb, "TextureStaging");
            RHI_Buffer_MemoryCopy(m_Device, stagingBuffer, data, 0);

            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, 0);
            RHI_Cmd_Begin(cmd, 0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            
            Containers::Vector<RHI::RHIImageMemoryBarrier> b1;
            {
                RHI::RHIImageMemoryBarrier bar = {};
                bar.srcAccess = RHI::ACCESS_NONE;
                bar.dstAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
                bar.oldLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
                bar.newLayout = RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
                bar.srcQueueFamilyIndex = ~0U;
                bar.dstQueueFamilyIndex = ~0U;
                bar.image = *reinterpret_cast<RHI::RHIImageHandle*>(&m_TextureImage);
                bar.subresourceRange.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
                bar.subresourceRange.baseMipLevel = 0;
                bar.subresourceRange.levelCount = 1;
                bar.subresourceRange.baseArrayLayer = 0;
                bar.subresourceRange.layerCount = 1;
                bar.srcStageMask = RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT;
                bar.dstStageMask = RHI::PIPELINE_STAGE_TRANSFER_BIT;
                b1.push_back(bar);
            }
            RHI_Cmd_PipelineBarrier_Image(cmd, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_TRANSFER_BIT, 0, &b1);

            Containers::Vector<RHI::RHIBufferImageCopy> regions;
            {
                RHI::RHIBufferImageCopy region = {};
                region.imageSubresource.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
                region.imageSubresource.layerCount = 1;
                region.width = width;
                region.height = height;
                region.depth = 1;
                regions.push_back(region);
            }
            RHI_Cmd_CopyBufferToImage(cmd, stagingBuffer, m_TextureImage, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, &regions);

            Containers::Vector<RHI::RHIImageMemoryBarrier> b2;
            {
                RHI::RHIImageMemoryBarrier bar = {};
                bar.srcAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
                bar.dstAccess = RHI::ACCESS_SHADER_READ_BIT;
                bar.oldLayout = RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
                bar.newLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                bar.srcQueueFamilyIndex = ~0U;
                bar.dstQueueFamilyIndex = ~0U;
                bar.image = *reinterpret_cast<RHI::RHIImageHandle*>(&m_TextureImage);
                bar.subresourceRange.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
                bar.subresourceRange.baseMipLevel = 0;
                bar.subresourceRange.levelCount = 1;
                bar.subresourceRange.baseArrayLayer = 0;
                bar.subresourceRange.layerCount = 1;
                bar.srcStageMask = RHI::PIPELINE_STAGE_TRANSFER_BIT;
                bar.dstStageMask = RHI::PIPELINE_STAGE_FRAGMENT_SHADER_BIT;
                b2.push_back(bar);
            }
            RHI_Cmd_PipelineBarrier_Image(cmd, RHI::PIPELINE_STAGE_TRANSFER_BIT, RHI::PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, &b2);

            RHI_Cmd_End(cmd);
            RHI_Device_Submit(m_Device, cmd, 0);
            RHI_Device_WaitIdle(m_Device);
            RHI_Device_ReleaseBuffer(m_Device, stagingBuffer);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, 0, cmd);
        }

        void InitShaderProgram()
        {
            m_VertProgram = RHI_Device_CreateGPUProgram(m_Device);
            m_FragProgram = RHI_Device_CreateGPUProgram(m_Device);

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
            
            auto shaderFileName = L"TextureMapping";
            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            auto currentPath = exeDir.generic_wstring() + L"\\Shader";
            auto path = currentPath + L"\\" + shaderFileName + L".hlsl";

            // Vertex Shader
            HAL::ShaderCompileParams vertexParams
            {
                path,
                L"Vert",
                L"6_0",
                L"-spirv",
                envStr,
                L"0",
                RHI::EProgramStage::Vertex,
                {},
                {},
                currentPath + L"\\"+ shaderFileName + L".vert.spirv",
                true
            };

            HAL::ShaderCompilerOutput outputVertex;
            if (!HAL::CompileShaderFromFile(std::move(vertexParams), outputVertex) || outputVertex.codePointer == nullptr || outputVertex.codeSize == 0)
            {
                LOG_ERROR("Vertex shader compilation failed.");
                throw std::exception("Vertex shader compilation failed.");
            }

            {
                std::string nameStr = String::WStringToString(path);
                RHI::RHIShaderProgramDesc desc = { outputVertex.codeSize, outputVertex.codePointer, "Vert", nameStr.c_str(), RHI::SHADER_STAGE_VERTEX_BIT };
                RHI_Device_AttachProgramByteCode(m_Device, m_VertProgram, &desc);
            }
            if (outputVertex.codePointer) std::free(outputVertex.codePointer);

            // Fragment Shader
            HAL::ShaderCompileParams fragmentParams
            {
                path,
                L"Frag",
                L"6_0",
                L"-spirv",
                envStr,
                L"0",
                RHI::EProgramStage::Fragment,
                {},
                {},
                currentPath + L"\\" + shaderFileName + L".frag.spirv",
                true
            };

            HAL::ShaderCompilerOutput outputfragment;
            if (!HAL::CompileShaderFromFile(std::move(fragmentParams), outputfragment) || outputfragment.codePointer == nullptr || outputfragment.codeSize == 0)
            {
                LOG_ERROR("Fragment shader compilation failed.");
                throw std::exception("Fragment shader compilation failed.");
            }

            {
                std::string nameStr = String::WStringToString(path);
                RHI::RHIShaderProgramDesc desc = { outputfragment.codeSize, outputfragment.codePointer, "Frag", nameStr.c_str(), RHI::SHADER_STAGE_FRAGMENT_BIT };
                RHI_Device_AttachProgramByteCode(m_Device, m_FragProgram, &desc);
            }
            if (outputfragment.codePointer) std::free(outputfragment.codePointer);
        }

        void CreatePipeline()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);
            m_Pso = RHI_PipelineManager_CreatePSO(pm);

            RHI_PSO_AddProgram(m_Pso, m_VertProgram);
            RHI_PSO_AddProgram(m_Pso, m_FragProgram);

            RHI_PSO_AddVertexBindingDescription(m_Pso, 0, sizeof(Vertex), RHI::VERTEX_INPUT_RATE_VERTEX);
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 0, 0, RHI::FORMAT_R32G32B32_SFLOAT, offsetof(Vertex, pos));
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 1, 0, RHI::FORMAT_R32G32_SFLOAT, offsetof(Vertex, uv));

            Containers::Vector<RHI::RHIBufferHandle> buffers;
            buffers.push_back(*reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[0]));
            RHI_PSO_AddDescriptorSetLayoutBinding_Buffers(m_Pso, 0, 0, RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER, 1, RHI::SHADER_STAGE_VERTEX_BIT, &buffers);
            
            RHI_PSO_AddDescriptorSetLayoutBinding_Images(m_Pso, 0, 1, RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE, 1, RHI::SHADER_STAGE_FRAGMENT_BIT, nullptr);
            RHI_PSO_AddDescriptorSetLayoutBinding_Images(m_Pso, 0, 2, RHI::DESCRIPTOR_TYPE_SAMPLER, 1, RHI::SHADER_STAGE_FRAGMENT_BIT, nullptr);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);

            RHI_PSO_AddDynamicState(m_Pso, RHI::DYNAMIC_STATE_VIEWPORT);
            RHI_PSO_AddDynamicState(m_Pso, RHI::DYNAMIC_STATE_SCISSOR);
            RHI_PSO_AddBlendAttachmentState_Simple(m_Pso, false, 0xf);
            
            RHI_PSO_SetCullMode(m_Pso, RHI::CULL_MODE_NONE);
            RHI_PSO_SetFrontFace(m_Pso, RHI::FRONT_FACE_CLOCKWISE);
            RHI_PSO_SetSampleCount(m_Pso, RHI::SAMPLE_COUNT_1_BIT);

            m_Pipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_Pso);
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i) {
                RHI_Pipeline_AllocGraphics(m_Device, m_Pipeline, i, m_Subpass);
            }
        }

        void Render()
        {
            UInt32 currentIndex = GetCurrentFrameIndex();
            if (m_FrameTickets[currentIndex] > 0) {
                RHI_Device_WaitQueueTicket(m_Device, m_FrameTickets[currentIndex]);
            }

            UBO ubo = {};
            for(int i=0; i<16; ++i) ubo.model[i] = ubo.view[i] = ubo.proj[i] = 0;
            ubo.model[0] = ubo.model[5] = ubo.model[10] = ubo.model[15] = 1.0f;
            ubo.view[0] = ubo.view[5] = ubo.view[10] = ubo.view[15] = 1.0f;
            ubo.proj[0] = ubo.proj[5] = ubo.proj[10] = ubo.proj[15] = 1.0f;
            // Vulkan Y is down, but Arisen might be using DX layout. If screen is still black, try ubo.proj[5] = -1.0f;
            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffer[currentIndex], &ubo, 0);

            Containers::Vector<RHI::RHIBufferHandle> buffers;
            buffers.push_back(*reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[currentIndex]));
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 0, &buffers);

            RHI::RHIDescriptorImageInfo texInfo = {};
            texInfo.sampler = RHI::RHISamplerHandle::Invalid();
            texInfo.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&m_TextureView);
            texInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
            Containers::Vector<RHI::RHIDescriptorImageInfo> texInfos;
            texInfos.push_back(texInfo);
            RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 1, &texInfos);

            RHI::RHIDescriptorImageInfo samInfo = {};
            samInfo.sampler = *reinterpret_cast<RHI::RHISamplerHandle*>(&m_Sampler);
            samInfo.imageView = RHI::RHIImageViewHandle::Invalid();
            samInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
            Containers::Vector<RHI::RHIDescriptorImageInfo> samInfos;
            samInfos.push_back(samInfo);
            RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 2, &samInfos);

            RHI_DescriptorPool_Reset(m_DescriptorPool, m_DescriptorPoolIds[currentIndex]);
            RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[currentIndex], 0, m_Pso);
            RHI_DescriptorPool_UpdateDescriptorSets(m_DescriptorPool, m_DescriptorPoolIds[currentIndex], m_Pso);

            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, m_FrameIndex);
            RHI_Cmd_Begin(cmd, m_FrameIndex, 0);
            {
                auto surface = RHI_Instance_GetSurface(m_Instance, m_WindowId);
                auto swapchain = RHI_Surface_GetSwapChain(surface);
                RHI_ImageHandle backBuffer = RHI_SwapChain_AquireCurrentImage(swapchain, m_FrameIndex);
                if (backBuffer != 0) {
                    auto backBufferView = RHI_SwapChain_GetImageView(swapchain, m_FrameIndex);
                    RHI_RenderPass_Alloc(m_Device, m_RenderPass, m_FrameIndex);
                    RHI_FrameBuffer_SetAttachment(m_Device, m_FrameBuffer, m_FrameIndex, backBufferView, m_RenderPass);

                    RHI::RenderPassBeginDesc desc = {};
                    desc.renderPass = *reinterpret_cast<RHI::RHIRenderPassHandle*>(&m_RenderPass);
                    desc.frameBuffer = *reinterpret_cast<RHI::RHIFrameBufferHandle*>(&m_FrameBuffer);
                    desc.subpassContents = RHI::SUBPASS_CONTENTS_INLINE;
                    
                    RHI_Cmd_BeginRenderPass(cmd, m_FrameIndex, &desc);
                    {
                        RHI_Cmd_BindPipeline(cmd, m_FrameIndex, m_Pipeline);
                        Float32 w = (Float32)RHI_ImageView_GetWidth(m_Device, backBufferView);
                        Float32 h = (Float32)RHI_ImageView_GetHeight(m_Device, backBufferView);
                        RHI_Cmd_SetViewport(cmd, 0.0f, 0.0f, w, h, 0.0f, 1.0f);
                        RHI_Cmd_SetScissor(cmd, 0, 0, (UInt32)w, (UInt32)h);
                        RHI_Cmd_BindDescriptorSets_FromPool(cmd, m_FrameIndex, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPool, m_DescriptorPoolIds[currentIndex]);
                        RHI_Cmd_BindVertexBuffers(cmd, m_VertexBuffer, 0);
                        RHI_Cmd_BindIndexBuffer(cmd, m_IndexBuffer, 0, RHI::INDEX_TYPE_UINT16);
                        RHI_Cmd_DrawIndexed(cmd, 6, 1, 0, 0, 0, 0);
                    }
                    RHI_Cmd_EndRenderPass(cmd);

                    auto imageAvailableSem = RHI_SwapChain_GetImageAvailableSemaphore(swapchain, m_FrameIndex);
                    auto renderFinishedSem = RHI_SwapChain_GetRenderFinishSemaphore(swapchain, m_FrameIndex);
                    if (imageAvailableSem && renderFinishedSem) {
                        RHI_Cmd_WaitSemaphore(cmd, imageAvailableSem, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT);
                        RHI_Cmd_SignalSemaphore(cmd, renderFinishedSem);
                    }
                }
            }
            RHI_Cmd_End(cmd);
            m_FrameTickets[currentIndex] = RHI_Device_Submit(m_Device, cmd, m_FrameIndex);
            
            auto surface = RHI_Instance_GetSurface(m_Instance, m_WindowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            RHI_SwapChain_Present(swapchain, m_FrameIndex);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, m_FrameIndex, cmd);

            NextFrame();
        }
    };
}

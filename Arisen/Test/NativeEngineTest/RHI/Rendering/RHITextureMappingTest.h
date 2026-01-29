#pragma once

#include "../RHITestBase.h"
#include "../../../Engine/NativeEngine/RHI/PipelineExports.h"
#include "../../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../../Engine/NativeEngine/RHI/SyncExports.h"
#include "../../../Engine/NativeEngine/RHI/SurfaceExports.h"
#include "../../../Engine/NativeEngine/RHI/HandlesExports.h"
#include "../../../Engine/NativeEngine/RHI/DescriptorExports.h"

#include <stb_image.h>
#include <windows.h>

namespace ArisenEngine::Testing
{
    class RHITextureMappingTest : public RHITestBase
    {
        struct Vertex {
            float pos[3];
            float uv[2];
        };

        struct UBO {
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
        RHI_PipelineHandle m_Pipeline[3] = {0, 0, 0};
        
        RHI_CommandBufferPoolHandle m_CmdPool = 0;
        RHI_CommandBufferHandle m_CmdBuffers[3] = {0, 0, 0};

        RHI_GPUProgramHandle m_VertProgram = 0;
        RHI_GPUProgramHandle m_FragProgram = 0;

    public:
        TestCategory GetCategory() const override { return TestCategory::Rendering; }
        const char* GetName() const override { return "TextureMappingTest"; }

        bool SetupTest() override
        {
            CreateResources();
            CreatePipeline();
            return true;
        }

        void TeardownTest() override
        {
            if (m_Device)
            {
                RHI_Device_ReleaseBuffer(m_Device, m_VertexBuffer);
                RHI_Device_ReleaseBuffer(m_Device, m_IndexBuffer);
                for(int i=0; i<3; ++i) RHI_Device_ReleaseBuffer(m_Device, m_UboBuffer[i]);
                
                RHI_Device_ReleaseImage(m_Device, m_TextureImage);
                RHI_Device_ReleaseSampler(m_Device, m_Sampler);
                
                if (m_Pso) RHI_PSO_Destroy(m_Pso);
                
                RHI_Device_ReleaseGPUProgram(m_Device, m_VertProgram);
                RHI_Device_ReleaseGPUProgram(m_Device, m_FragProgram);
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
                Sleep(16);
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
            stbi_uc* pixels = stbi_load("Resources/Textures/checkerboard.png", &texWidth, &texHeight, &texChannels, STBI_rgb_alpha);
            if (!pixels) {
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

            stbi_image_free(pixels);

            // 5. Sampler
            RHI::RHISamplerDesc samplerDesc = {};
            samplerDesc.magFilter = RHI::FILTER_LINEAR;
            samplerDesc.minFilter = RHI::FILTER_LINEAR;
            samplerDesc.addressModeU = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            samplerDesc.addressModeV = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            samplerDesc.addressModeW = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            m_Sampler = RHI_Device_CreateSampler(m_Device, &samplerDesc);
        }

        void CreatePipeline()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);
            m_Pso = RHI_PipelineManager_CreatePSO(pm);

            m_VertProgram = RHI_Device_CreateGPUProgram(m_Device);
            m_FragProgram = RHI_Device_CreateGPUProgram(m_Device);
            
            RHI::RHIShaderProgramDesc vProgDesc = { 0, nullptr, "main", "TextureMapping.vert", RHI::SHADER_STAGE_VERTEX_BIT };
            RHI_Device_AttachProgramByteCode(m_Device, m_VertProgram, &vProgDesc);
            RHI_PSO_AddProgram(m_Pso, m_VertProgram);

            RHI::RHIShaderProgramDesc fProgDesc = { 0, nullptr, "main", "TextureMapping.frag", RHI::SHADER_STAGE_FRAGMENT_BIT };
            RHI_Device_AttachProgramByteCode(m_Device, m_FragProgram, &fProgDesc);
            RHI_PSO_AddProgram(m_Pso, m_FragProgram);

            RHI_PSO_AddVertexBindingDescription(m_Pso, 0, sizeof(Vertex), RHI::VERTEX_INPUT_RATE_VERTEX);
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 0, 0, RHI::FORMAT_R32G32B32_SFLOAT, offsetof(Vertex, pos));
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 1, 0, RHI::FORMAT_R32G32_SFLOAT, offsetof(Vertex, uv));

            Containers::Vector<RHI::RHIBufferHandle> buffers;
            buffers.push_back(*reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[0]));
            RHI_PSO_AddDescriptorSetLayoutBinding_Buffers(m_Pso, 0, 0, RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER, 1, RHI::SHADER_STAGE_VERTEX_BIT, &buffers);

            RHI::RHIDescriptorImageInfo imageInfo = { 
                *reinterpret_cast<RHI::RHISamplerHandle*>(&m_Sampler), 
                *reinterpret_cast<RHI::RHIImageViewHandle*>(&m_TextureView), 
                RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL 
            };
            Containers::Vector<RHI::RHIDescriptorImageInfo> images;
            images.push_back(imageInfo);
            RHI_PSO_AddDescriptorSetLayoutBinding_Images(m_Pso, 0, 1, RHI::DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER, 1, RHI::SHADER_STAGE_FRAGMENT_BIT, &images);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);

            Containers::Vector<RHI::EFormat> colorFormats;
            colorFormats.push_back(RHI::FORMAT_B8G8R8A8_SRGB);
            RHI_PSO_SetRenderingFormats(m_Pso, &colorFormats, RHI::FORMAT_UNDEFINED, RHI::FORMAT_UNDEFINED);

            for (int i = 0; i < 3; ++i) {
                m_Pipeline[i] = RHI_PipelineManager_GetGraphicsPipeline(pm, m_Pso);
                RHI_Pipeline_AllocGraphics(m_Device, m_Pipeline[i], i, 0);
            }
        }

        void Render()
        {
            UInt32 frameIndex = GetCurrentFrameIndex();
            
            UBO ubo = {};
            // Identity matrix setup (simplified)
            for(int i=0; i<16; ++i) ubo.model[i] = ubo.view[i] = ubo.proj[i] = 0;
            ubo.model[0] = ubo.model[5] = ubo.model[10] = ubo.model[15] = 1.0f;
            ubo.view[0] = ubo.view[5] = ubo.view[10] = ubo.view[15] = 1.0f;
            ubo.proj[0] = ubo.proj[5] = ubo.proj[10] = ubo.proj[15] = 1.0f;

            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffer[frameIndex], &ubo, 0);
            
            NextFrame();
        }
    };
}

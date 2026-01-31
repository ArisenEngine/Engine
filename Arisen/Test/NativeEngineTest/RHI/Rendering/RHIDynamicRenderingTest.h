#pragma once

#include "../RHIRenderingTestBase.h"



namespace ArisenEngine::Testing
{
    class RHIDynamicRenderingTest : public RHIRenderingTestBase
    {
    private:
        RHI_PSOHandle m_Pso = nullptr;
        RHI_PipelineHandle m_Pipeline = 0;
        Containers::Vector<RHI_BufferHandle> m_UboBuffer;
        RHI_ImageHandle m_Texture = 0;
        RHI_SamplerHandle m_Sampler = 0;

        Containers::Vector<RHI::RHIImageMemoryBarrier> m_CachedBarriers;
        RHI::RHIRenderingInfo m_CachedRenderingInfo;
        RHI::RHIRenderingAttachmentInfo m_CachedColorAtt;

    public:
        const char* GetName() const override { return "DynamicRenderingTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            RHIRenderingTestBase::SetupTest();

            InitCommonResources();
            InitShaderProgram(L"StandardTest");
            CreateResources();
            CreatePipeline();

            return true;
        }

        void TeardownTest() override
        {
            if (m_Sampler) RHI_Device_ReleaseSampler(m_Device, m_Sampler);
            if (m_Texture) RHI_Device_ReleaseImage(m_Device, m_Texture);
            
            for (auto& ub : m_UboBuffer)
            {
                if (ub) RHI_Device_ReleaseBuffer(m_Device, ub);
            }
            m_UboBuffer.clear();

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
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = std::filesystem::path(exePathW).parent_path();
            
            m_Model = LoadGLTF((exeDir / "Assets" / "Buggy.gltf").string());

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor ubDesc = {};
                ubDesc.size = sizeof(UniformBufferObject);
                ubDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                ubDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_UboBuffer.push_back(RHI_Device_CreateBuffer(m_Device, &ubDesc, "UBO"));
            }

            // Texture
            int texWidth, texHeight, texChannels;
            auto texturePath = (exeDir / "Assets" / "Arisen.png").string();
            stbi_uc* pixels = stbi_load(texturePath.c_str(), &texWidth, &texHeight, &texChannels, STBI_rgb_alpha);
            if (pixels)
            {
                RHI::RHIImageDescriptor texDesc = {};
                texDesc.imageType = RHI::IMAGE_TYPE_2D;
                texDesc.width = texWidth;
                texDesc.height = texHeight;
                texDesc.depth = 1;
                texDesc.mipLevels = 1;
                texDesc.arrayLayers = 1;
                texDesc.format = RHI::FORMAT_R8G8B8A8_SRGB;
                texDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
                texDesc.usage = RHI::IMAGE_USAGE_TRANSFER_DST_BIT | RHI::IMAGE_USAGE_SAMPLED_BIT;
                texDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
                m_Texture = RHI_Device_CreateImage(m_Device, &texDesc, "Texture");

                RHI::RHIImageViewDesc viewDesc = {};
                viewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
                viewDesc.format = RHI::FORMAT_R8G8B8A8_SRGB;
                viewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
                viewDesc.levelCount = 1;
                viewDesc.layerCount = 1;
                RHI_Image_AddImageView(m_Device, m_Texture, &viewDesc);

                UploadImage(m_Texture, (UInt64)texWidth * texHeight * 4, pixels, texWidth, texHeight);
                stbi_image_free(pixels);
            }

            RHI::RHISamplerDesc sampDesc = {};
            sampDesc.magFilter = RHI::FILTER_LINEAR;
            sampDesc.minFilter = RHI::FILTER_LINEAR;
            m_Sampler = RHI_Device_CreateSampler(m_Device, &sampDesc);

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types = { RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER, RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE, RHI::DESCRIPTOR_TYPE_SAMPLER };
                Containers::Vector<UInt32> counts = { 1, 1, 1 };
                m_DescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, 1));
            }
        }

        void CreatePipeline()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);
            m_Pso = RHI_PipelineManager_CreatePSO(pm);

            RHI_PSO_AddProgram(m_Pso, m_VertProgram);
            RHI_PSO_AddProgram(m_Pso, m_FragProgram);

            RHI_PSO_AddVertexBindingDescription(m_Pso, 0, sizeof(GLTFVertex), RHI::VERTEX_INPUT_RATE_VERTEX);
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 0, 0, RHI::FORMAT_R32G32B32_SFLOAT, offsetof(GLTFVertex, pos));
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 1, 0, RHI::FORMAT_R32G32B32_SFLOAT, offsetof(GLTFVertex, normal));
            RHI_PSO_AddVertexInputAttributeDescription(m_Pso, 2, 0, RHI::FORMAT_R32G32_SFLOAT, offsetof(GLTFVertex, uv));

            Containers::Vector<RHI::RHIBufferHandle> buffers;
            buffers.push_back(*reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[0]));
            RHI_PSO_AddDescriptorSetLayoutBinding_Buffers(m_Pso, 0, 0, RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER, 1, RHI::SHADER_STAGE_VERTEX_BIT, &buffers);

            RHI_PSO_AddDescriptorSetLayoutBinding_Images(m_Pso, 0, 1, RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE, 1, RHI::SHADER_STAGE_FRAGMENT_BIT, nullptr);
            RHI_PSO_AddDescriptorSetLayoutBinding_Images(m_Pso, 0, 2, RHI::DESCRIPTOR_TYPE_SAMPLER, 1, RHI::SHADER_STAGE_FRAGMENT_BIT, nullptr);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);

            RHI_PSO_AddDynamicState(m_Pso, RHI::DYNAMIC_STATE_VIEWPORT);
            RHI_PSO_AddDynamicState(m_Pso, RHI::DYNAMIC_STATE_SCISSOR);

            Containers::Vector<RHI::EFormat> colorFormats = { RHI::FORMAT_B8G8R8A8_SRGB };
            RHI_PSO_SetRenderingFormats(m_Pso, &colorFormats, RHI::FORMAT_UNDEFINED, RHI::FORMAT_UNDEFINED);

            m_Pipeline = RHI_PipelineManager_GetGraphicsPipeline(pm, m_Pso);
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i) {
                RHI_Pipeline_AllocGraphics(m_Device, m_Pipeline, i, nullptr);
            }
        }

        void UpdateUniformBuffer()
        {
            UpdateCamera((float)frameTime);
            UniformBufferObject ubo;
            ubo.model = glm::mat4(1.0f);
            ubo.view = GetViewMatrix();
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);
            ubo.proj = GetProjectionMatrix(width / height);
            RHI_Buffer_MemoryCopy(m_Device, m_UboBuffer[m_FrameIndex], &ubo, 0);
        }

        void RecordAndSubmit()
        {
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, m_FrameIndex);

            // Update descriptors
            Containers::Vector<RHI::RHIBufferHandle> ubos = { *reinterpret_cast<RHI::RHIBufferHandle*>(&m_UboBuffer[m_FrameIndex]) };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 0, &ubos);

            auto texView = RHI_Image_GetView(m_Device, m_Texture);
            RHI::RHIDescriptorImageInfo texInfo = {};
            texInfo.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&texView);
            texInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
            Containers::Vector<RHI::RHIDescriptorImageInfo> texInfos = { texInfo };
            RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 1, &texInfos);

            RHI::RHIDescriptorImageInfo samInfo = {};
            samInfo.sampler = *reinterpret_cast<RHI::RHISamplerHandle*>(&m_Sampler);
            samInfo.imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
            Containers::Vector<RHI::RHIDescriptorImageInfo> samInfos = { samInfo };
            RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 2, &samInfos);

            RHI_DescriptorPool_Reset(m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex]);
            RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex], 0, m_Pso);
            RHI_DescriptorPool_UpdateDescriptorSets(m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex], m_Pso);

            RHI_Cmd_Begin(cmd, m_FrameIndex, 0);

            auto surface = RHI_Instance_GetSurface(m_Instance, m_WindowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            auto colorBuffer = RHI_SwapChain_AquireCurrentImage(swapchain, m_FrameIndex);
            if (colorBuffer)
            {
                auto colorView = RHI_SwapChain_GetImageView(swapchain, m_FrameIndex);

                // Transition to color attachment optimal
                m_CachedBarriers.clear();
                RHI::RHIImageMemoryBarrier barrier{};
                barrier.image = *reinterpret_cast<RHI::RHIImageHandle*>(&colorBuffer);
                barrier.oldLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
                barrier.newLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                barrier.srcAccess = RHI::ACCESS_NONE;
                barrier.dstAccess = RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
                barrier.srcStageMask = RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT;
                barrier.dstStageMask = RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
                m_CachedBarriers.push_back(barrier);
                RHI_Cmd_PipelineBarrier_Image(cmd, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, 0, &m_CachedBarriers);

                m_CachedColorAtt = {};
                m_CachedColorAtt.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&colorView);
                m_CachedColorAtt.imageLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                m_CachedColorAtt.loadOp = RHI::ATTACHMENT_LOAD_OP_CLEAR;
                m_CachedColorAtt.storeOp = RHI::ATTACHMENT_STORE_OP_STORE;
                m_CachedColorAtt.clearValue.float32[0] = 0.0f;
                m_CachedColorAtt.clearValue.float32[1] = 0.0f;
                m_CachedColorAtt.clearValue.float32[2] = 0.2f;
                m_CachedColorAtt.clearValue.float32[3] = 1.0f;

                m_CachedRenderingInfo = {};
                UInt32 width = HAL::GetWindowWidth(m_WindowId);
                UInt32 height = HAL::GetWindowHeight(m_WindowId);
                m_CachedRenderingInfo.RHIRenderArea = { 0, 0, width, height };
                m_CachedRenderingInfo.layerCount = 1;
                m_CachedRenderingInfo.colorAttachmentCount = 1;
                m_CachedRenderingInfo.pColorAttachments = &m_CachedColorAtt;

                RHI_Cmd_BeginRendering(cmd, &m_CachedRenderingInfo);
                RHI_Cmd_BindPipeline(cmd, m_FrameIndex, m_Pipeline);
                RHI_Cmd_SetViewport(cmd, 0, 0, (float)width, (float)height, 0, 1);
                RHI_Cmd_SetScissor(cmd, 0, 0, width, height);
                RHI_Cmd_BindDescriptorSets_FromPool(cmd, m_FrameIndex, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, m_DescriptorPool, m_DescriptorPoolIds[m_FrameIndex]);
                RHI_Cmd_BindVertexBuffers(cmd, m_Model.vertexBuffer, 0);
                RHI_Cmd_BindIndexBuffer(cmd, m_Model.indexBuffer, 0, RHI::INDEX_TYPE_UINT32);
                RHI_Cmd_DrawIndexed(cmd, m_Model.indexCount, 1, 0, 0, 0, 0);
                RHI_Cmd_EndRendering(cmd);

                // Transition to present
                m_CachedBarriers.clear();
                barrier.oldLayout = RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                barrier.newLayout = RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR;
                barrier.srcAccess = RHI::ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                barrier.dstAccess = RHI::ACCESS_NONE;
                barrier.srcStageMask = RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
                barrier.dstStageMask = RHI::PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT;
                m_CachedBarriers.push_back(barrier);
                RHI_Cmd_PipelineBarrier_Image(cmd, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, RHI::PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, 0, &m_CachedBarriers);

                auto imageAvailableSem = RHI_SwapChain_GetImageAvailableSemaphore(swapchain, m_FrameIndex);
                auto renderFinishedSem = RHI_SwapChain_GetRenderFinishSemaphore(swapchain, m_FrameIndex);
                RHI_Cmd_WaitSemaphore(cmd, imageAvailableSem, RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT);
                RHI_Cmd_SignalSemaphore(cmd, renderFinishedSem);
            }

            RHI_Cmd_End(cmd);
            m_FrameTickets[m_FrameIndex] = RHI_Device_Submit(m_Device, cmd, m_FrameIndex);
            RHI_SwapChain_Present(swapchain, m_FrameIndex);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, m_FrameIndex, cmd);
        }
    };
}

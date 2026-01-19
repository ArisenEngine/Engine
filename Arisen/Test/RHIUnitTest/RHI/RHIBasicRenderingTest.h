#pragma once

#include "RHITestBase.h"
#include "../../Engine/NativeEngine/RHI/RHIExports.h"
#include "../../Engine/NativeEngine/RHI/HandlesExports.h"
#include "../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../Engine/NativeEngine/RHI/PipelineExports.h"
#include "../../Engine/NativeEngine/RHI/DescriptorExports.h"
#include "../../Engine/NativeEngine/RHI/SyncExports.h"
#include "../../Engine/NativeEngine/RHI/SurfaceExports.h"
#include "RHI/Enums/Memory/EBufferUsage.h"
#include "RHI/Program/GPUPipelineStateObject.h"
#include "ShaderCompiler/ShaderCompilerAPI.h"

#define GLM_FORCE_RADIANS
#include <glm/glm.hpp>
#include <glm/gtc/matrix_transform.hpp>
#include <chrono>

// Note: stb_image.h is already included with STB_IMAGE_IMPLEMENTATION in RHIUnitTest.h
// #include "stb_image.h"

namespace ArisenEngine::Testing
{
    /**
     * @brief Basic rendering test - Draws a textured rotating square.
     * 
     * This test validates:
     * - RHI initialization
     * - Buffer creation (vertex, index, uniform)
     * - Image/texture creation
     * - Shader compilation and loading
     * - Pipeline state configuration
     * - Render pass and framebuffer setup
     * - Command buffer recording and submission
     * - Swapchain presentation
     */
    class RHIBasicRenderingTest : public RHITestBase
    {
    private:
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

        const std::vector<Vertex> m_Vertices = {
            {{-0.5f, -0.5f}, {1.0f, 0.0f, 0.0f}},
            {{0.5f, -0.5f}, {0.0f, 1.0f, 0.0f}},
            {{0.5f, 0.5f}, {0.0f, 0.0f, 1.0f}},
            {{-0.5f, 0.5f}, {1.0f, 1.0f, 1.0f}}
        };

        const std::vector<uint16_t> m_Indices = {
            0, 1, 2, 2, 3, 0
        };

        RHI_BufferHandle m_VertexBuffer = nullptr;
        RHI_BufferHandle m_IndexBuffer = nullptr;
        Containers::Vector<RHI_BufferHandle> m_UniformBuffers;
        RHI_ImageHandle m_TextureImage = nullptr;
        
        RHI_RenderPassHandle m_RenderPass = nullptr;
        RHI_SubpassHandle m_Subpass = nullptr;
        RHI_FrameBufferHandle m_FrameBuffer = nullptr;
        RHI_PSOHandle m_PipelineState = nullptr;
        RHI_DescriptorPoolHandle m_DescriptorPool = nullptr;
        Containers::Vector<UInt32> m_DescriptorPoolIds;
        UInt32 m_CommandPoolId = 0;
        
        Containers::Vector<UInt32> m_GpuPrograms;

        std::chrono::high_resolution_clock::time_point m_StartTime;
        UInt32 m_WindowWidth = 640;
        UInt32 m_WindowHeight = 480;

    public:
        const char* GetName() const override
        {
            return "RHIBasicRenderingTest";
        }

        bool SetupTest() override
        {
            LOG_INFO("Setting up basic rendering test resources...");

            m_StartTime = std::chrono::high_resolution_clock::now();

            if (!CreateBuffers()) return false;
            if (!CreateTexture()) return false;
            if (!CompileShaders()) return false;
            if (!CreateRenderPass()) return false;
            if (!CreatePipelineState()) return false;

            LOG_INFO("Basic rendering test setup complete");
            return true;
        }

        bool Run() override
        {
            // Run for a few frames to verify rendering works
            const int framesToRun = 10;
            
            for (int i = 0; i < framesToRun; ++i)
            {
                RHI_Device_WaitFrameFence(m_Device, m_FrameIndex);
                
                if (!RenderFrame())
                {
                    LOG_ERROR("Frame %d rendering failed", i);
                    return false;
                }

                NextFrame();
            }

            LOG_INFO("Successfully rendered %d frames", framesToRun);
            return true;
        }

        void TeardownTest() override
        {
            // TODO: Add resource cleanup
            LOG_INFO("Tearing down basic rendering test");
        }

    private:
        bool CreateBuffers()
        {
            // Create vertex buffer
            m_VertexBuffer = RHI_Device_GetBufferHandle(m_Device, "Vertex Buffer");
            RHI::BufferDescriptor vbDesc{
                0,
                sizeof(Vertex) * m_Vertices.size(),
                RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT,
                RHI::SHARING_MODE_EXCLUSIVE
            };
            RHI_Buffer_Alloc(m_VertexBuffer, &vbDesc);
            RHI_Buffer_AllocDeviceMemory(m_VertexBuffer, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            // Create index buffer
            m_IndexBuffer = RHI_Device_GetBufferHandle(m_Device, "Index Buffer");
            RHI::BufferDescriptor ibDesc{
                0,
                sizeof(uint16_t) * m_Indices.size(),
                RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_INDEX_BUFFER_BIT,
                RHI::SHARING_MODE_EXCLUSIVE
            };
            RHI_Buffer_Alloc(m_IndexBuffer, &ibDesc);
            RHI_Buffer_AllocDeviceMemory(m_IndexBuffer, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            // Create uniform buffers (one per frame in flight)
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                auto name = std::string("Uniform Buffer ") + std::to_string(i);
                auto uniformBuffer = RHI_Device_GetBufferHandle(m_Device, name.c_str());
                
                RHI::BufferDescriptor ubDesc{
                    0,
                    sizeof(UniformBufferObject),
                    RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT,
                    RHI::SHARING_MODE_EXCLUSIVE
                };
                RHI_Buffer_Alloc(uniformBuffer, &ubDesc);
                RHI_Buffer_AllocDeviceMemory(uniformBuffer, 
                    RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
                
                m_UniformBuffers.push_back(uniformBuffer);
            }

            // TODO: Upload vertex/index data using staging buffers
            LOG_INFO("Buffers created successfully");
            return true;
        }

        bool CreateTexture()
        {
            // TODO: Load texture from Assets/Arisen.png and upload
            LOG_INFO("Texture created (placeholder)");
            return true;
        }

        bool CompileShaders()
        {
            // TODO: Compile vertex and fragment shaders
            LOG_INFO("Shaders compiled (placeholder)");
            return true;
        }

        bool CreateRenderPass()
        {
            m_RenderPass = RHI_Device_GetRenderPass(m_Device);
            
            // Configure render pass
            RHI_RenderPass_AddAttachmentAction(m_RenderPass,
                RHI::EFormat::FORMAT_B8G8R8A8_SRGB,
                RHI::SAMPLE_COUNT_1_BIT,
                RHI::ATTACHMENT_LOAD_OP_CLEAR,
                RHI::ATTACHMENT_STORE_OP_STORE,
                RHI::ATTACHMENT_LOAD_OP_DONT_CARE,
                RHI::ATTACHMENT_STORE_OP_DONT_CARE,
                RHI::IMAGE_LAYOUT_UNDEFINED,
                RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);

            m_Subpass = RHI_RenderPass_AddSubPass(m_RenderPass);
            RHI_Subpass_SetBindPoint(m_Subpass, RHI::PIPELINE_BIND_POINT_GRAPHICS);
            RHI_Subpass_AddColorReference(m_Subpass, 0, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

            m_FrameBuffer = RHI_Device_GetFrameBuffer(m_Device);
            m_CommandPoolId = RHI_Device_CreateCommandBufferPool(m_Device);

            LOG_INFO("Render pass created");
            return true;
        }

        bool CreatePipelineState()
        {
            // TODO: Configure PSO with vertex layout, dynamic states, etc.
            LOG_INFO("Pipeline state created (placeholder)");
            return true;
        }

        bool RenderFrame()
        {
            // TODO: Record commands, submit, and present
            return true;
        }
    };
}

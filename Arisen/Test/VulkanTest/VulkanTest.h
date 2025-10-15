#pragma once
#include <filesystem>
#include "Windows/PlatformTypes.h"
#include "Test.h"
#include "RHI\RHILoader.h"
#include "RHI/Instance.h"
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
#include "RHI/Handles/ImageHandle.h"
#include "RHI/Memory/ImageView.h"
#include "RHI/Synchronization/RHIImageMemoryBarrier.h"
#include "RHI/CommandBuffer/RHICommandBuffer.h"
#include "RHI/CommandBuffer/RHICommandBufferPool.h"
#include "RHI/Program/GPUPipelineManager.h"
#include "RHI/Program/GPURenderPass.h"
#include "RHI/Program/GPUSubPass.h"
#include "RHI/Program/GPUPipelineStateObject.h"
#include "Windows/RenderWindowAPI.h"
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
#define GLM_FORCE_RADIANS
#include <glm/glm.hpp>
#include <glm/gtc/matrix_transform.hpp>

#include <chrono>
#include "RHI/Handles/BufferHandle.h"

#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"
#include "vulkan_core.h"

using namespace ArisenEngine;

#ifdef TEST_WINDOWS

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
    unsigned int commandPoolId;
    RHI_DescriptorPoolHandle descriptorPool;
    Containers::Vector<UInt32> gpuPrograms;
    Containers::Vector<UInt32> descriptorPoolIds;
    bool bShouldResize;
};

Containers::Vector<RenderContext> g_RenderContexts;

RHI::RHIFactory* gRHIFactory = nullptr;
const int k_WindowsCount = 1;

void WinResize(HWND hwnd, UInt32 width, UInt32 height)
{
    auto id = Platforms::GetWindowId(hwnd);
    for (int i = 0; i < k_WindowsCount; ++i)
    {
        if (g_RenderContexts[i].windowId == id)
        {
            g_RenderContexts[i].bShouldResize = true;
            g_RenderContexts[i].newWidth = width;
            g_RenderContexts[i].newHeight = height;
        }
    }
}

LRESULT WinProc(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam)
{
    switch (msg)
    {
    case WM_DESTROY:
        {
            PostQuitMessage(0);
        }

        break;
    case WM_SYSCHAR:
        {
            if (wparam == VK_RETURN && (HIWORD(lparam) & KF_ALTDOWN))
            {
                return 0;
            }
        }
        break;
    case WM_SIZE:
        {
           
        }
        break;
    }
    return DefWindowProc(hwnd, msg, wparam, lparam);
}

struct Vertex
{
    glm::vec2 pos;
    glm::vec3 color;
};

// Vulkan require using std140 alignment
struct UniformBufferObject {
   alignas(16) glm::mat4 model;
   alignas(16) glm::mat4 view;
   alignas(16) glm::mat4 proj;
};

const std::vector<Vertex> vertices = {
    {{-0.5f, -0.5f}, {1.0f, 0.0f, 0.0f}},
    {{0.5f, -0.5f}, {0.0f, 1.0f, 0.0f}},
    {{0.5f, 0.5f}, {0.0f, 0.0f, 1.0f}},
    {{-0.5f, 0.5f}, {1.0f, 1.0f, 1.0f}}
};

const std::vector<uint16_t> indices = {
    0, 1, 2, 2, 3, 0
};

using Clock = std::chrono::high_resolution_clock;
// 初始化计时器
auto lastTime = Clock::now();
double frameTime = 0.0;  // 单帧耗时，单位：秒
double fps = 0.0;


class EngineTest : public Test
{
private:
    UInt32 frameIndex {0};
    RHI_InstanceHandle m_Instance{};
    UInt32 m_MaxFramesInFlight {2};

public:
    EngineTest(): m_Instance(nullptr)
    {
        // std::set_terminate([](){
        //     Debugger::Logger::Shutdown();
        // });
    }

    void CreateRenderWindow()
    {
        g_RenderContexts.resize(k_WindowsCount);

        for (int i = 0; i < k_WindowsCount; ++i)
        {
            auto windowId = Platforms::CreateRenderWindowWithResizeCallback(nullptr, WinProc, WinResize, 640, 480);
            g_RenderContexts[i] = RenderContext
            {
                windowId,
                640, 480
            };
        }
    }

    void InitRenderHardwareDriver()
    {
        RHI::InstanceInfo app_info
       {
           /** app name */
           " Engine Test",
           /** engine name */
           "Engine Test",
           /** enable validation layer */
           true,
           /** API Version */
           0, 1, 3, 0,
           /** App Version */
           1, 0, 0,
           /** App Version */
           1, 0, 0,
           /* Max Frames in Flight */
           2
       };

        RHI_SetGraphicsAPI(RHI::GraphsicsAPI::Vulkan);
        m_Instance = RHI_CreateInstance(&app_info);
        m_MaxFramesInFlight = RHI_Instance_GetMaxFramesInFlight(m_Instance);
        // env string will be retrieved when compiling shaders
        // LOG_INFO(std::move(env));

        // init surfaces
        for (auto& renderContext : g_RenderContexts)
        {
            RHI_Instance_CreateSurface(m_Instance, renderContext.windowId);
        }

        // pick physical device
        RHI_Instance_PickPhysicalDevice(m_Instance, true);

        // init logical devices
        RHI_Instance_InitLogicDevices(m_Instance);
        
    }

    void InitRenderContext()
    {
        for (int i = 0; i < k_WindowsCount; ++i)
        {
            g_RenderContexts[i].device = RHI_Instance_GetLogicalDevice(m_Instance, g_RenderContexts[i].windowId);
            g_RenderContexts[i].commandPoolId = RHI_Device_CreateCommandBufferPool(g_RenderContexts[i].device);
            g_RenderContexts[i].renderPass = RHI_Device_GetRenderPass(g_RenderContexts[i].device);
            g_RenderContexts[i].frameBuffer = RHI_Device_GetFrameBuffer(g_RenderContexts[i].device);
            g_RenderContexts[i].vertexBufferHandle = RHI_Device_GetBufferHandle(g_RenderContexts[i].device, "Vertex Buffer");
            g_RenderContexts[i].indicesBufferHandle = RHI_Device_GetBufferHandle(g_RenderContexts[i].device, "Indices Buffer");
            g_RenderContexts[i].textureHandle = RHI_Device_GetImageHandle(g_RenderContexts[i].device, "Texture Image");
            g_RenderContexts[i].descriptorPool = RHI_Device_GetDescriptorPool(g_RenderContexts[i].device);
            for(int frameIndex = 0; frameIndex < (int)m_MaxFramesInFlight; ++frameIndex)
            {
                Containers::Vector<RHI::EDescriptorType> types { RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER };
                Containers::Vector<unsigned int> counts { 1 };
                unsigned int poolId = RHI_DescriptorPool_AddPool(g_RenderContexts[i].descriptorPool, &types, &counts, 1);
                g_RenderContexts[i].descriptorPoolIds.emplace_back(poolId);
                auto name = std::string("Uniform Buffer ") + std::to_string(frameIndex);
                g_RenderContexts[i].uniformBuffers.emplace_back(RHI_Device_GetBufferHandle(g_RenderContexts[i].device, name.c_str()));
            }
        }
    }


    void InitShaderProgram()
    {
        // Retrieve environment string from instance (wide string)
        std::wstring envStr;
        {
            unsigned int len = RHI_Instance_GetEnvStringW(m_Instance, nullptr, 0);
            if (len > 0)
            {
                std::wstring tmp;
                tmp.resize(len ? (len - 1) : 0);
                if (len > 1)
                {
                    RHI_Instance_GetEnvStringW(m_Instance, tmp.data(), len);
                }
                envStr = std::move(tmp);
            }
        }
        auto shaderFileName = L"UniformBuffers";
        namespace fs = std::filesystem;
        auto currentPath = fs::current_path().generic_wstring() + L"\\Shader";
        auto path = currentPath + L"\\" + shaderFileName + L".hlsl";

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
        if (Platforms::CompileShaderFromFile(std::move(vertexParams), outputVertex))
        {
            LOG_DEBUG("Vertex Shader Compilation done.");
        }

        for (int i = 0; i < k_WindowsCount; ++i)
        {
            auto programId = RHI_Device_CreateGPUProgram(g_RenderContexts[i].device);
            auto desc = RHI::GPUProgramDesc
            {
                outputVertex.codeSize,
                outputVertex.codePointer,
                "Vert",
                String::WStringToString(path).c_str(),
                RHI::SHADER_STAGE_VERTEX_BIT
            };
            RHI_Device_AttachProgramByteCode(g_RenderContexts[i].device, programId, &desc);
            g_RenderContexts[i].gpuPrograms.emplace_back(programId);
        }


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
        if (Platforms::CompileShaderFromFile(std::move(fragmentParams), outputfragment))
        {
            LOG_DEBUG("Fragment Shader Compilation done.");
        }

        for (int i = 0; i < k_WindowsCount; ++i)
        {
            auto programId = RHI_Device_CreateGPUProgram(g_RenderContexts[i].device);
            auto desc = RHI::GPUProgramDesc
            {
                outputfragment.codeSize,
                outputfragment.codePointer,
                "Frag",
                String::WStringToString(path).c_str(),
                RHI::SHADER_STAGE_FRAGMENT_BIT
            };
            RHI_Device_AttachProgramByteCode(g_RenderContexts[i].device, programId, &desc);
            g_RenderContexts[i].gpuPrograms.emplace_back(programId);
        }
    }

    void InitBuffer()
    {
        // Init Buffer
        for (int i = 0; i < k_WindowsCount; ++i)
        {
            RHI::BufferDescriptor vbDesc{
                0,
                sizeof(vertices[0]) * vertices.size(),
                RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT,
                RHI::SHARING_MODE_EXCLUSIVE
            };
            RHI_Buffer_Alloc(g_RenderContexts[i].vertexBufferHandle, &vbDesc);
            RHI_Buffer_AllocDeviceMemory(g_RenderContexts[i].vertexBufferHandle, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            RHI::BufferDescriptor ibDesc{
                0,
                sizeof(indices[0]) * indices.size(),
                RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_INDEX_BUFFER_BIT,
                RHI::SHARING_MODE_EXCLUSIVE
            };
            RHI_Buffer_Alloc(g_RenderContexts[i].indicesBufferHandle, &ibDesc);
            RHI_Buffer_AllocDeviceMemory(g_RenderContexts[i].indicesBufferHandle, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            for (const auto& uniformBuffer : g_RenderContexts[i].uniformBuffers)
            {
                RHI::BufferDescriptor ubDesc{
                    0,
                    sizeof(UniformBufferObject),
                    RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT,
                    RHI::SHARING_MODE_EXCLUSIVE
                };
                RHI_Buffer_Alloc(uniformBuffer, &ubDesc);
                RHI_Buffer_AllocDeviceMemory(uniformBuffer, RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
            }
            
            UploadVertex(g_RenderContexts[i]);
        }
    }
    
    void CreateImage()
    {
        int texWidth, texHeight, texChannels;
        stbi_uc* pixels = stbi_load("Assets/Arisen.png",
            &texWidth, &texHeight, &texChannels, STBI_rgb_alpha);
        UInt64 imageSize = texWidth * texHeight * 4;

        if (!pixels)
        {
            LOG_ERROR("failed to load texture image!");
        }

        for (int i = 0; i < k_WindowsCount; ++i)
        {
            RHI::ImageDescriptor imgDesc{
                RHI::IMAGE_TYPE_2D, static_cast<UInt32>(texWidth), static_cast<UInt32>(texHeight), 1,
                1, 1, RHI::FORMAT_R8G8B8A8_SRGB, RHI::IMAGE_TILING_OPTIMAL,
                RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_USAGE_SAMPLED_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT,
                RHI::SAMPLE_COUNT_1_BIT, RHI::SHARING_MODE_EXCLUSIVE
            };
            RHI_Image_Alloc(g_RenderContexts[i].textureHandle, &imgDesc);
            RHI_Image_AllocDeviceMemory(g_RenderContexts[i].textureHandle, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
            RHI::ImageViewDesc imageViewDesc {
                RHI::IMAGE_VIEW_TYPE_2D, RHI::FORMAT_R8G8B8A8_SRGB, RHI::IMAGE_ASPECT_COLOR_BIT,
                0, 1, 0, 1,
            };
            imageViewDesc.width = static_cast<UInt32>(texWidth);
            imageViewDesc.height = static_cast<UInt32>(texHeight);
            RHI_Image_AddImageView(g_RenderContexts[i].textureHandle, &imageViewDesc);
            UploadImage(g_RenderContexts[i], imageSize, pixels, texWidth, texHeight);
        }
        
    }
    
    bool Initialize() override
    {
        if (!ArisenEngine::Debugger::Logger::GetInstance().Initialize())
        {
            throw std::exception(" Logger initialize failed.");
        }

        // Debugger::Logger::GetInstance().SetServerityLevel(Debugger::Logger::LogLevel::Log);
        
        LOG_INFO("Logger initialized..");

        CreateRenderWindow();

        InitRenderHardwareDriver();
        
        InitRenderContext();

        Platforms::InitDXC();

        InitShaderProgram();

        InitBuffer();

        CreateImage();
        
        return true;
    }

    void Run() override
    {
        for (int i = 0; i < k_WindowsCount; ++i)
        {
            UploadUniformBuffer(g_RenderContexts[i]);
            RecordSubmitPresent(std::move(g_RenderContexts[i]));
        
            if (g_RenderContexts[i].bShouldResize)
            {
                RHI_Device_SetResolution(g_RenderContexts[i].device, g_RenderContexts[i].newWidth, g_RenderContexts[i].newHeight);
                g_RenderContexts[i].bShouldResize = false;
            }
        }

        ++frameIndex;

        // 获取当前时间
        auto currentTime = Clock::now();
        std::chrono::duration<double> delta = currentTime - lastTime;
        lastTime = currentTime;

        // 计算帧耗时和帧率
        frameTime = delta.count();   // 单位：秒
        fps = (1.0 / frameTime) * 0.1 + fps * 0.9; // 单位：帧每秒
        std::cout << "FPS:" << fps << ", Delta Time:"<< frameTime << std::endl;
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

        RHI_Buffer_MemoryCopy(context.uniformBuffers[frameIndex % m_MaxFramesInFlight], &ubo, 0);
    }
    
    void UploadVertex(RenderContext const& context)
    {
        auto device = context.device;
        auto vertexBufferHandle = context.vertexBufferHandle;
        auto indicesBufferHandle = context.indicesBufferHandle;
        
        auto vertexStagingBufferHandle = RHI_Device_GetBufferHandle(device, "Vertex Staging Buffer");
        RHI::BufferDescriptor vsb{
            0,
               sizeof(vertices[0]) * vertices.size(),
               RHI::BUFFER_USAGE_TRANSFER_SRC_BIT,
               RHI::SHARING_MODE_EXCLUSIVE
        };
        RHI_Buffer_Alloc(vertexStagingBufferHandle, &vsb);
        RHI_Buffer_AllocDeviceMemory(vertexStagingBufferHandle, RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
        RHI_Buffer_MemoryCopy(vertexStagingBufferHandle, vertices.data(), 0);

        auto indicesStagingBufferHandle = RHI_Device_GetBufferHandle(device, "Indices Staging Buffer");
        RHI::BufferDescriptor isb{
            0,
               sizeof(indices[0]) * indices.size(),
               RHI::BUFFER_USAGE_TRANSFER_SRC_BIT,
               RHI::SHARING_MODE_EXCLUSIVE
        };
        RHI_Buffer_Alloc(indicesStagingBufferHandle, &isb);
        RHI_Buffer_AllocDeviceMemory(indicesStagingBufferHandle, RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
        RHI_Buffer_MemoryCopy(indicesStagingBufferHandle, indices.data(), 0);

        auto commandBuffer = RHI_Device_GetCommandBuffer(device, context.commandPoolId, frameIndex);
        RHI_Cmd_Begin(commandBuffer, frameIndex, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
        RHI_Cmd_CopyBuffer(commandBuffer, vertexStagingBufferHandle, 0, vertexBufferHandle, 0, RHI_Buffer_Size(vertexBufferHandle));

        RHI_Cmd_CopyBuffer(commandBuffer, indicesStagingBufferHandle, 0, indicesBufferHandle, 0, RHI_Buffer_Size(indicesBufferHandle));
        
        RHI_Cmd_End(commandBuffer);
        RHI_Device_Submit(device, commandBuffer, frameIndex);
        RHI_Device_GraphicQueueWaitIdle(device);
    }

    void UploadImage(RenderContext const& context, UInt64 textureSize, void* data, UInt32 texWidth, UInt32 texHeight)
    {
        auto device = context.device;
        auto textureStagingBufferHandle = RHI_Device_GetBufferHandle(device, "Texture Staging Buffer");
        RHI::BufferDescriptor tsb{
            0,
               textureSize,
               RHI::BUFFER_USAGE_TRANSFER_SRC_BIT,
               RHI::SHARING_MODE_EXCLUSIVE
        };

        RHI_Buffer_Alloc(textureStagingBufferHandle, &tsb);
        RHI_Buffer_AllocDeviceMemory(textureStagingBufferHandle, RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT |
            RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
        RHI_Buffer_MemoryCopy(textureStagingBufferHandle, data, 0);

       
        // Transfer Undefined to Transfer Dst
        auto commandBuffer = RHI_Device_GetCommandBuffer(device, context.commandPoolId, frameIndex);
        RHI_Cmd_Begin(commandBuffer, frameIndex, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
        {
            Containers::Vector<RHI::RHIImageMemoryBarrier> barriers {
                            {
                                RHI::ACCESS_NONE,
                                RHI::ACCESS_TRANSFER_WRITE_BIT,
                                RHI::IMAGE_LAYOUT_UNDEFINED,
                                RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                                VK_QUEUE_FAMILY_IGNORED,
                                VK_QUEUE_FAMILY_IGNORED,
                                reinterpret_cast<RHI::ImageHandle*>(context.textureHandle),
                                {
                                    RHI::IMAGE_ASPECT_COLOR_BIT,
                                    0, 1, 0, 1
                                }
                            }
        };
            RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_TRANSFER_BIT,
                0, &barriers);
        } // end of pipeline barrier

        // Copy Buffer To Image
        {
            ArisenEngine::Containers::Vector<RHI::BufferImageCopy> regions{
                { 0, 0, 0, { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 }, 0, 0, 0, texWidth, texHeight, 1 }
            };
            RHI_Cmd_CopyBufferToImage(commandBuffer, textureStagingBufferHandle, context.textureHandle,
                RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, &regions);
        } // end of copy buffer to image

        // Transfer Dst to Shader Read Only
        {
            Containers::Vector<RHI::RHIImageMemoryBarrier> barriers{
                    {
                        RHI::ACCESS_TRANSFER_WRITE_BIT,
                        RHI::ACCESS_SHADER_READ_BIT,
                        RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                        RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                        ~0U,
                        ~0U,
                        reinterpret_cast<RHI::ImageHandle*>(context.textureHandle),
                        {
                            RHI::IMAGE_ASPECT_COLOR_BIT,
                            0, 1, 0, 1
                        }
                    }
            };
            RHI_Cmd_PipelineBarrier_Image(commandBuffer, RHI::PIPELINE_STAGE_TRANSFER_BIT, RHI::PIPELINE_STAGE_FRAGMENT_SHADER_BIT,
                0, &barriers);
        } // end of pipeline barrier

        RHI_Cmd_End(commandBuffer);
        RHI_Device_Submit(device, commandBuffer, frameIndex);
        RHI_Device_ReleaseCommandBuffer(device, context.commandPoolId, frameIndex, commandBuffer);
        RHI_Device_GraphicQueueWaitIdle(device);
    }

    void AddDynamicState(RHI::GPUPipelineStateObject* pipelineState)
    {
        pipelineState->AddDynamicPipelineState(RHI::DYNAMIC_STATE_SCISSOR);
        pipelineState->AddDynamicPipelineState(RHI::DYNAMIC_STATE_VIEWPORT);
    }
    
    void RecordSubmitPresent(RenderContext&& context)
    {
        auto currentIndex = frameIndex % m_MaxFramesInFlight;
        
        auto commandBuffer = RHI_Device_GetCommandBuffer(context.device, context.commandPoolId, frameIndex);
        auto pipelineManager = RHI_Device_GetPipelineManager(context.device);
        
        auto pipelineState = RHI_PipelineManager_CreatePSO(pipelineManager);

        RHI_PSO_AddVertexBindingDescription(pipelineState, 0, sizeof(Vertex), RHI::VERTEX_INPUT_RATE_VERTEX);
        RHI_PSO_AddVertexInputAttributeDescription(pipelineState, 0, 0, RHI::EFormat::FORMAT_R32G32_SFLOAT, offsetof(Vertex, pos));
        RHI_PSO_AddVertexInputAttributeDescription(pipelineState, 1, 0, RHI::EFormat::FORMAT_R32G32B32_SFLOAT, offsetof(Vertex, color));

        RHI_PSO_ClearDescriptorSetLayoutBindings(pipelineState);
        Containers::Vector<std::shared_ptr<RHI::BufferHandle>> ubos;
        ubos.emplace_back(std::shared_ptr<RHI::BufferHandle>(reinterpret_cast<RHI::BufferHandle*>(context.uniformBuffers[currentIndex]), [](RHI::BufferHandle*){}));
        RHI_PSO_AddDescriptorSetLayoutBinding_Buffers(pipelineState, 0, 0, RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER, 1, RHI::SHADER_STAGE_VERTEX_BIT, &ubos);
        RHI_PSO_BuildDescriptorSetLayout(pipelineState);
        
        // Record cmd
        RHI_Cmd_WaitForFence(commandBuffer, frameIndex);

        RHI_DescriptorPool_Reset(context.descriptorPool, context.descriptorPoolIds[currentIndex]);
        RHI_DescriptorPool_AllocDescriptorSet(context.descriptorPool, context.descriptorPoolIds[currentIndex], 0, pipelineState);
        RHI_DescriptorPool_UpdateDescriptorSets(context.descriptorPool, context.descriptorPoolIds[currentIndex], pipelineState);
        
        RHI_Cmd_Begin(commandBuffer, frameIndex, 0);
        {
            auto renderPass = reinterpret_cast<RHI::GPURenderPass*>(context.renderPass);
            auto frameBuffer = reinterpret_cast<RHI::FrameBuffer*>(context.frameBuffer);
            auto surface = RHI_Instance_GetSurface(m_Instance, context.windowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            auto backBuffer = RHI_SwapChain_AquireCurrentImage(swapchain, frameIndex);
            auto backBufferView = RHI_Image_GetView(backBuffer);
            auto format = backBufferView->GetFormat();
            
            RHI_RenderPass_Free(context.renderPass, frameIndex);
            
            RHI_RenderPass_AddAttachmentAction(context.renderPass,
                format, RHI::SAMPLE_COUNT_1_BIT,
                RHI::ATTACHMENT_LOAD_OP_CLEAR, RHI::ATTACHMENT_STORE_OP_STORE,
                RHI::ATTACHMENT_LOAD_OP_DONT_CARE, RHI::ATTACHMENT_STORE_OP_DONT_CARE,
                RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);

            auto subpass = RHI_RenderPass_AddSubPass(context.renderPass);

            {
                // setup subpass
                RHI_Subpass_SetDependency(subpass,
                    RHI_Instance_GetExternalIndex(m_Instance),
                    RHI::EPipelineStageFlag::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
                    0,
                    RHI::EPipelineStageFlag::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
                    RHI::EAccessFlag::ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
                    0);
                RHI_Subpass_SetBindPoint(subpass, RHI::PIPELINE_BIND_POINT_GRAPHICS);
                RHI_Subpass_AddColorReference(subpass, 0, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);
                RHI_Subpass_SetDescriptionFlag(subpass, 0);
            }

            RHI_RenderPass_Alloc(context.renderPass, frameIndex);

            RHI_FrameBuffer_SetAttachment(context.frameBuffer, frameIndex, backBufferView, context.renderPass);

            {
                RHI::RenderPassBeginDesc desc
                {
                    renderPass,
                    frameBuffer,
                    RHI::SUBPASS_CONTENTS_INLINE
                };


                RHI_Cmd_BeginRenderPass(commandBuffer, frameIndex, &desc);

                {
                    for (auto programId : context.gpuPrograms)
                    {
                        RHI_PSO_AddProgram(pipelineState, programId);
                    }

                    {
                        // Pipeline State Object
                      
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

                        auto pipeline = RHI_PipelineManager_GetGraphicsPipeline(pipelineManager, pipelineState);

                        RHI_Pipeline_AllocGraphics(pipeline, frameIndex, subpass);
                        RHI_Cmd_BindPipeline(commandBuffer, frameIndex, pipeline);
                    }

                    {
                        // viewport scissor
                        RHI_Cmd_SetViewport(commandBuffer, 0, 0, static_cast<Float32>(backBufferView->GetWidth()), static_cast<
                                                       Float32>(backBufferView->GetHeight()), 0, 1);
                        RHI_Cmd_SetScissor(commandBuffer, 0, 0, backBufferView->GetWidth(), backBufferView->GetHeight());
                    }

                    {
                        // bind vertex buffers
                        RHI_Cmd_BindVertexBuffers(commandBuffer, context.vertexBufferHandle, 0);
                        RHI_Cmd_BindIndexBuffer(commandBuffer, context.indicesBufferHandle, 0, RHI::INDEX_TYPE_UINT16);
                    }

                    {
                        // bind descriptor sets
                        RHI_Cmd_BindDescriptorSets_FromPool(commandBuffer, frameIndex, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, context.descriptorPool, context.descriptorPoolIds[currentIndex]);
                    }
                    {
                        // draw call
                        // commandBuffer->Draw(3, 1, 0, 0, 0);
                        RHI_Cmd_DrawIndexed(commandBuffer, static_cast<unsigned int>(indices.size()), 1, 0, 0, 0, 0);
                    }
                    
                }
                RHI_Cmd_EndRenderPass(commandBuffer);
            }
        }

        RHI_Cmd_End(commandBuffer);

        {
            auto surface = RHI_Instance_GetSurface(m_Instance, context.windowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            RHI_Cmd_WaitSemaphore(commandBuffer, RHI_SwapChain_GetImageAvailableSemaphore(swapchain, frameIndex), RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT);
            RHI_Cmd_SignalSemaphore(commandBuffer, RHI_SwapChain_GetRenderFinishSemaphore(swapchain, frameIndex));
            RHI_Device_Submit(context.device, commandBuffer, frameIndex);
        }

        RHI_Device_ReleaseCommandBuffer(context.device, context.commandPoolId, frameIndex, commandBuffer);
        
        {
            // Present
            auto surface = RHI_Instance_GetSurface(m_Instance, context.windowId);
            auto swapchain = RHI_Surface_GetSwapChain(surface);
            RHI_SwapChain_Present(swapchain, frameIndex);
        }
    }

    void Shutdown() override
    {
        LOG_INFO(" Shut down ...");

        for (auto renderContext : g_RenderContexts)
        {
            RHI_Device_WaitIdle(renderContext.device);
            for (auto ub : renderContext.uniformBuffers)
            {
                RHI_Buffer_Free(ub);
                RHI_Device_ReleaseBufferHandle(renderContext.device, ub);
            }
            if (renderContext.vertexBufferHandle)
            {
                RHI_Buffer_Free(renderContext.vertexBufferHandle);
                RHI_Device_ReleaseBufferHandle(renderContext.device, renderContext.vertexBufferHandle);
            }
            if (renderContext.indicesBufferHandle)
            {
                RHI_Buffer_Free(renderContext.indicesBufferHandle);
                RHI_Device_ReleaseBufferHandle(renderContext.device, renderContext.indicesBufferHandle);
            }
            if (renderContext.textureHandle)
            {
                RHI_Image_Free(renderContext.textureHandle);
                RHI_Device_ReleaseImageHandle(renderContext.device, renderContext.textureHandle);
            }
            if (renderContext.frameBuffer) RHI_Device_ReleaseFrameBuffer(renderContext.device, renderContext.frameBuffer);
            if (renderContext.renderPass) RHI_Device_ReleaseRenderPass(renderContext.device, renderContext.renderPass);
        }
        
        g_RenderContexts.clear();
        
        // RHI dispose
        if (m_Instance) RHI_DestroyInstance(m_Instance);
        
        Platforms::ReleaseDXC();

        // rhi loader dispose 
        Graphics::RHILoader::Dispose();

        // NOTE: logger must be dispose at the last
        Debugger::Logger::Shutdown();
    }
};

#endif

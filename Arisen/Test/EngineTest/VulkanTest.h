#pragma once
#include <filesystem>
#include "Windows/PlatformTypes.h"
#include "Test.h"
#include "RHI\RHILoader.h"
#include "RHI/Instance.h"
#include "RHI/Enums/Pipeline/EAccessFlag.h"
#include "RHI/Enums/Memory/EBufferUsage.h"
#include "RHI/Enums/Pipeline/EColorComponentFlag.h"
#include "RHI/Surfaces/Surface.h"
#include "RHI/Handles/ImageHandle.h"
#include "RHI/Memory/ImageView.h"
#include "RHI/Program/GPUPipelineManager.h"
#include "RHI/Program/GPUSubPass.h"
#include "RHI/Program/GPUPipelineStateObject.h"
#include "Windows/RenderWindowAPI.h"
#include "ShaderCompiler/ShaderCompilerAPI.h"
#define GLM_FORCE_RADIANS
#include <glm/glm.hpp>
#include <glm/gtc/matrix_transform.hpp>

#include <chrono>
#include "RHI/Handles/BufferHandle.h"

using namespace ArisenEngine;

#ifdef TEST_WINDOWS

struct RenderContext
{
    UInt32 windowId;
    UInt32 newWidth;
    UInt32 newHeight;
    RHI::Device* device;
    std::shared_ptr<RHI::GPURenderPass> renderPass;
    std::shared_ptr<RHI::FrameBuffer> frameBuffer;
    std::shared_ptr<RHI::BufferHandle> vertexBufferHandle;
    std::shared_ptr<RHI::BufferHandle> indicesBufferHandle;
    Containers::Vector<std::shared_ptr<RHI::BufferHandle>> uniformBuffers;
     
    RHI::RHICommandBufferPool* commandPool;
    Containers::Vector<UInt32> gpuPrograms;
    Containers::Vector<UInt32> descriptorPoolIds;
    bool bShouldResize;
};

Containers::Vector<RenderContext> g_RenderContexts;

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

class EngineTest : public Test
{
private:
    UInt32 frameIndex {0};
    RHI::Instance* m_Instance{};

public:
    EngineTest(): m_Instance(nullptr)
    {
        // std::set_terminate([](){
        //     Debugger::Logger::Shutdown();
        // });
    }

    bool Initialize() override
    {
        if (!ArisenEngine::Debugger::Logger::GetInstance().Initialize())
        {
            throw std::exception(" Logger initialize failed.");
        }

        // Debugger::Logger::GetInstance().SetServerityLevel(Debugger::Logger::LogLevel::Log);
        
        LOG_INFO("Logger initialized..");

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

        // auto window2 = Platforms::CreateRenderWindow(nullptr, WinProc, 400, 680);
        // auto window3 = Platforms::CreateRenderWindow(nullptr, WinProc, 600, 380);

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

        Graphics::RHILoader::SetCurrentGraphicsAPI(RHI::GraphsicsAPI::Vulkan);
        m_Instance = Graphics::RHILoader::CreateInstance(std::move(app_info));
        auto env = m_Instance->GetEnvString();
        // LOG_INFO(std::move(env));

        // init surfaces
        for (auto& renderContext : g_RenderContexts)
        {
            m_Instance->CreateSurface(std::move(renderContext.windowId));
        }

        // pick physical device
        m_Instance->PickPhysicalDevice();

        // init logical devices
        m_Instance->InitLogicDevices();

        for (int i = 0; i < k_WindowsCount; ++i)
        {
            g_RenderContexts[i].device = m_Instance->GetLogicalDevice(g_RenderContexts[i].windowId);
            auto poolId = g_RenderContexts[i].device->CreateCommandBufferPool();
            g_RenderContexts[i].commandPool = g_RenderContexts[i].device->GetCommandBufferPool(poolId);
            g_RenderContexts[i].renderPass = g_RenderContexts[i].device->GetRenderPass();
            g_RenderContexts[i].frameBuffer = g_RenderContexts[i].device->GetFrameBuffer();
            g_RenderContexts[i].vertexBufferHandle = g_RenderContexts[i].device->GetBufferHandle("Vertex Buffer");
            g_RenderContexts[i].indicesBufferHandle = g_RenderContexts[i].device->GetBufferHandle("Indices Buffer");
           
            for(int frameIndex = 0; frameIndex < m_Instance->GetMaxFramesInFlight(); ++frameIndex)
            {
                g_RenderContexts[i].descriptorPoolIds.emplace_back(
                    g_RenderContexts[i].device->GetDescriptorPool()
                    ->AddPool({RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER},
                        {1},1));
                g_RenderContexts[i].uniformBuffers.emplace_back(
                    g_RenderContexts[i].device->GetBufferHandle(
                        "Uniform Buffer " + std::to_string(frameIndex)));
            }
        }

        Platforms::InitDXC();

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
            m_Instance->GetEnvString(),
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
            auto programId = g_RenderContexts[i].device->CreateGPUProgram();
            auto desc = RHI::GPUProgramDesc
            {
                outputVertex.codeSize,
                outputVertex.codePointer,
                "Vert",
                String::WStringToString(path).c_str(),
                RHI::SHADER_STAGE_VERTEX_BIT
            };
            g_RenderContexts[i].device->AttachProgramByteCode(programId, std::move(desc));
            g_RenderContexts[i].gpuPrograms.emplace_back(programId);
        }


        Platforms::ShaderCompileParams fragmentParams
        {
            path,
            L"Frag",
            L"6_0",
            L"-spirv",
            m_Instance->GetEnvString(),
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
            auto programId = g_RenderContexts[i].device->CreateGPUProgram();
            auto desc = RHI::GPUProgramDesc
            {
                outputfragment.codeSize,
                outputfragment.codePointer,
                "Frag",
                String::WStringToString(path).c_str(),
                RHI::SHADER_STAGE_FRAGMENT_BIT
            };
            g_RenderContexts[i].device->AttachProgramByteCode(programId, std::move(desc));
            g_RenderContexts[i].gpuPrograms.emplace_back(programId);
        }

        // Init Buffer
        for (int i = 0; i < k_WindowsCount; ++i)
        {
            g_RenderContexts[i].vertexBufferHandle->AllocBufferHandle({
                0,
                sizeof(vertices[0]) * vertices.size(),
                RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT,
                RHI::SHARING_MODE_EXCLUSIVE
            });
            g_RenderContexts[i].vertexBufferHandle->AllocBufferMemory(RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            g_RenderContexts[i].indicesBufferHandle->AllocBufferHandle({
                0,
                sizeof(indices[0]) * indices.size(),
                RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_INDEX_BUFFER_BIT,
                RHI::SHARING_MODE_EXCLUSIVE
            });
            g_RenderContexts[i].indicesBufferHandle->AllocBufferMemory(RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            for (const auto& uniformBuffer : g_RenderContexts[i].uniformBuffers)
            {
                uniformBuffer->AllocBufferHandle({
                    0,
                    sizeof(UniformBufferObject),
                    RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT,
                    RHI::SHARING_MODE_EXCLUSIVE
                });
                uniformBuffer->AllocBufferMemory(RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
                uniformBuffer ->SetBufferOffsetRange(0, sizeof(UniformBufferObject));
            }
            
            UploadVertex(g_RenderContexts[i]);
        }

       
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
                g_RenderContexts[i].device->SetResolution(g_RenderContexts[i].newWidth, g_RenderContexts[i].newHeight);
                g_RenderContexts[i].bShouldResize = false;
            }
        }

        ++frameIndex;
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

        context.uniformBuffers[frameIndex % m_Instance->GetMaxFramesInFlight()]->MemoryCopy(&ubo, 0);
    }
    
    void UploadVertex(RenderContext const& context)
    {
        auto device = context.device;
        auto vertexBufferHandle = context.vertexBufferHandle;
        auto indicesBufferHandle = context.indicesBufferHandle;
        
        auto vertexStagingBufferHandle = device->GetBufferHandle("Vertex Staging Buffer");
        vertexStagingBufferHandle->AllocBufferHandle({
            0,
               sizeof(vertices[0]) * vertices.size(),
               RHI::BUFFER_USAGE_TRANSFER_SRC_BIT,
               RHI::SHARING_MODE_EXCLUSIVE
        });
        vertexStagingBufferHandle->AllocBufferMemory(RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
        vertexStagingBufferHandle->MemoryCopy(vertices.data(), 0);

        auto indicesStagingBufferHandle = device->GetBufferHandle("Indices Staging Buffer");
        indicesStagingBufferHandle->AllocBufferHandle({
            0,
               sizeof(indices[0]) * indices.size(),
               RHI::BUFFER_USAGE_TRANSFER_SRC_BIT,
               RHI::SHARING_MODE_EXCLUSIVE
        });
        indicesStagingBufferHandle->AllocBufferMemory(RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
        indicesStagingBufferHandle->MemoryCopy(indices.data(), 0);

        auto commandBuffer = context.commandPool->GetCommandBuffer(frameIndex);
        commandBuffer->Begin(frameIndex, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
        commandBuffer->CopyBuffer(vertexStagingBufferHandle.get(), 0,
            vertexBufferHandle.get(), 0, vertexBufferHandle->BufferSize());

        commandBuffer->CopyBuffer(indicesStagingBufferHandle.get(), 0,
           indicesBufferHandle.get(), 0, indicesBufferHandle->BufferSize());
        
        commandBuffer->End();
        device->Submit(commandBuffer.get(), frameIndex);
        device->GraphicQueueWaitIdle();
    }

    void AddDynamicState(RHI::GPUPipelineStateObject* pipelineState)
    {
        pipelineState->AddDynamicPipelineState(RHI::DYNAMIC_STATE_SCISSOR);
        pipelineState->AddDynamicPipelineState(RHI::DYNAMIC_STATE_VIEWPORT);
    }
    
    void RecordSubmitPresent(RenderContext&& context)
    {
        auto currentIndex = frameIndex % m_Instance->GetMaxFramesInFlight();
        
        auto commandBuffer = context.commandPool->GetCommandBuffer(frameIndex);

        auto pipelineManager = context.device->GetGPUPipelineManager();
        
        auto pipelineState = pipelineManager->GetPipelineState();

        pipelineState->AddVertexBindingDescription(0, sizeof(Vertex), RHI::VERTEX_INPUT_RATE_VERTEX);
        pipelineState->AddVertexInputAttributeDescription(0, 0, RHI::Format::FORMAT_R32G32_SFLOAT, offsetof(Vertex, pos));
        pipelineState->AddVertexInputAttributeDescription(1, 0, RHI::Format::FORMAT_R32G32B32_SFLOAT, offsetof(Vertex, color));

        pipelineState->ClearDescriptorSetLayoutBindings();
        pipelineState->AddDescriptorSetLayoutBinding(0, 0, RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER,
            1, RHI::SHADER_STAGE_VERTEX_BIT,
            Containers::Vector<std::shared_ptr<RHI::BufferHandle>>{
                context.uniformBuffers[currentIndex]
            });
        pipelineState->BuildDescriptorSetLayout();
        
        // Record cmd
        commandBuffer->WaitForFence(frameIndex);

        auto descriptorPool = context.device->GetDescriptorPool();
        descriptorPool->ResetPool(context.descriptorPoolIds[currentIndex]);
        descriptorPool->AllocDescriptorSet(context.descriptorPoolIds[currentIndex], 0, pipelineState.get());
        descriptorPool->UpdateDescriptorSets(context.descriptorPoolIds[currentIndex], pipelineState.get());
        
        commandBuffer->Begin(frameIndex);
        {
            auto renderPass = context.renderPass.get();
            auto frameBuffer = context.frameBuffer.get();
            auto backBuffer = context.device->GetSurface()->GetSwapChain()->AquireCurrentImage(frameIndex);
            auto backBufferView = static_cast<RHI::ImageView*>(backBuffer->GetMemoryView());
            auto format = backBufferView->GetFormat();
            
            renderPass->FreeRenderPass(frameIndex);
            
            renderPass->AddAttachmentAction(
                format, RHI::SAMPLE_COUNT_1_BIT,
                RHI::ATTACHMENT_LOAD_OP_CLEAR, RHI::ATTACHMENT_STORE_OP_STORE,
                RHI::ATTACHMENT_LOAD_OP_DONT_CARE, RHI::ATTACHMENT_STORE_OP_DONT_CARE,
                RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR
            );

            auto subpass = renderPass->AddSubPass();

            {
                // setup subpass
                subpass->SetDependency(
                    m_Instance->GetExternalIndex(),
                    RHI::EPipelineStageFlag::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
                    0,
                    RHI::EPipelineStageFlag::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
                    RHI::EAccessFlagBits::ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
                    0
                );
                subpass->SetBindPoint(RHI::PIPELINE_BIND_POINT_GRAPHICS);
                subpass->AddColorReference(0, RHI::IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);
                subpass->SetSubPassDescriptionFlag(0);
            }

            renderPass->AllocRenderPass(frameIndex);

            frameBuffer->SetAttachment(frameIndex, backBufferView, renderPass);

            {
                RHI::RenderPassBeginDesc desc
                {
                    renderPass,
                    frameBuffer,
                    RHI::SUBPASS_CONTENTS_INLINE
                };


                commandBuffer->BeginRenderPass(frameIndex, std::move(desc));

                {
                    for (auto programId : context.gpuPrograms)
                    {
                        pipelineState->AddProgram(programId);
                    }

                    {
                        // Pipeline State Object
                      
                        AddDynamicState(pipelineState.get());
                        pipelineState->SetPrimitiveState(RHI::PRIMITIVE_TOPOLOGY_TRIANGLE_LIST, false);
                        pipelineState->SetDepthClampEnable(false);
                        pipelineState->SetRasterizerDiscardEnable(false);
                        pipelineState->SetPolygonMode(RHI::EPOLYGON_MODE_FILL);
                        pipelineState->SetLineWidth(1.0F);
                        pipelineState->SetCullMode(RHI::CULL_MODE_NONE);
                        pipelineState->SetFrontFace(RHI::FRONT_FACE_CLOCKWISE);
                        pipelineState->SetDepthBiasEnable(false);
                        pipelineState->SetSampleShading(false);
                        pipelineState->SetSampleCount(RHI::SAMPLE_COUNT_1_BIT);
                        pipelineState->AddBlendAttachmentState(false,
                                                               RHI::EColorComponentFlagBits::COLOR_COMPONENT_R_BIT |
                                                               RHI::EColorComponentFlagBits::COLOR_COMPONENT_G_BIT |
                                                               RHI::EColorComponentFlagBits::COLOR_COMPONENT_B_BIT |
                                                               RHI::EColorComponentFlagBits::COLOR_COMPONENT_A_BIT);
                        pipelineState->SetLogicOp(false, RHI::LOGIC_OP_COPY);
                        pipelineState->SetBlendConstants(0.0f, 0.0f, 0.0f, 0.0f);

                        auto pipeline = pipelineManager->GetGraphicsPipeline(pipelineState.get());

                        pipeline->AllocGraphicPipeline(frameIndex, subpass);
                        commandBuffer->BindPipeline(frameIndex, pipeline);
                    }

                    {
                        // viewport scissor
                        commandBuffer->SetViewport(0, 0, static_cast<Float32>(backBufferView->GetWidth()), static_cast<
                                                       Float32>(backBufferView->GetHeight()), 0, 1);
                        commandBuffer->SetScissor(0, 0, backBufferView->GetWidth(), backBufferView->GetHeight());
                    }

                    {
                        // bind vertex buffers
                        commandBuffer->BindVertexBuffers(context.vertexBufferHandle.get(), 0);
                        commandBuffer->BindIndexBuffer(context.indicesBufferHandle.get(), 0, RHI::INDEX_TYPE_UINT16);
                    }

                    {
                        // bind descriptor sets
                        auto descriptorSets = descriptorPool->GetDescriptorSets(context.descriptorPoolIds[currentIndex]);
                        commandBuffer->BindDescriptorSets(frameIndex, RHI::PIPELINE_BIND_POINT_GRAPHICS, 0, descriptorSets, 0, 0);
                    }
                    {
                        // draw call
                        // commandBuffer->Draw(3, 1, 0, 0, 0);
                        commandBuffer->DrawIndexed(indices.size(), 1, 0, 0, 0, 0);
                    }
                    
                }
                commandBuffer->EndRenderPass();
            }
        }

        commandBuffer->End();

        {
            auto swapchain = context.device->GetSurface()->GetSwapChain();
            commandBuffer->WaitSemaphore(
                swapchain->GetImageAvailableSemaphore(frameIndex),
                RHI::PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT
                );
            commandBuffer->SignalSemaphore(swapchain->GetRenderFinishSemaphore(frameIndex));
            commandBuffer->InjectFence(commandBuffer->GetOwner()->GetFence(frameIndex));
            // Submit
            context.device->Submit(commandBuffer.get(), frameIndex);
        }

        context.commandPool->ReleaseCommandBuffer(frameIndex, commandBuffer);
        
        {
            // Present
            context.device->GetSurface()->GetSwapChain()->Present(frameIndex);
        }
    }

    void Shutdown() override
    {
        LOG_INFO(" Shut down ...");

        for (auto renderContext : g_RenderContexts)
        {
            renderContext.device->DeviceWaitIdle();
            renderContext.uniformBuffers.clear();
        }
        
        g_RenderContexts.clear();
        
        // RHI dispose
        delete m_Instance;
        
        Platforms::ReleaseDXC();

        // rhi loader dispose 
        Graphics::RHILoader::Dispose();

        // NOTE: logger must be dispose at the last
        Debugger::Logger::Shutdown();
    }
};

#endif

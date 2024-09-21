#pragma once
#include <filesystem>
#include "Windows/PlatformTypes.h"
#include "Test.h"
#include "RHI\RHILoader.h"
#include "RHI/Instance.h"
#include "RHI/Enums/Pipeline/EAccessFlag.h"
#include "RHI/Enums/Pipeline/EColorComponentFlag.h"
#include "RHI/Surfaces/Surface.h"
#include "RHI/Handles/ImageHandle.h"
#include "RHI/Memory/ImageView.h"
#include "RHI/Program/GPUPipelineManager.h"
#include "RHI/Program/GPUSubPass.h"
#include "RHI/Program/GPUPipelineStateObject.h"
#include "Windows/RenderWindowAPI.h"
#include "ShaderCompiler/ShaderCompilerAPI.h"
#include <glm/glm.hpp>

#include "RHI/Handles/BufferHandle.h"

using namespace NebulaEngine;

#ifdef TEST_WINDOWS

struct RenderContext
{
    u32 windowId;
    RHI::Device* device;
    std::shared_ptr<RHI::GPURenderPass> renderPass;
    std::shared_ptr<RHI::FrameBuffer> frameBuffer;
    std::shared_ptr<RHI::BufferHandle> bufferHandle;
    RHI::RHICommandBufferPool* commandPool;
    Containers::Vector<u32> gpuPrograms;
    bool bShouldResize;
    u32 newWidth;
    u32 newHeight;
};

Containers::Vector<RenderContext> g_RenderContexts;

const int k_WindowsCount = 1;

void WinResize(HWND hwnd, u32 width, u32 height)
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

const std::vector<Vertex> vertices =
{
    {{0.0f, -0.5f}, {1.0f, 0.0f, 0.0f}},
    {{0.5f, 0.5f}, {0.0f, 1.0f, 0.0f}},
    {{-0.5f, 0.5f}, {0.0f, 0.0f, 1.0f}}
};


class EngineTest : public Test
{
private:
    u32 frameIndex {0};
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
        if (!NebulaEngine::Debugger::Logger::GetInstance().Initialize())
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
                windowId
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
            g_RenderContexts[i].bufferHandle = g_RenderContexts[i].device->GetBufferHandle();
        }

        Platforms::InitDXC();

        auto shaderFileName = L"SimpleUnlit";
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

        // Upload Vertex Buffer
        for (int i = 0; i < k_WindowsCount; ++i)
        {
            RHI::BufferAllocDesc desc
            {
                0,
                sizeof(vertices[0]) * vertices.size(),
                RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT,
                RHI::SHARING_MODE_EXCLUSIVE
            };
            g_RenderContexts[i].bufferHandle->AllocBufferHandle(std::move(desc));
            g_RenderContexts[i].bufferHandle->AllocBufferMemory(RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT);
        }

        // init Pipeline Assets

        
        
        return true;
    }

    void Run() override
    {
        for (int i = 0; i < k_WindowsCount; ++i)
        {
            RecordSubmitPresent(std::move(g_RenderContexts[i]));

            if (g_RenderContexts[i].bShouldResize)
            {
                g_RenderContexts[i].device->SetResolution(g_RenderContexts[i].newWidth, g_RenderContexts[i].newHeight);
                g_RenderContexts[i].bShouldResize = false;
            }
        }

        ++frameIndex;
    }

    void UploadVertex(RHI::GPUPipelineStateObject* pipelineState, RHI::BufferHandle* bufferHandle)
    {
        pipelineState->AddVertexBindingDescription(0, sizeof(Vertex), RHI::VERTEX_INPUT_RATE_VERTEX);
        pipelineState->AddVertexInputAttributeDescription(0, 0, RHI::Format::FORMAT_R32G32_SFLOAT, offsetof(Vertex, pos));
        pipelineState->AddVertexInputAttributeDescription(1, 0, RHI::Format::FORMAT_R32G32B32_SFLOAT, offsetof(Vertex, color));
        bufferHandle->MemoryCopy(vertices.data(), 0);
    }

    void AddDynamicState(RHI::GPUPipelineStateObject* pipelineState)
    {
        pipelineState->AddDynamicPipelineState(RHI::DYNAMIC_STATE_SCISSOR);
        pipelineState->AddDynamicPipelineState(RHI::DYNAMIC_STATE_VIEWPORT);
    }
    
    void RecordSubmitPresent(RenderContext&& context)
    {
        auto commandBuffer = context.commandPool->GetCommandBuffer(frameIndex);

        // Record cmd
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
                    auto pipelineManager = context.device->GetGPUPipelineManager();

                    auto pipelineState = pipelineManager->GetPipelineState();
                    for (auto programId : context.gpuPrograms)
                    {
                        pipelineState->AddProgram(programId);
                    }

                    {
                        // Pipeline State Object
                        UploadVertex(pipelineState.get(), context.bufferHandle.get());
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
                        commandBuffer->SetViewport(0, 0, static_cast<f32>(backBufferView->GetWidth()), static_cast<
                                                       f32>(backBufferView->GetHeight()), 0, 1);
                        commandBuffer->SetScissor(0, 0, backBufferView->GetWidth(), backBufferView->GetHeight());
                    }

                    {
                        // bind vertex buffers
                        commandBuffer->BindVertexBuffers(0, { context.bufferHandle.get() }, {0});
                    }
                    
                    {
                        // draw call
                        commandBuffer->Draw(3, 1, 0, 0);
                    }
                }
                commandBuffer->EndRenderPass();
            }
        }

        commandBuffer->End();

        {
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

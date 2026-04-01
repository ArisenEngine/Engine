#include "GameInstance.h"

#include <ranges>

#include "RHIFactoryD3D12.h"
#include "SharedPtrOutWrapper.h"
#include "Containers/Containers.h"
#include "Logger/Logger.h"
#include "CommonFlags.hpp"
#include "DataUtils.h"
#include "DescriptorHeap.h"
#include "IRenderContext.h"
#include "ResourceProvider.h"
#include "ViewStateD3D12.h"

GameInstance::~GameInstance()
{
}

void GameInstance::Initialize(HWND hwnd)
{
    ArisenEngine::Debugger::Logger::GetInstance().Initialize();
    LOG_DEBUG("GameInstance Init!");

    try {
        LOG_DEBUG("Starting InitArisenRHI...");
        InitArisenRHI(hwnd);
        LOG_DEBUG("InitArisenRHI completed successfully");

        LOG_DEBUG("Starting LoadAssets...");
        LoadAssets();
        LOG_DEBUG("LoadAssets completed successfully");

        LOG_DEBUG("GameInstance::Initialize completed successfully");
    } catch (const std::exception& e) {
        LOG_FATAL(std::string("Exception in GameInstance::Initialize: ") + e.what());
        throw;
    } catch (...) {
        LOG_FATAL("Unknown exception in GameInstance::Initialize");
        throw;
    }
}

void GameInstance::Loop()
{
    LOG_DEBUG("GameInstance::Loop called - currently empty, this needs implementation");
}

void GameInstance::OnKeyDown(char KeyCode)
{
}

void GameInstance::OnKeyUp(char KeyCode)
{
}

void GameInstance::InitArisenRHI(HWND hwnd)
{
    switch (DeviceType)
    {
    case RHI_DEVICE_TYPE::RHI_DEVICE_TYPE_D3D12:
        {
            using namespace ArisenRHID3D12;
            EngineCreateInfoD3D12 EngineCreateInfo;
            EngineCreateInfo.EnableValidation = true;
            EngineCreateInfo.ValidationFlags |= D3D12_VALIDATION_FLAGS::D3D12_VALIDATION_FLAG_GPU_BASED_VALIDATION;

            RHIFactoryD3D12* FactoryD3D12 = CreateRHIFactoryD3D12();
            pDevice = FactoryD3D12->CreateDeviceD3D12(EngineCreateInfo);
            RenderContextSettings context_ettings;
            Environment env;
            env.window_handle = hwnd;
            m_render_context_ptr = pDevice->CreateRenderContext(context_ettings, env);
            m_render_context_ptr->SetName("Graphics Context");

            int32_t attachment_index = 0;
            // create render pass pattern settings.
            m_screen_pass_pattern_settings.shader_access_mask = {RenderPassAccess::ShaderResources, RenderPassAccess::Samplers};
            m_screen_pass_pattern_settings.is_final_pass = true;

            const Vector4F default_clear_color(0.0F, 0.2F, 0.4F, 1.0F);
            m_screen_pass_pattern_settings.color_attachments = {
                RenderPassColorAttachment(attachment_index,
                TextureFormat::BGRA8Unorm,
                RenderPassAttachment::LoadAction::Clear,
                RenderPassAttachment::StoreAction::Store,
                default_clear_color
                )
            };

            // create render pattern.
            m_screen_pass_pattern_ptr = m_render_context_ptr->CreateRenderPattern(m_screen_pass_pattern_settings);
            m_screen_pass_pattern_ptr->SetName("Final Screen Pass Pattern");

            // Set view state.
            m_view_state_ptr = m_render_context_ptr->CreateViewState({
                {GetRect(context_ettings.frame_size)},
                {GetRect(context_ettings.frame_size)}
            });

            // Create Frame Resources.
            for (uint32_t frame_index = 0; frame_index < context_ettings.frame_buffers_Count; frame_index++ )
            {
                AppFrame& frame = m_frames.emplace_back(frame_index);
                frame.screen_texture = m_render_context_ptr->CreateTexture(ConvertToTextureSettings(context_ettings, frame_index));
                frame.screen_texture->SetName(std::format("frame buffer {}", frame_index));

                std::vector<TextureViewBase> attachments{frame.screen_texture->GetTextureView()};
                //depth

                frame.screen_pass = m_render_context_ptr->CreateRenderPass(*m_screen_pass_pattern_ptr,
                    {
                        attachments, context_ettings.frame_size
                    });
            }

            // Create shaders.
            std::map<ShaderType, ShaderSettings> shader_set
            {
                {ShaderType::Vertex, {ResourceProvider::Get(), "HelloTriangle","VS"}},
                {ShaderType::Pixel, {ResourceProvider::Get(), "HelloTriangle","PS"}},
            };

            Ptrs<IShader> shaders_created;
            std::ranges::transform(shader_set, std::back_inserter(shaders_created), [this](const auto& pair){
                return m_render_context_ptr->CreateShader(pair.first, pair.second);
            });

            // create PSO
            m_pso_ptr = m_render_context_ptr->CreateRenderPipelineStateObject({
                .program = m_render_context_ptr->CreateProgram({
                    .shaders = shaders_created,
                    .attachment_formats = m_screen_pass_pattern_ptr->GetAttachmentFormats()
                }),
                .render_pattern = m_screen_pass_pattern_ptr.get()
            });

            const ICommandQueue& cmd_queue = m_render_context_ptr->GetDefaultCommandKit(CommandListType::Render).GetQueue();
            for (Frame& frame : m_frames)
            {
                frame.render_command_list = cmd_queue.CreateRenderCommandList(*frame.screen_pass);
                frame.render_command_list->SetName(std::format("Render Triangle{}", frame.index));
                frame.command_list_set = cmd_queue.CreateCommandListSet({frame.render_command_list});
            }

            // complete.
            m_render_context_ptr->com
        }
        break;
    case RHI_DEVICE_TYPE::RHI_DEVICE_TYPE_VULKAN:
        break;

    default:
        LOG_ERROR("ArisenRHI init failed.");
        break;
    }
}

void GameInstance::LoadAssets()
{
    LOG_DEBUG("LoadAssets called - currently empty, this is expected");
}

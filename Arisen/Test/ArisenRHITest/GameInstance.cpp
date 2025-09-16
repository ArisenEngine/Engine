#include "GameInstance.h"

#include "RHIFactoryD3D12.h"
#include "SharedPtrOutWrapper.h"
#include "Containers/Containers.h"
#include "Logger/Logger.h"
#include "CommonFlags.hpp"
#include "DataUtils.h"
#include "DescriptorHeap.h"
#include "IRenderContext.h"
#include "ViewStateD3D12.h"

GameInstance::~GameInstance()
{
}

void GameInstance::Initialize(HWND hwnd)
{
    ArisenEngine::Debugger::Logger::GetInstance().Initialize();
    LOG_DEBUG("GameInstance Init!");

    InitArisenRHI(hwnd);
    LoadAssets();
}

void GameInstance::Loop()
{
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
            RenderContextSettings Settings;
            Environment env;
            env.window_handle = hwnd;
            pRender_context = pDevice->CreateRenderContext(Settings, env);
            pRender_context->SetName("Graphics Context");

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
            m_screen_pass_pattern_ptr = pRender_context->CreateRenderPattern(m_screen_pass_pattern_settings);
            m_screen_pass_pattern_ptr->SetName("Final Screen Pass Pattern");

            // Set view state.
            m_view_state_ptr = pRender_context->CreateViewState({
                {GetRect(Settings.frame_size)},
                {GetRect(Settings.frame_size)}
            });

            // Create Frame Resources.
            for (uint32_t frame_index = 0; frame_index < Settings.frame_buffers_Count; frame_index++ )
            {
                AppFrame& frame = m_frames.emplace_back(frame_index);
                /Create screenTexture.
            }
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
}

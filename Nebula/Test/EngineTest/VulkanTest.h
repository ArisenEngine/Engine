#pragma once
#include "Windows/PlatformTypes.h"
#include "Test.h"
#include "Graphics\RHILoader.h"
#include "RHI/Instance.h"
#include "Windows/RenderWindowAPI.h"
#include "ShaderCompiler/ShaderCompilerAPI.h"

using namespace NebulaEngine;

#ifdef TEST_WINDOWS

u32 windowId;

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
    }
    return DefWindowProc(hwnd, msg, wparam, lparam);
}


class EngineTest : public Test
{
private:

    RHI::Instance* m_Instance {};
    
public:

    EngineTest(): m_Instance(nullptr)
    {
    }

    bool Initialize() override
    {
        if (!NebulaEngine::Debugger::Logger::GetInstance().Initialize())
        {
            throw std::exception(" Logger initialize failed.");
        }

        LOG_INFO("Logger initialized..");
        
        windowId = Platforms::CreateRenderWindow(nullptr, WinProc, 640, 480);

        auto window2 = Platforms::CreateRenderWindow(nullptr, WinProc, 400, 680);
        auto window3 = Platforms::CreateRenderWindow(nullptr, WinProc, 600, 380);

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
            1, 0, 0
        };
        
        Graphics::RHILoader::SetCurrentGraphicsAPI(RHI::GraphsicsAPI::Vulkan);
        m_Instance = Graphics::RHILoader::CreateInstance(std::move(app_info));
        auto env = m_Instance->GetEnvString();
        LOG_INFO(std::move(env));

        // init surface
        m_Instance->CreateSurface(std::move(windowId));
        m_Instance->CreateSurface(std::move(window2));
        m_Instance->CreateSurface(std::move(window3));

        // pick physical device
        m_Instance->PickPhysicalDevice();
        
        // init logical devices
        m_Instance->InitLogicDevices();
        
        LOG_INFO(" Is support linear space:" + std::to_string(m_Instance->IsSupportLinearColorSpace(std::move(windowId))));

        Platforms::InitDXC();
        
        return true;
    }

    void Run() override
    {
        
    }

    void Shutdown() override
    {
        LOG_INFO(" Shut down ...");

        // rhi dispose
        delete m_Instance;

        Platforms::ReleaseDXC();
        
        // rhi loader dispose 
        Graphics::RHILoader::Dispose();

        // NOTE: logger must be dispose at the last
        Debugger::Logger::Dispose();
    }
};

#endif

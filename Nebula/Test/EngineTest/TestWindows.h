#pragma once
#include "Platforms/PlatformTypes.h"
#include "Test.h"
#include "Graphics\RHILoader.h"
#include "RHI/Instance.h"
#include "Renderding/RenderSurface.h"

using namespace NebulaEngine;

#ifdef TEST_WINDOWS

u32 render_surface_id;

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

    RHI::Instance* m_Instance;
    
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
        
        render_surface_id = Rendering::CreateRenderSurface(nullptr, WinProc, 1920, 1080);

        RHI::AppInfo app_info
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
        
        return true;
    }

    void Run() override
    {
       
    }

    void Shutdown() override
    {
        LOG_INFO(" Shut down ...");
        Graphics::RHILoader::Dispose();

        // NOTE: logger must be dispose at the last
        Debugger::Logger::Dispose();
    }
};

#endif

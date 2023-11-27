#pragma once
#include "Platforms/PlatformTypes.h"
#include "Platforms/Platform.h"
#include "Test.h"
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

    RHI::Instance* m_instance;
    
public:

    EngineTest(): m_instance(nullptr)
    {
    }

    bool Initialize() override
    {
        if (!NebulaEngine::Debugger::Logger::Initialize())
        {
            throw std::exception(" Logger initialize failed.");
        }

        render_surface_id = Rendering::CreateRenderSurface(nullptr, WinProc, 1920, 1080);

        
        return true;
    }

    void Run() override
    {
       
    }

    void Shutdown() override
    {
        Debugger::Logger::Exit();
    }
};

#endif

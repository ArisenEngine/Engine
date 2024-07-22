#pragma once
#include <filesystem>
#include "Windows/PlatformTypes.h"
#include "Test.h"
#include "Graphics\RHILoader.h"
#include "RHI/Instance.h"
#include "Windows/RenderWindowAPI.h"
#include "ShaderCompiler/ShaderCompilerAPI.h"

using namespace NebulaEngine;

#ifdef TEST_WINDOWS

Containers::Vector<u32> windowsList;

const int k_WindowsCount = 4;

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
        
        for (int i = 0; i < k_WindowsCount; ++i)
        {
            windowsList.emplace_back(Platforms::CreateRenderWindow(nullptr, WinProc, 640, 480));
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
            1, 0, 0
        };
        
        Graphics::RHILoader::SetCurrentGraphicsAPI(RHI::GraphsicsAPI::Vulkan);
        m_Instance = Graphics::RHILoader::CreateInstance(std::move(app_info));
        auto env = m_Instance->GetEnvString();
        // LOG_INFO(std::move(env));

        // init surfaces
        for (auto& windowId : windowsList)
        {
            m_Instance->CreateSurface(std::move(windowId));
        }

        // pick physical device
        m_Instance->PickPhysicalDevice();
        
        // init logical devices
        m_Instance->InitLogicDevices();
        
        Platforms::InitDXC();
        
        namespace fs = std::filesystem;
        auto currentPath = fs::current_path().generic_wstring() + L"\\Shader";
        auto path = currentPath + L"\\FullScreen.hlsl";
        
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
            currentPath + L"\\FullScreen.vert.spirv"
        };

        Platforms::ShaderCompilerOutput outputVertex;
        if (Platforms::CompileShaderFromFile(std::move(vertexParams), outputVertex))
        {
            LOG_DEBUG("Vertex Shader Compilation done.");
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
            currentPath + L"\\FullScreen.frag.spirv"
        };

        Platforms::ShaderCompilerOutput outputfragment;
        if (Platforms::CompileShaderFromFile(std::move(fragmentParams), outputfragment))
        {
            LOG_DEBUG("Fragment Shader Compilation done.");
        }
        
        
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

        windowsList.clear();
    }
};

#endif

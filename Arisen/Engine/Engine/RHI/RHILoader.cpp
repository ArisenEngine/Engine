#include "RHILoader.h"
#include "Logger/Logger.h"

#include <dbghelp.h>

#pragma comment(lib, "Dbghelp.lib")


void ArisenEngine::Graphics::RHILoader::SetCurrentGraphicsAPI(RHI::GraphsicsAPI api_type)
{
    if (_rhi_dll != NULL && _api_type == api_type)
    {
        return;
    }

    if (_rhi_dll != NULL)
    {
        FreeLibrary(_rhi_dll);
        _rhi_dll = NULL;
    }


    switch (api_type)
    {
    case RHI::GraphsicsAPI::Vulkan:
        _rhi_dll = LoadLibraryA("RHI.Vulkan.dll");
        break;

    default:
        LOG_FATAL("Unsupported graphics api.");
    }

    char dllPath[MAX_PATH];
    DWORD result = GetModuleFileNameA(_rhi_dll, dllPath, MAX_PATH);
    if (result != 0)
    {
        LOG_INFO("[RHILoader::SetCurrentGraphicsAPI] RHI dll loaded: " + std::string(dllPath));
    }

    HANDLE process = GetCurrentProcess();

    // 初始化符号解析器
    if (!SymInitialize(process, nullptr, TRUE))
    {
        DWORD error = GetLastError();
        if (error == ERROR_INVALID_FUNCTION)
        {
            LOG_DEBUG("Symbols are already initialized.");
        }
        else
        {
            LOG_FATAL("SymInitialize failed. Error code: " + error);
        }
    }
    else
    {
        LOG_DEBUG("Symbols initialized successfully.");
    }

    
    // 获取 DLL 的基地址
    DWORD64 moduleBase = (DWORD64)_rhi_dll;

    // 手动加载符号文件
    if (SymLoadModuleEx(
            process,
            nullptr,
            dllPath,        // DLL 文件路径
            nullptr,        // 模块名称（可选）
            moduleBase,     // 模块基地址
            0,              // 模块大小（可为 0，DbgHelp 自动计算）
            nullptr,
            0))
    {
        LOG_INFO("[RHILoader::SetCurrentGraphicsAPI]" + std::string(dllPath) + " Symbols loaded.");
        SymRefreshModuleList(process);

        IMAGEHLP_MODULE64 moduleInfo = { sizeof(IMAGEHLP_MODULE64) };
        if (SymGetModuleInfo64(process, moduleBase, &moduleInfo))
        {
            LOG_INFO("Loaded symbols: " + std::string(moduleInfo.LoadedImageName) + ", Loaded PDB Name: " + std::string(moduleInfo.LoadedPdbName));
        }
        else
        {
           LOG_FATAL("Failed to get module info. Error: " + GetLastError());
        }

    }
    else
    {
        LOG_FATAL("Failed to load symbols for: " + std::string(dllPath));
        
    }

}

ArisenEngine::RHI::Instance* ArisenEngine::Graphics::RHILoader::CreateInstance(RHI::InstanceInfo&& app_info)
{
    if (_rhi_dll == NULL)
    {
        LOG_FATAL(" RHI dll not loaded !");
        throw std::exception(" RHI dll not loaded !");
    }

    typedef RHI::Instance* (__fastcall* InstanceCreate)(RHI::InstanceInfo&& app_info);
    InstanceCreate createInstance;

    createInstance = (InstanceCreate)GetProcAddress(_rhi_dll, "CreateInstance");

    ASSERT(createInstance);

    return createInstance(std::move(app_info));
}

void ArisenEngine::Graphics::RHILoader::Dispose()
{
    if (_rhi_dll != NULL)
    {
        FreeLibrary(_rhi_dll);
        _rhi_dll = NULL;
    }

    HANDLE process = GetCurrentProcess();
    SymCleanup(process);
}

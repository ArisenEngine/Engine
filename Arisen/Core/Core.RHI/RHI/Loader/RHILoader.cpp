#include "RHILoader.h"
#include "Logger/Logger.h"
#include "Base/FoundationMinimal.h"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <dbghelp.h>

#pragma comment(lib, "Dbghelp.lib")

namespace ArisenEngine::RHI
{
    void RHILoader::SetCurrentGraphicsAPI(GraphicsAPI api_type)
    {
        if (_rhi_dll != nullptr && _api_type == api_type)
        {
            return;
        }

        if (_rhi_dll != nullptr)
        {
            FreeLibrary((HMODULE)_rhi_dll);
            _rhi_dll = nullptr;
        }

        switch (api_type)
        {
        case GraphicsAPI::Vulkan:
            _rhi_dll = LoadLibraryA("RHI.Vulkan.dll");
            break;

        default:
            LOG_FATAL("Unsupported graphics api.");
            return;
        }

        if (!_rhi_dll)
        {
            LOG_FATAL("Failed to load RHI dll.");
            return;
        }

        _api_type = api_type;

        char dllPath[MAX_PATH];
        DWORD result = GetModuleFileNameA((HMODULE)_rhi_dll, dllPath, MAX_PATH);
        if (result != 0)
        {
            LOG_INFO(String::Format("[RHILoader::SetCurrentGraphicsAPI] RHI dll loaded: %s", dllPath));
        }

        HANDLE process = GetCurrentProcess();

        // Initialize symbol handler
        if (!SymInitialize(process, nullptr, FALSE))
        {
            DWORD error = GetLastError();
            if (error == ERROR_INVALID_FUNCTION)
            {
                LOG_DEBUG("Symbols are already initialized.");
            }
            else
            {
                LOG_FATAL(String::Format("SymInitialize failed. Error code: %lu", error));
            }
        }
        else
        {
            LOG_DEBUG("Symbols initialized successfully.");
        }

        // Unload previous symbols for this module if they exist (to handle reloads)
        DWORD64 moduleBase = (DWORD64)_rhi_dll;
        SymUnloadModule64(process, moduleBase);

        // Load symbols for the module
        if (SymLoadModuleEx(
            process,
            nullptr,
            dllPath,
            nullptr,
            moduleBase,
            0,
            nullptr,
            0))
        {
            LOG_INFO(String::Format("[RHILoader::SetCurrentGraphicsAPI] %s Symbols loaded.", dllPath));
            SymRefreshModuleList(process);

            IMAGEHLP_MODULE64 moduleInfo = {sizeof(IMAGEHLP_MODULE64)};
            if (SymGetModuleInfo64(process, moduleBase, &moduleInfo))
            {
                LOG_INFO(
                    String::Format("Loaded symbols: %s, Loaded PDB Name: %s", moduleInfo.LoadedImageName, moduleInfo.
                        LoadedPdbName));
            }
        }
        else
        {
            LOG_WARN(String::Format("Failed to load symbols for: %s. Error: %lu", dllPath, GetLastError()));
        }
    }

    RHIInstance* RHILoader::CreateInstance(RHIInstanceInfo&& app_info)
    {
        if (_rhi_dll == nullptr)
        {
            LOG_FATAL("RHI dll not loaded!");
            throw std::exception("RHI dll not loaded!");
        }

        typedef RHIInstance* (__fastcall*InstanceCreate)(RHIInstanceInfo&& app_info);
        InstanceCreate createInstance = (InstanceCreate)GetProcAddress((HMODULE)_rhi_dll, "CreateInstance");

        if (!createInstance)
        {
            LOG_FATAL("Failed to find 'CreateInstance' in RHI dll.");
            return nullptr;
        }

        return createInstance(std::move(app_info));
    }

    void RHILoader::Dispose()
    {
        if (_rhi_dll != nullptr)
        {
            FreeLibrary((HMODULE)_rhi_dll);
            _rhi_dll = nullptr;
        }

        HANDLE process = GetCurrentProcess();
        SymCleanup(process);
    }
} // namespace ArisenEngine::RHI

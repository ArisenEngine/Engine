#include "RHILoader.h"
#include "Logger/Logger.h"

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
        LOG_INFO("rhi: " + std::string(dllPath));
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
}

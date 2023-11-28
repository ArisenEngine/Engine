#include "RHILoader.h"

#include "Debugger/Logger.h"

void NebulaEngine::Graphics::RHILoader::SetCurrentGraphicsAPI(RHI::GraphsicsAPI api_type)
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
        Debugger::Logger::Fatal("Unsupported graphics api.");
    }
}

NebulaEngine::RHI::Instance* NebulaEngine::Graphics::RHILoader::CreateInstance(RHI::AppInfo&& app_info)
{
    if (_rhi_dll == NULL)
    {
        LOG_FATAL(" RHI dll not loaded !");
        throw std::exception(" RHI dll not loaded !");
    }

    typedef RHI::Instance* (__fastcall* InstanceCreate)(RHI::AppInfo&& app_info);
    InstanceCreate createInstance;

    createInstance = (InstanceCreate)GetProcAddress(_rhi_dll, "CreateInstance");

    ASSERT(createInstance);

    return createInstance(std::move(app_info));
}

void NebulaEngine::Graphics::RHILoader::Dispose()
{
    if (_rhi_dll != NULL)
    {
        FreeLibrary(_rhi_dll);
        _rhi_dll = NULL;
    }
}

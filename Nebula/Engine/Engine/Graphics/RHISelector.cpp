#include "RHISelector.h"

#include "Debugger/Logger.h"

void NebulaEngine::Graphics::RHISelector::SetCurrentGraphicsAPI(RHI::GraphsicsAPI api_type)
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

void NebulaEngine::Graphics::RHISelector::Dispose()
{
    if (_rhi_dll != NULL)
    {
        FreeLibrary(_rhi_dll);
        _rhi_dll = NULL;
    }
}

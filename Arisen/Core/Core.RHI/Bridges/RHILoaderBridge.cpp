// RHILoaderBridge.cpp — extern "C" bridge for RHILoader static methods
// Auto-included by collect_sources → compiled into Core.RHI.dll
#include "RHI/Loader/RHILoader.h"
#include "RHI/Core/RHIInstance.h"
#include "RHI/Definitions/CoreRHICommon.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_BEGIN_BRIDGE("RHILoader", "Core.RHI.dll", "Arisen.Native.RHI")

extern "C" {
RHI_DLL void RHILoader_SetCurrentGraphicsAPI(int apiType)
{
    RHILoader::SetCurrentGraphicsAPI(static_cast<GraphicsAPI>(apiType));
}

RHI_DLL void* RHILoader_CreateInstance(
    const char* name, const char* engineName, int validationLayer,
    uint32_t variant, uint32_t major_ver, uint32_t minor_ver, uint32_t patch,
    uint32_t appMajor, uint32_t appMinor, uint32_t appPatch,
    uint32_t engineMajor, uint32_t engineMinor, uint32_t enginePatch,
    uint32_t maxFramesInFlight)
{
    RHIInstanceInfo info{};
    info.name = name;
    info.engineName = engineName;
    info.validationLayer = validationLayer != 0;
    info.variant = variant;
    info.major = major_ver;
    info.minor = minor_ver;
    info.patch = patch;
    info.appMajor = appMajor;
    info.appMinor = appMinor;
    info.appPatch = appPatch;
    info.engineMajor = engineMajor;
    info.engineMinor = engineMinor;
    info.enginePatch = enginePatch;
    info.maxFramesInFlight = maxFramesInFlight;
    return static_cast<void*>(RHILoader::CreateInstance(std::move(info)));
}

RHI_DLL void RHILoader_Dispose()
{
    RHILoader::Dispose();
}
} // extern "C"

ARISEN_BIND_END_BRIDGE()

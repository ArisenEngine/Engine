#pragma once
#include "RHI/Definitions/GraphicsAPI.h"
#include "RHI/Instance/RHIInstance.h"
#include "../EngineCommon.h"


namespace ArisenEngine::Graphics
{
    class ENGINE_DLL RHILoader
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHILoader)

        static void SetCurrentGraphicsAPI(RHI::GraphicsAPI api_type);
        static RHI::RHIInstance* CreateInstance(RHI::InstanceInfo&& app_info);
        static void Dispose();
    private:

        static inline RHI::GraphicsAPI _api_type {RHI::GraphicsAPI::None};
        static inline HMODULE _rhi_dll {NULL};
    };

    // extern "C" ENGINE_DLL void SetGraphicsAPI(RHI::GraphsicsAPI api_type);
    // inline void SetGraphicsAPI(RHI::GraphsicsAPI api_type)
    // {
    //     RHILoader::SetCurrentGraphicsAPI(api_type);
    // }
    //
    // extern "C" ENGINE_DLL RHI::Instance* CreateInstance(RHI::InstanceInfo&& app_info);
    // inline RHI::Instance* CreateInstance(RHI::InstanceInfo&& app_info)
    // {
    //     return RHILoader::CreateInstance(std::move(app_info));
    // }

}


#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    ARISEN_BIND_ENUM(GraphicsAPI)

    enum class GraphicsAPI
    {
        None = 0,
        Vulkan,
        DirectX12,
        Metal
    };
}

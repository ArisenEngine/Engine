#pragma once
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

namespace ArisenEngine::RHI
{
    // For DXC 
    ARISEN_BIND_ENUM(EProgramStage)

    typedef enum EProgramStage
    {
        Vertex = 0,
        Hull,
        Domain,
        Geometry,
        Fragment,
        Compute,
        // Shader Model 6.3
        RayTracing,
        // Shader Model 6.5
        Amplification,
        // Shader Model 6.5
        Mesh,
        STAGE_MAX
    } EProgramStage;
}

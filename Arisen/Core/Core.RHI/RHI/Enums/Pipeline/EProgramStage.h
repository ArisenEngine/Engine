#pragma once

namespace ArisenEngine::RHI
{
    // For DXC 
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

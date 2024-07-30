#pragma once

namespace NebulaEngine::RHI
{
    // For DXC 
    typedef enum ProgramStage
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
    } ProgramStage;
    
}

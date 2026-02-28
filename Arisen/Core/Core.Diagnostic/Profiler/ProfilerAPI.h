#pragma once
#include "Base/BindingMacros.h"
#include "../CoreDiagnosticCommon.h"

ARISEN_BIND_MODULE("Core.Diagnostic.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.Diagnostics")

namespace ArisenEngine::Diagnostics
{
    /**
     * @brief Context for a profiling zone, matches TracyCZoneCtx.
     */
    ARISEN_BIND_STRUCT(ProfilerZoneContext)

    struct ProfilerZoneContext
    {
        uint32_t id;
        int32_t active;
    };

    /**
     * @brief Bridge functions for the Profiler system.
     */
    ARISEN_BIND_BEGIN_BRIDGE("Profiler", "Core.Diagnostic.dll", "Arisen.Native.Diagnostics")

    extern "C" {
    DIAGNOSTIC_DLL ProfilerZoneContext Profiler_BeginZone(const char* name);
    DIAGNOSTIC_DLL void Profiler_EndZone(ProfilerZoneContext ctx);
    DIAGNOSTIC_DLL void Profiler_FrameMark();
    DIAGNOSTIC_DLL void Profiler_FrameMarkNamed(const char* name);
    DIAGNOSTIC_DLL void Profiler_PlotValue(const char* name, double value);
    DIAGNOSTIC_DLL void Profiler_SetThreadName(const char* name);
    }

    ARISEN_BIND_END_BRIDGE()
}

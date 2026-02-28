#include "Profiler.h"
#include "ProfilerAPI.h"
#include <cstring>

#if ARISEN_PROFILER_ENABLED
#include <tracy/TracyC.h>
#endif

namespace ArisenEngine::Diagnostics
{
    ProfilerZoneContext Profiler_BeginZone(const char* name)
    {
#if ARISEN_PROFILER_ENABLED
        // Allocate a source location for the dynamic zone name.
        // Note: In highly performance-sensitive C# code, frequent allocation of srcloc should be avoided.
        uint64_t srcloc = ___tracy_alloc_srcloc(0, "ProfilerAPI.cs", 0, "CSharpZone", 10, 0);
        TracyCZoneCtx ctx = ___tracy_emit_zone_begin_alloc(srcloc, 1);
        if (name)
        {
            ___tracy_emit_zone_name(ctx, name, std::strlen(name));
        }
        return { ctx.id, ctx.active };
#else
        return { 0, 0 };
#endif
    }

    void Profiler_EndZone(ProfilerZoneContext ctx)
    {
#if ARISEN_PROFILER_ENABLED
        TracyCZoneCtx tracy_ctx = { ctx.id, ctx.active };
        ___tracy_emit_zone_end(tracy_ctx);
#endif
    }

    void Profiler_FrameMark()
    {
#if ARISEN_PROFILER_ENABLED
        TracyCFrameMark;
#endif
    }

    void Profiler_FrameMarkNamed(const char* name)
    {
#if ARISEN_PROFILER_ENABLED
        TracyCFrameMarkNamed(name);
#endif
    }

    void Profiler_PlotValue(const char* name, double value)
    {
#if ARISEN_PROFILER_ENABLED
        TracyCPlot(name, value);
#endif
    }

    void Profiler_SetThreadName(const char* name)
    {
#if ARISEN_PROFILER_ENABLED
        TracyCSetThreadName(name);
#endif
    }
}

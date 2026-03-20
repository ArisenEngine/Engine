#pragma once

#ifndef ARISEN_PROFILER_ENABLED
#define ARISEN_PROFILER_ENABLED 0
#endif

#if ARISEN_PROFILER_ENABLED

#ifndef TRACY_ENABLE
#define TRACY_ENABLE
#endif
#include <tracy/Tracy.hpp>

/**
 * @brief Profiling macros for Arisen Engine.
 * These macros wrap Tracy Profiler calls and are zero-overhead when ARISEN_PROFILER_ENABLED is 0.
 */

#define ARISEN_PROFILE_ZONE(name) ZoneScopedN(name)
#define ARISEN_PROFILE_FRAME(name) FrameMarkNamed(name)
#define ARISEN_PROFILE_TAG(name, text) ZoneText(text, strlen(text))
#define ARISEN_PROFILE_VALUE(name, val) TracyPlot(name, val)

#else

#define ARISEN_PROFILE_ZONE(name)
#define ARISEN_PROFILE_FRAME(name)
#define ARISEN_PROFILE_TAG(name, text)
#define ARISEN_PROFILE_VALUE(name, val)

#endif

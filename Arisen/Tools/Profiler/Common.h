#pragma once

#ifdef TOOL_PROFILER_EXPORTS

#define TOOL_PROFILER_DLL   __declspec( dllexport )

#else

#define TOOL_PROFILER_DLL   __declspec( dllimport )

#endif


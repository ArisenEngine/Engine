
#pragma once

#if _WIN64

#define TRACY_IMPORTS  // NOLINT(clang-diagnostic-unused-macros)

/**
* If you are experiencing crashes or freezes when manually loading/unloading a separate DLL with Tracy
integration, you might want to try defining both TRACY_DELAYED_INIT and TRACY_MANUAL_LIFETIME macros.
 **/
// #define TRACY_DELAYED_INIT // NOLINT(clang-diagnostic-unused-macros)
// #define TRACY_MANUAL_LIFETIME // NOLINT(clang-diagnostic-unused-macros)
#else

#error "Not support yet."

#endif

#define TRACY_ENABLE 1
#define TRACY_FIBERS 1

// Manual lifetime and delayed init should define together
#define TRACY_MANUAL_LIFETIME
#define TRACY_DELAYED_INIT

#include <tracy/Tracy.hpp> // NOLINT(clang-diagnostic-invalid-utf8)
#include <tracy/TracyC.h>


#include "Common.h"

namespace Arisen::Tools::Profiler
{
    extern "C"
    {
        static void Initialize()
        {
            // TODO: fix compile link error
            // tracy::StartupProfiler();
        }
        
        static void Terminate()
        {
            // tracy::ShutdownProfiler();
        }
        
        TOOL_PROFILER_DLL void SetThreadName(const char* name);
        TOOL_PROFILER_DLL void SetThreadNameHint(const char* name, int32_t groupHint);
        TOOL_PROFILER_DLL void Message(const char* msg, int32_t callstack_depth = 0);
        TOOL_PROFILER_DLL void ColoredMessage(const char* msg, uint32_t color, int32_t callstack_depth = 0);
        
        TOOL_PROFILER_DLL void SetFrameMark();
        TOOL_PROFILER_DLL void SetFrameMarkStart(const char* name);
        TOOL_PROFILER_DLL void SetFrameMarkEnd(const char* name);
        TOOL_PROFILER_DLL void CaptureFrameImage(const void* image, uint16_t w, uint16_t h, uint8_t offset, bool flip );
        
        TOOL_PROFILER_DLL TracyCZoneCtx MarkNamedZoneScope(const char* name);
        TOOL_PROFILER_DLL TracyCZoneCtx MarkZoneScope();
        TOOL_PROFILER_DLL void MarkZoneEnd(TracyCZoneCtx zone);
        TOOL_PROFILER_DLL void MarkLock();
        TOOL_PROFILER_DLL void MarkMemoryDiscard(const char* name, bool secure);
        TOOL_PROFILER_DLL void MarkMemoryAlloc(const void* ptr, size_t size, bool secure);
        TOOL_PROFILER_DLL void MarkMemoryFree(const void* ptr, bool secure);
        TOOL_PROFILER_DLL void MarkNamedMemoryAlloc(const void* ptr, size_t size, bool secure, const char* name);
        TOOL_PROFILER_DLL void MarkNamedMemoryFree(const void* ptr, bool secure, const char* name);
        TOOL_PROFILER_DLL void MarkFiberEnter(const char* name, int32_t groupHint);
        TOOL_PROFILER_DLL void MarkFiberExit();
        
    }
}


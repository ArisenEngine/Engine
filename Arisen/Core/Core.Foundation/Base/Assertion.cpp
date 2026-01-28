#include "Assertion.h"
#include "../Diagnostics/Log.h"
#include <cstdio>
#include <format>

#if defined(__has_include) && __has_include(<stacktrace>) && __cpp_lib_stacktrace >= 202011
    #include <stacktrace>
    #define HAS_STACKTRACE 1
#else
    #define HAS_STACKTRACE 0
#endif

namespace ArisenEngine
{
    void ReportAssertionFailure(const char* condition, const char* file, int line, const char* function, const char* msg)
    {
        std::string errorMessage = std::format("Assertion Failed: ({})\nFile: {}\nLine: {}\nFunction: {}", 
                                             condition, file, line, function);
        
        if (msg)
        {
            errorMessage += std::format("\nMessage: {}", msg);
        }

#if HAS_STACKTRACE
        try {
            auto trace = std::stacktrace::current();
            errorMessage += "\nStacktrace:\n";
            errorMessage += std::to_string(trace);
        } catch (...) {}
#endif

        // 1. Try to log via engine's log system
        // Log::Fatal might use ASSERT, so we need to be careful, but InternalLog usually just calls the handler
        Diagnostics::Log::Fatal(errorMessage.c_str());

        // 2. Fallback to stderr in case logger is not initialized or crashed
        std::fprintf(stderr, "%s\n", errorMessage.c_str());
        std::fflush(stderr);
    }
}

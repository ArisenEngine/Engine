#include "Assertion.h"
#include "../Diagnostics/Log.h"
#include <cstdio>
#include <format>
#ifdef _WIN32
    #include <crtdbg.h>
#endif

#if defined(__has_include) && __has_include(<stacktrace>) && __cpp_lib_stacktrace >= 202011
    #include <stacktrace>
    #define HAS_STACKTRACE 1
#else
    #define HAS_STACKTRACE 0
#endif

namespace ArisenEngine
{
#ifdef _WIN32
    int CRTReportHook(int reportType, char* message, int* returnValue)
    {
        // reportType can be _CRT_WARN, _CRT_ERROR, or _CRT_ASSERT
        // message contains the assertion text
        
        static bool isHandling = false;
        if (isHandling) return 0; // Avoid recursion
        isHandling = true;

        const char* typeStr = "CRT Unknown";
        if (reportType == _CRT_WARN) typeStr = "CRT Warning";
        else if (reportType == _CRT_ERROR) typeStr = "CRT Error";
        else if (reportType == _CRT_ASSERT) typeStr = "CRT Assert";

        ReportAssertionFailure(message, "Unknown", 0, typeStr, "Caught by CRT Report Hook");
        
        isHandling = false;

        // Return 0 to allow the default reporting to continue (display dialog)
        // Return 1 if we've handled it and want to skip the dialog (but usually we want the dialog for debugging)
        return 0; 
    }
#endif

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
        Diagnostics::Log::Fatal(errorMessage.c_str());

        // 2. Fallback to stderr in case logger is not initialized or crashed
        std::fprintf(stderr, "%s\n", errorMessage.c_str());
        std::fflush(stderr);
    }

    void InitAssertionSystem()
    {
#ifdef _WIN32
        _CrtSetReportHook(CRTReportHook);
#endif
    }
}

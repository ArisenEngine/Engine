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

#ifdef _WIN32
    #include <Windows.h>
    #include <DbgHelp.h>
    #pragma comment(lib, "Dbghelp.lib")
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
#elif defined(_WIN32)
        errorMessage += "\nStacktrace (Windows):\n";
        void* stack[64];
        unsigned short frames = CaptureStackBackTrace(0, 64, stack, NULL);
        
        HANDLE process = GetCurrentProcess();
        char symbolBuffer[sizeof(SYMBOL_INFO) + MAX_SYM_NAME * sizeof(char)];
        PSYMBOL_INFO symbol = (PSYMBOL_INFO)symbolBuffer;
        symbol->SizeOfStruct = sizeof(SYMBOL_INFO);
        symbol->MaxNameLen = MAX_SYM_NAME;

        for (unsigned int i = 0; i < frames; i++)
        {
            if (SymFromAddr(process, (DWORD64)(stack[i]), 0, symbol))
            {
                IMAGEHLP_LINE64 line;
                line.SizeOfStruct = sizeof(IMAGEHLP_LINE64);
                DWORD displacement;
                if (SymGetLineFromAddr64(process, (DWORD64)(stack[i]), &displacement, &line))
                {
                    errorMessage += std::format("{}: {}() - {}:{}\n", 
                        i, symbol->Name, line.FileName, line.LineNumber);
                }
                else
                {
                    errorMessage += std::format("{}: {}() - 0x{:X}\n", 
                        i, symbol->Name, symbol->Address);
                }
            }
            else
            {
                errorMessage += std::format("{}: 0x{:X}\n", i, (uintptr_t)stack[i]);
            }
        }
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
        SymInitialize(GetCurrentProcess(), NULL, TRUE);
#endif
    }
}

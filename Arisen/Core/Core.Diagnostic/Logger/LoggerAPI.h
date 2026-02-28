#pragma once
#include "Base/BindingMacros.h"
#include "../CoreDiagnosticCommon.h"
#include "../../Core.Foundation/Diagnostics/ILogHandler.h"

ARISEN_BIND_MODULE("Core.Diagnostic.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.Diagnostics")

namespace ArisenEngine::Diagnostics
{
    ARISEN_BIND_BEGIN_BRIDGE("Logger", "Core.Diagnostic.dll", "Arisen.Native.Diagnostics")
    extern "C"
    {
        DIAGNOSTIC_DLL void Logger_Log(LogLevel level, const char* msg, const LogSourceLocation* location, const char* thread_name);
        DIAGNOSTIC_DLL void Logger_SetServerityLevel(LogLevel level);
        DIAGNOSTIC_DLL void Logger_BindCallback(void* callback);
        DIAGNOSTIC_DLL bool Logger_Initialize(bool bindCallback);
        DIAGNOSTIC_DLL void Logger_Shutdown();
    }
    ARISEN_BIND_END_BRIDGE()
}

#pragma once

#include "../../EngineCommon.h"
#include "Debugger/Logger.h"

namespace NebulaEngine::API
{
    extern "C" DLL void Debugger_Log(const wchar_t* msg, const char* threadName);

    inline void Debugger_Log(const wchar_t* msg, const char* threadName)
    {
        Debugger::Logger::Log(msg, threadName);
    }

    extern "C" DLL void Debugger_Info(const wchar_t* msg, const char* threadName);

    inline void Debugger_Info(const wchar_t* msg, const char* threadName)
    {
        Debugger::Logger::Info(msg, threadName);
    }

    extern "C" DLL void Debugger_Trace(const wchar_t* msg, const char* threadName);

    inline void Debugger_Trace(const wchar_t* msg, const char* threadName)
    {
        Debugger::Logger::Trace(msg, threadName);
    }

    extern "C" DLL void Debugger_Warning(const wchar_t* msg, const char* threadName);

    inline void Debugger_Warning(const wchar_t* msg, const char* threadName)
    {
        Debugger::Logger::Warning(msg, threadName);
    }

    extern "C" DLL void Debugger_Error(const wchar_t* msg, const char* threadName);

    inline void Debugger_Error(const wchar_t* msg, const char* threadName)
    {
        Debugger::Logger::Error(msg, threadName);
    }

    extern "C" DLL void Debugger_Fatal(const wchar_t* msg, const char* threadName);

    inline void Debugger_Fatal(const wchar_t* msg, const char* threadName)
    {
        Debugger::Logger::Fatal(msg, threadName);
    }
   
}

#pragma once

#include "../../EngineCommon.h"
#include "Debugger/Logger.h"

namespace NebulaEngine::API
{
    extern "C" DLL void Debugger_Log(const wchar_t* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Log(const wchar_t* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Log(msg, threadName, csharpTracce);
    }

    extern "C" DLL void Debugger_Info(const wchar_t* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Info(const wchar_t* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Info(msg, threadName, csharpTracce);
    }

    extern "C" DLL void Debugger_Trace(const wchar_t* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Trace(const wchar_t* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Trace(msg, threadName, csharpTracce);
    }

    extern "C" DLL void Debugger_Warning(const wchar_t* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Warning(const wchar_t* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Warning(msg, threadName, csharpTracce);
    }

    extern "C" DLL void Debugger_Error(const wchar_t* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Error(const wchar_t* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Error(msg, threadName, csharpTracce);
    }

    extern "C" DLL void Debugger_Fatal(const wchar_t* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Fatal(const wchar_t* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Fatal(msg, threadName, csharpTracce);
    }
   
}

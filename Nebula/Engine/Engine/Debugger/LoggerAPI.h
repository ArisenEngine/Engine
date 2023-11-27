#pragma once

#include "../EngineCommon.h"
#include "Debugger/Logger.h"

namespace NebulaEngine::Debugger
{
    extern "C" DLL void Debugger_Log(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Log(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Log(msg, threadName, csharpTracce);
    }

    extern "C" DLL void Debugger_Info(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Info(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Info(msg, threadName, csharpTracce);
    }

    extern "C" DLL void Debugger_Trace(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Trace(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Trace(msg, threadName, csharpTracce);
    }

    extern "C" DLL void Debugger_Warning(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Warning(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Warning(msg, threadName, csharpTracce);
    }

    extern "C" DLL void Debugger_Error(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Error(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Error(msg, threadName, csharpTracce);
    }

    extern "C" DLL void Debugger_Fatal(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Fatal(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::Fatal(msg, threadName, csharpTracce);
    }
   
    extern "C" DLL void Debugger_BindCallback(LogCallback callback);
    
    inline void Debugger_BindCallback(LogCallback callback)
    {
        Debugger::Logger::BindCallback(callback);
    }

    extern "C" DLL void Debugger_Flush();

    inline void Debugger_Flush()
    {
        Debugger::Logger::Exit();
    }

    extern "C" DLL bool Debugger_Initialize();

    inline bool Debugger_Initialize()
    {
        return Debugger::Logger::Initialize();
    }

}

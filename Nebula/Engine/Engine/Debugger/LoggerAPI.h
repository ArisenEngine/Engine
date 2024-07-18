#pragma once

#include "../EngineCommon.h"
#include "Logger/Logger.h"

namespace NebulaEngine::Debugger
{
    extern "C" ENGINE_DLL void Debugger_Log(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Log(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::GetInstance().Log(msg, threadName, csharpTracce);
    }

    extern "C" ENGINE_DLL void Debugger_Info(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Info(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::GetInstance().Info(msg, threadName, csharpTracce);
    }

    extern "C" ENGINE_DLL void Debugger_Trace(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Trace(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::GetInstance().Trace(msg, threadName, csharpTracce);
    }

    extern "C" ENGINE_DLL void Debugger_Warning(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Warning(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::GetInstance().Warning(msg, threadName, csharpTracce);
    }

    extern "C" ENGINE_DLL void Debugger_Error(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Error(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::GetInstance().Error(msg, threadName, csharpTracce);
    }

    extern "C" ENGINE_DLL void Debugger_Fatal(const char* msg, const char* threadName, const char* csharpTracce);

    inline void Debugger_Fatal(const char* msg, const char* threadName, const char* csharpTracce)
    {
        Debugger::Logger::GetInstance().Fatal(msg, threadName, csharpTracce);
    }
   
    extern "C" ENGINE_DLL void Debugger_BindCallback(LogCallback callback);
    
    inline void Debugger_BindCallback(LogCallback callback)
    {
        Debugger::Logger::GetInstance().BindCallback(callback);
    }

    extern "C" ENGINE_DLL void Debugger_Flush();

    inline void Debugger_Flush()
    {
        Debugger::Logger::Dispose();
    }

    extern "C" ENGINE_DLL bool Debugger_Initialize();

    inline bool Debugger_Initialize()
    {
        return Debugger::Logger::GetInstance().Initialize();
    }

}

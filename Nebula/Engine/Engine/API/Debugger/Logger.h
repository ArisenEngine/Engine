#pragma once
#include "../../EngineCommon.h"
#include "Common/CommandHeaders.h"
#include "Debugger/Logger.h"

namespace NebulaEngine::API
{
    extern "C" DLL void Debugger_Log(const wchar_t* msg);

    inline void Debugger_Log(const wchar_t* msg)
    {
        Debugger::Logger::Log(msg);
    }
   
}

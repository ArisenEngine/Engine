#pragma once

#ifdef COREDEBUGGER_EXPORTS

#define DEBUGGER_DLL   __declspec( dllexport )

#else

#define DEBUGGER_DLL   __declspec( dllimport )

#endif

extern "C" DEBUGGER_DLL void dummy_core_debugger_function();
inline void dummy_core_debugger_function()
{
    
}
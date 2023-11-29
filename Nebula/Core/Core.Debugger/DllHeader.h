#pragma once
#ifdef COREDEBUGGER_EXPORTS

#define DEBUGGER_DLL   __declspec( dllexport )

#else

#define DEBUGGER_DLL   __declspec( dllimport )

#endif 
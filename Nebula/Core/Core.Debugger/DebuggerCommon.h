#pragma once


#ifdef COREDEBUGGER_EXPORTS

#define DLL   __declspec( dllexport )

#else

#define DLL   __declspec( dllimport )

#endif 
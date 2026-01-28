#pragma once

#ifdef COREDIAGNOSTIC_EXPORTS
#define DIAGNOSTIC_DLL   __declspec( dllexport )
#else
#define DIAGNOSTIC_DLL   __declspec( dllimport )
#endif

extern "C" DIAGNOSTIC_DLL void dummy_core_diagnostic_function();
inline void dummy_core_diagnostic_function()
{
}
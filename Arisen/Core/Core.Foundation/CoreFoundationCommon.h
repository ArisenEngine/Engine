#pragma once

#ifdef COREFOUNDATION_EXPORTS
#define FOUNDATION_DLL   __declspec( dllexport )
#else
#define FOUNDATION_DLL   __declspec( dllimport )
#endif

extern "C" FOUNDATION_DLL void dummy_core_foundation_function();
inline void dummy_core_foundation_function()
{
}
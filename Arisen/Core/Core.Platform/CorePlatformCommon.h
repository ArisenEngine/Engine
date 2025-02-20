#pragma once

#ifdef PLATFORM_EXPORTS

#define PLATFORM_DLL   __declspec( dllexport )

#else

#define PLATFORM_DLL   __declspec( dllimport )

#endif

extern "C" PLATFORM_DLL void dummy_core_platform_function();
inline void dummy_core_platform_function()
{
    
}
#pragma once

#ifdef COREINFRA_EXPORTS

#define COREINFRA_DLL   __declspec( dllexport )

#else

#define COREINFRA_DLL   __declspec( dllimport )

#endif

extern "C" COREINFRA_DLL void dummy_core_infra_function();
inline void dummy_core_infra_function()
{
    
}
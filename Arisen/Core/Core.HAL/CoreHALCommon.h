#pragma once

#ifdef COREHAL_EXPORTS
#define HAL_DLL   __declspec( dllexport )
#else
#define HAL_DLL   __declspec( dllimport )
#endif

extern "C" HAL_DLL void dummy_core_hal_function();
inline void dummy_core_hal_function()
{
}
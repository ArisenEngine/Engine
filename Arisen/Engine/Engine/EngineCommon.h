#pragma once

#ifdef ENGINE_EXPORTS

#define ENGINE_DLL   __declspec( dllexport )

#else

#define ENGINE_DLL   __declspec( dllimport )

#endif
extern "C" ENGINE_DLL void dummy_engine_function();
inline void dummy_engine_function()
{
    
}
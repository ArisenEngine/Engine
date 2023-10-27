#pragma once


#ifdef RHIVULKAN_EXPORTS

#define DLL   __declspec( dllexport )

#else

#define DLL   __declspec( dllimport )

#endif 
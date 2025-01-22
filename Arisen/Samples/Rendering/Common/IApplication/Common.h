#pragma once


#ifdef IAPPLICATION_EXPORTS

#define DLL   __declspec( dllexport )

#else

#define DLL   __declspec( dllimport )

#endif // IAPPLICATION_EXPORTS
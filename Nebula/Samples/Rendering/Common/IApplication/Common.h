#pragma once


#ifndef _IAPPLICATION_dll

#define DLL   __declspec( dllexport )

#else

#define DLL   __declspec( dllimport )

#endif // !_IAPPLICATION_dll
#pragma once


#ifdef SOLUTIONMANAGER_EXPORTS

#define DLL   __declspec( dllexport )

#else

#define DLL   __declspec( dllimport )

#endif 
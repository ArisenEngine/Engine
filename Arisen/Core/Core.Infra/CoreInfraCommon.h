#pragma once

#ifdef COREINFRA_EXPORTS

#define COREINFRA_DLL   __declspec( dllexport )

#else

#define COREINFRA_DLL   __declspec( dllimport )

#endif
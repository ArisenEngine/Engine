#pragma once

#include "./Common/CommandHeaders.h"

#ifdef PLATFORM_EXPORTS

#define PLATFORM_DLL   __declspec( dllexport )

#else

#define PLATFORM_DLL   __declspec( dllimport )

#endif 
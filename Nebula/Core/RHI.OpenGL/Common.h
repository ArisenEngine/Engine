#pragma once

#include "./Common/CommandHeaders.h"

#ifdef RHIOPENGL_EXPORTS

#define DLL   __declspec( dllexport )

#else

#define DLL   __declspec( dllimport )

#endif 
#pragma once

#include "./Common/CommandHeaders.h"

#ifdef RHIOPENGL_EXPORTS

#define RHI_OPENGL_DLL   __declspec( dllexport )

#else

#define RHI_OPENGL_DLL   __declspec( dllimport )

#endif 
#pragma once

#include "./Common/CommandHeaders.h"

#ifdef RHIVULKAN_EXPORTS

#define DLL   __declspec( dllexport )

#else

#define DLL   __declspec( dllimport )

#endif


// validation layers
static NebulaEngine::Containers::Vector<const char*> ValidationLayers
{
    "VK_LAYER_KHRONOS_validation"
};
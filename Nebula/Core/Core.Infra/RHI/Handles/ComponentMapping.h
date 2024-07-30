#pragma once
#include "RHI/Enums/Image/ComponentSwizzle.h"

namespace NebulaEngine::RHI
{
    typedef struct ComponentMapping {
        ComponentSwizzle    r;
        ComponentSwizzle    g;
        ComponentSwizzle    b;
        ComponentSwizzle    a;
    } ComponentMapping;
}

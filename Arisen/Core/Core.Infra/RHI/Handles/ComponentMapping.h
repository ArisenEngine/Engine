#pragma once
#include "RHI/Enums/Image/ComponentSwizzle.h"

namespace ArisenEngine::RHI
{
    typedef struct ComponentMapping {
        ComponentSwizzle    r;
        ComponentSwizzle    g;
        ComponentSwizzle    b;
        ComponentSwizzle    a;
    } ComponentMapping;
}

#pragma once

#include "Common/CommandHeaders.h"

namespace ArisenEngine::RHI
{
    // 32-bit index + 32-bit generation: safe handle for registry-backed resources.
    struct RHIResourceHandle final
    {
        UInt32 index { 0xFFFFFFFFu };
        UInt32 generation { 0 };

        [[nodiscard]] bool IsValid() const { return index != 0xFFFFFFFFu; }
        static RHIResourceHandle Invalid() { return {}; }
    };
}


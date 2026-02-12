#pragma once
#include "Base/FoundationMinimal.h"

namespace ArisenEngine::RHI
{
    /**
     * @brief Fragment shading rates for Variable Rate Shading (VRS).
     * Maps directly to VkExtent2D for fragment shading rate.
     */
    enum class EShadingRate : UInt32
    {
        Rate1x1 = 0, // Normal shading
        Rate1x2 = 1,
        Rate2x1 = 2,
        Rate2x2 = 3,
        Rate2x4 = 4,
        Rate4x2 = 5,
        Rate4x4 = 6
    };
}

#pragma once
#include "Base/FoundationMinimal.h"

namespace ArisenEngine::RHI
{
    /**
     * @brief Combiner operations for Variable Rate Shading (VRS).
     * Specifies how the pipeline shading rate, primitive shading rate, and attachment shading rate are combined.
     */
    enum class EShadingRateCombiner : UInt32
    {
        Keep = 0,    // Use the first rate
        Replace = 1, // Use the second rate
        Min = 2,     // Use the minimum of the two rates
        Max = 3,     // Use the maximum of the two rates
        Mul = 4      // Multiply the two rates
    };
}

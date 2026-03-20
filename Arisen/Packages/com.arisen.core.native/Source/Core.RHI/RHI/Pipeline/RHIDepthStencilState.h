#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/Sampler/ECompareOp.h"

namespace ArisenEngine::RHI
{
    typedef enum EStencilOp
    {
        STENCIL_OP_KEEP = 0,
        STENCIL_OP_ZERO = 1,
        STENCIL_OP_REPLACE = 2,
        STENCIL_OP_INCREMENT_AND_CLAMP = 3,
        STENCIL_OP_DECREMENT_AND_CLAMP = 4,
        STENCIL_OP_INVERT = 5,
        STENCIL_OP_INCREMENT_AND_WRAP = 6,
        STENCIL_OP_DECREMENT_AND_WRAP = 7,
        STENCIL_OP_MAX_ENUM = 0x7FFFFFFF
    } EStencilOp;

    struct RHIStencilOpState
    {
        EStencilOp failOp;
        EStencilOp passOp;
        EStencilOp depthFailOp;
        ECompareOp compareOp;
        UInt32 compareMask;
        UInt32 writeMask;
        UInt32 reference;
    };

    struct RHIDepthStencilState
    {
        bool depthTestEnable;
        bool depthWriteEnable;
        ECompareOp depthCompareOp;
        bool depthBoundsTestEnable;
        bool stencilTestEnable;
        RHIStencilOpState front;
        RHIStencilOpState back;
        Float32 minDepthBounds;
        Float32 maxDepthBounds;

        RHIDepthStencilState()
            : depthTestEnable(false)
              , depthWriteEnable(false)
              , depthCompareOp(COMPARE_OP_LESS)
              , depthBoundsTestEnable(false)
              , stencilTestEnable(false)
              , front{STENCIL_OP_KEEP, STENCIL_OP_KEEP, STENCIL_OP_KEEP, COMPARE_OP_ALWAYS, 0xFF, 0xFF, 0}
              , back{STENCIL_OP_KEEP, STENCIL_OP_KEEP, STENCIL_OP_KEEP, COMPARE_OP_ALWAYS, 0xFF, 0xFF, 0}
              , minDepthBounds(0.0f)
              , maxDepthBounds(1.0f)
        {
        }
    };
}

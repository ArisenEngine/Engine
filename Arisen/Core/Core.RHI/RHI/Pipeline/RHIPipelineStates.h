#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/Pipeline/EPrimitiveTopology.h"
#include "RHI/Enums/Pipeline/EPolygonMode.h"
#include "RHI/Enums/Pipeline/ECullMode.h"
#include "RHI/Enums/Pipeline/EFrontFace.h"
#include "RHI/Enums/Pipeline/EBlendFactor.h"
#include "RHI/Enums/Pipeline/EBlendOp.h"
#include "RHI/Enums/Pipeline/ELogicOp.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Pipeline/EDynamicState.h"

namespace ArisenEngine::RHI
{
    struct RHIInputAssemblyState
    {
        EPrimitiveTopology topology = PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
        bool primitiveRestartEnable = false;
    };

    struct RHITessellationState
    {
        UInt32 patchControlPoints = 3;
    };

    struct RHIRasterizationState
    {
        bool depthClampEnable = false;
        bool rasterizerDiscardEnable = false;
        EPolygonMode polygonMode = EPOLYGON_MODE_FILL;
        ECullModeFlagBits cullMode = ECullModeFlagBits::CULL_MODE_BACK_BIT;
        EFrontFace frontFace = EFrontFace::FRONT_FACE_CLOCKWISE;
        bool depthBiasEnable = false;
        float depthBiasConstantFactor = 0.0f;
        float depthBiasClamp = 0.0f;
        float depthBiasSlopeFactor = 0.0f;
        float lineWidth = 1.0f;
    };

    struct RHIMultisampleState
    {
        ESampleCountFlagBits rasterizationSamples = ESampleCountFlagBits::SAMPLE_COUNT_1_BIT;
        bool sampleShadingEnable = false;
        float minSampleShading = 1.0f;
        const UInt32* pSampleMask = nullptr;
        bool alphaToCoverageEnable = false;
        bool alphaToOneEnable = false;
    };

    struct RHIColorBlendAttachmentState
    {
        bool blendEnable = false;
        EBlendFactor srcColorBlendFactor = BLEND_FACTOR_ONE;
        EBlendFactor dstColorBlendFactor = BLEND_FACTOR_ZERO;
        EBlendOp colorBlendOp = BLEND_OP_ADD;
        EBlendFactor srcAlphaBlendFactor = BLEND_FACTOR_ONE;
        EBlendFactor dstAlphaBlendFactor = BLEND_FACTOR_ZERO;
        EBlendOp alphaBlendOp = BLEND_OP_ADD;
        UInt32 colorWriteMask = 0xF; // Default RGBA
    };

    struct RHIColorBlendState
    {
        bool logicOpEnable = false;
        ELogicOp logicOp = LOGIC_OP_COPY;
        Containers::Vector<RHIColorBlendAttachmentState> attachments;
        float blendConstants[4] = { 0.0f, 0.0f, 0.0f, 0.0f };
    };

    // Bitmask for dynamic states
    enum EDynamicStateFlagBits : UInt64
    {
        DYNAMIC_STATE_VIEWPORT_BIT = 1ULL << 0,
        DYNAMIC_STATE_SCISSOR_BIT = 1ULL << 1,
        DYNAMIC_STATE_LINE_WIDTH_BIT = 1ULL << 2,
        DYNAMIC_STATE_DEPTH_BIAS_BIT = 1ULL << 3,
        DYNAMIC_STATE_BLEND_CONSTANTS_BIT = 1ULL << 4,
        DYNAMIC_STATE_DEPTH_BOUNDS_BIT = 1ULL << 5,
        DYNAMIC_STATE_STENCIL_COMPARE_MASK_BIT = 1ULL << 6,
        DYNAMIC_STATE_STENCIL_WRITE_MASK_BIT = 1ULL << 7,
        DYNAMIC_STATE_STENCIL_REFERENCE_BIT = 1ULL << 8,
        DYNAMIC_STATE_CULL_MODE_BIT = 1ULL << 9,
        DYNAMIC_STATE_FRONT_FACE_BIT = 1ULL << 10,
        DYNAMIC_STATE_PRIMITIVE_TOPOLOGY_BIT = 1ULL << 11,
        DYNAMIC_STATE_DEPTH_TEST_ENABLE_BIT = 1ULL << 12,
        DYNAMIC_STATE_DEPTH_WRITE_ENABLE_BIT = 1ULL << 13,
        DYNAMIC_STATE_DEPTH_COMPARE_OP_BIT = 1ULL << 14,
        DYNAMIC_STATE_STENCIL_TEST_ENABLE_BIT = 1ULL << 15,
        DYNAMIC_STATE_STENCIL_OP_BIT = 1ULL << 16,
        DYNAMIC_STATE_FRAGMENT_SHADING_RATE_BIT = 1ULL << 17,
        // Add more as needed
    };
}

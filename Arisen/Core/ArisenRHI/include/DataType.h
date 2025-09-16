#pragma once
// 这里的数据类型后续需要移动到core.
#include <hlsl++_vector_float.h>
#include "CoreMinimalRHI.h"
#include "Math/Rect.h"

ARISENRHI_BEGIN_NAMEPSACE
    struct Rect
{
    hlslpp::float2 origin;
    hlslpp::float2 size;
};

using Vector4F = hlslpp::float4;
using Vector3F = hlslpp::float3;
using Vector2F = hlslpp::float2;

using FrameSize = ArisenEngine::Math::RectSize<uint32_t>;
ARISENRHI_END_NAMESPACE

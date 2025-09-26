#pragma once
#include "IObject.h"

ARISENRHI_BEGIN_NAMEPSACE

enum class ResourceType
{
    Buffer,
    Texture,
    Sampler,
};

enum class TextureDimensionType : uint32_t
{
    Tex1D = 0,
    Tex1DArray,
    Tex2D,
    Tex2DArray,
};

struct IResource : IObject
{
    
};
ARISENRHI_END_NAMESPACE

#pragma once
#include "CoreMinimalRHI.h"

ARISENRHI_BEGIN_NAMEPSACE

template<typename T, size_t size> 
class Color
{
    
};

template<size_t size>
using ColorF = Color<float, size>;
using Color3F = ColorF<3>;
using Color4F = ColorF<4>;

ARISENRHI_END_NAMESPACE
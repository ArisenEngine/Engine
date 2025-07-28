#pragma once
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE

struct IObject
{
    virtual ~IObject() = default;
    // ref or interface stuffs.
    
    virtual bool SetName(std::string_view name) = 0;
};

ARISENRHI_END_NAMESPACE

#pragma once
#include "CoreMiminalRHI.h"
#include "IObject.h"

ARISENRHI_BEGIN_NAMEPSACE
enum class ContextType
{
    Render,
    Compute,
};

struct IContext : public IObject
{
    
};
ARISENRHI_END_NAMESPACE

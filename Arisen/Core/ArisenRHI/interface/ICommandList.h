#pragma once
#include "IObject.h"

ARISENRHI_BEGIN_NAMEPSACE

enum class CommandListType
{
    Transfer,
    Render,
    Compute,
    __COUNT
};

struct ICommandList : public IObject
{
       
};
ARISENRHI_END_NAMESPACE

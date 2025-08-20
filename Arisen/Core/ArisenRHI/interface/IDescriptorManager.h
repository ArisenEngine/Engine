#pragma once
#include "CoreMinimalRHI.h"
#include "IObject.h"

ARISENRHI_BEGIN_NAMEPSACE
struct IDescriptorManager : public IObject
{
    virtual void CompleteInitialize() = 0;
};
ARISENRHI_END_NAMESPACE

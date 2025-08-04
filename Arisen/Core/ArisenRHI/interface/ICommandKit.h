#pragma once
#include "CoreMinimalRHI.h"
#include "ICommandQueue.h"
#include "IObject.h"

ARISENRHI_BEGIN_NAMEPSACE
struct ICommandKit : public IObject
{
    [[nodiscard]] virtual ICommandQueue& GetQueue() const = 0;
};
ARISENRHI_END_NAMESPACE

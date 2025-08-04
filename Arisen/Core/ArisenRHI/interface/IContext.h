#pragma once
#include "CoreMinimalRHI.h"
#include "IObject.h"

ARISENRHI_BEGIN_NAMEPSACE
enum class ContextType
{
    Render,
    Compute,
};

struct IContext : public IObject
{
    virtual Ptr<ICommandKit> CreateCommandKit(CommandListType type) const = 0;
    virtual ICommandKit& GetDefaultCommandKit(CommandListType type) const = 0;
    
};
ARISENRHI_END_NAMESPACE

#pragma once
#include "CoreMinimalRHI.h"
#include "IObject.h"
#include "ICommandKit.h"
#include "ICommandList.h"

ARISENRHI_BEGIN_NAMEPSACE
enum class ContextType
{
    Render,
    Compute,
};

struct IRHIContext : public IObject
{
    virtual Ptr<ICommandKit> CreateCommandKit(CommandListType type) const = 0;
    virtual Ptr<ICommandQueue> CreateCommandQueue(CommandListType type) const = 0;
    
    virtual ICommandKit& GetDefaultCommandKit(CommandListType type) const = 0;
};
ARISENRHI_END_NAMESPACE

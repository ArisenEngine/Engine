#pragma once
#include "CoreMinimalRHI.h"
#include "ICommandQueue.h"
#include "IRHIContext.h"
#include "ObjectBase.h"

ARISENRHI_BEGIN_NAMEPSACE
class CommandQueueBase : public ObjectBase<ICommandQueue>
{
public:
    CommandQueueBase(const IRHIContext& context, CommandListType type);
    
private:
    const IRHIContext& m_context;
    const CommandListType m_command_list_type;
};
ARISENRHI_END_NAMESPACE

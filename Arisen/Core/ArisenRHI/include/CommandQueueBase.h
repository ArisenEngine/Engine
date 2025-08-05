#pragma once
#include "CoreMinimalRHI.h"
#include "ICommandQueue.h"
#include "IRHIContext.h"
#include "ObjectBase.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename BaseInterface>
class CommandQueueBase : public ObjectBase<BaseInterface>
{
public:
    CommandQueueBase(const IRHIContext& context, CommandListType type)
    :m_context(context),m_command_list_type(type)
    {}
    
private:
    const IRHIContext& m_context;
    const CommandListType m_command_list_type;
};
ARISENRHI_END_NAMESPACE

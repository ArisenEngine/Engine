#include "CommandQueueBase.h"

ARISENRHI_BEGIN_NAMEPSACE
CommandQueueBase::CommandQueueBase(const IRHIContext& context, CommandListType type)
    :m_context(context),m_command_list_type(type)
{
}
ARISENRHI_END_NAMESPACE
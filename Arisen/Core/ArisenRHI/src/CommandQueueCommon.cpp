#include "CommandQueueCommon.h"

#include "Logger/DebugUtils.h"

ARISENRHI_BEGIN_NAMEPSACE
    CommandQueueCommon::CommandQueueCommon(const IRHIContext& context, CommandListType type)
    : CommandQueueBase(context, type)
{
}

CommandQueueCommon::~CommandQueueCommon()
{
    ShutdownQueueExecution();
}

void CommandQueueCommon::ShutdownQueueExecution()
{
    CHECK(0, "Shutdown queue not implemented!");
}

ARISENRHI_END_NAMESPACE

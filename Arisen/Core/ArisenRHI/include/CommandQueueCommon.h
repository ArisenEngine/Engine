#pragma once
#include "CommandQueueBase.h"
#include "CoreMinimalRHI.h"

ARISENRHI_BEGIN_NAMEPSACE
class CommandQueueCommon : public CommandQueueBase
{
public:
    CommandQueueCommon(const IRHIContext& context, CommandListType type);
    ~CommandQueueCommon() override;
    
protected:
    void ShutdownQueueExecution();
};
ARISENRHI_END_NAMESPACE
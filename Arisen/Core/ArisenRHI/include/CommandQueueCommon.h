#pragma once
#include "CommandQueueBase.h"
#include "CoreMinimalRHI.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename BaseInterface> requires std::is_base_of_v<ICommandQueue, BaseInterface>
class CommandQueueCommon : public CommandQueueBase<BaseInterface>
{
public:
    CommandQueueCommon(const IRHIContext& context, CommandListType type)
    : CommandQueueBase<BaseInterface>(context, type)
    {}
    
    ~CommandQueueCommon() override
    {
        LOG_RHI_DEBUG("CommandqueueCommon desctructor!")
        ShutdownQueueExecution();
    }
    
protected:
    void ShutdownQueueExecution()
    {
        LOG_RHI_ERROR(std::format("Shutdown queue not implemented:{}!",static_cast<void*>(this)));
    }
};
ARISENRHI_END_NAMESPACE
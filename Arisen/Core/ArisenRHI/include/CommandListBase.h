#pragma once
#include "ICommandList.h"
#include "ICommandQueue.h"
#include "ObjectBase.h"
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE
template<typename CommandListInterface> requires std::is_base_of_v<ICommandList, CommandListInterface>
class CommandListBase : public ObjectBase<CommandListInterface>
{
public:
    CommandListBase(const ICommandQueue& command_queue, CommandListType type)
        : m_command_queue(command_queue)
        , m_type(type)
    {}
private:
    const ICommandQueue& m_command_queue;
    CommandListType m_type;
};
ARISENRHI_END_NAMESPACE

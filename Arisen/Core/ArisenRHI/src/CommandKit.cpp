#include "CommandKit.h"

ArisenRHI::CommandKit::CommandKit(const IRHIContext& context, CommandListType cmd_list_type)
    :m_context(context), m_cmd_list_type(cmd_list_type)
{
}

bool ArisenRHI::CommandKit::SetName(std::string_view name)
{
    return ObjectBase<ICommandKit>::SetName(name);
}

ArisenRHI::ICommandQueue& ArisenRHI::CommandKit::GetQueue() const
{
    if (m_cmd_queue_ptr)
    {
        return *m_cmd_queue_ptr;
    }

    m_cmd_queue_ptr = m_context.CreateCommandQueue(m_cmd_list_type);
    m_cmd_queue_ptr->SetName(std::format("{} CommandQueue", GetName()));
    return *m_cmd_queue_ptr;
}

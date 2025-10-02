#pragma once
#include "ICommandList.h"
#include "ICommandListSet.h"
#include "ObjectBase.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<ICommandListSet, typename RHIImplTraits::CommandListSetInterface>
class CommandListSetBase : public ObjectBase<typename RHIImplTraits::CommandListSetInterface>
{
public:
    CommandListSetBase(std::vector<Ptr<ICommandList>> command_lists)
    {
    }
};
ARISENRHI_END_NAMESPACE

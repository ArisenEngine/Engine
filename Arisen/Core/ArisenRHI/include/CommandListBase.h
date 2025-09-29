#pragma once
#include "ICommandList.h"
#include "ObjectBase.h"
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE
template<typename CommandListInterface> requires std::is_base_of_v<ICommandList, CommandListInterface>
class CommandListBase : public ObjectBase<CommandListInterface>
{
public:

};
ARISENRHI_END_NAMESPACE

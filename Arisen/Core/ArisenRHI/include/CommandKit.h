#pragma once
#include "CoreMinimalRHI.h"
#include "ICommandKit.h"
#include "ICommandList.h"
#include "IRHIContext.h"
#include "ObjectBase.h"

ARISENRHI_BEGIN_NAMEPSACE
class CommandKit final : public ObjectBase<ICommandKit>
{
public:
   CommandKit(const IContext& context, CommandListType cmd_list_type);

   virtual bool SetName(std::string_view name) override;

   [[nodiscard]] virtual ICommandQueue& GetQueue() const override;

private:
   const IRHIContext& m_context;
   CommandListType m_cmd_list_type;
   mutable Ptr<ICommandQueue> m_cmd_queue_ptr;
};
ARISENRHI_END_NAMESPACE

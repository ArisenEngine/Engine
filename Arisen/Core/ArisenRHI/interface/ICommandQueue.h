#pragma once
#include "ICommandList.h"
#include "ICommandListSet.h"
#include "IFence.h"
#include "IObject.h"
#include "IRenderPass.h"

ARISENRHI_BEGIN_NAMEPSACE
struct ICommandQueue : IObject
{
    [[nodiscard]] virtual Ptr<IRenderCommandList> CreateRenderCommandList(IRenderPass& render_pass) const = 0;
    [[nodiscard]] virtual Ptr<ICommandListSet> CreateCommandListSet(std::vector<Ptr<ICommandList>> command_lists) const = 0;
    [[nodiscard]] virtual Ptr<IFence> CreateFence() const = 0;
};
ARISENRHI_END_NAMESPACE

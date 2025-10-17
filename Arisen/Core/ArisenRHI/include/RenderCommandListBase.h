#pragma once
#include "ICommandList.h"
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<IRenderCommandList, typename RHIImplTraits::RenderCommandListInterface>
class RenderCommandListBase : public CommandListBase<typename RHIImplTraits::RenderCommandListInterface>
{
public:
    RenderCommandListBase(const ICommandQueue& command_queue, IRenderPass& render_pass)
        : CommandListBase<typename RHIImplTraits::RenderCommandListInterface>(command_queue, CommandListType::Render)
        , m_render_pass_ptr(render_pass.GetInterface<IRenderPass>())
    {}

    // IRenderCommandList
    virtual void ResetWithPSO(const IRenderPipelineStateObject& pso) const override
    {
        SetPSO(pso);
    }

    virtual void SetPSO(const IRenderPipelineStateObject& pso) const override
    {
        pso.Apply(*this);
    }

private:
    Ptr<IRenderPass> m_render_pass_ptr;
};
ARISENRHI_END_NAMESPACE

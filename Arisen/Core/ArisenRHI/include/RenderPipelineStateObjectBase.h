#pragma once
#include "IRenderPipelineStateObject.h"
#include "IRHIContext.h"
#include "ObjectBase.h"
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<IRenderPipelineStateObject, typename RHIImplTraits::RenderPipelineStateObjectInterface>
class RenderPipelineStateObjectBase : public ObjectBase<typename RHIImplTraits::RenderPipelineStateObjectInterface>
{
public:
    RenderPipelineStateObjectBase(const IRHIContext& context, const RenderPipelineStateObjectSettings& settings)
        :m_settings(settings), m_context(context)
    {}

    void Reset(const RenderPipelineStateObjectSettings& settings);
private:
    const IRHIContext& m_context;
    RenderPipelineStateObjectSettings m_settings;
};
ARISENRHI_END_NAMESPACE
#pragma once
#include "CoreMinimalRHI.h"
#include "IRenderContext.h"
#include "IRenderPattern.h"
#include "ObjectBase.h"

ARISENRHI_BEGIN_NAMEPSACE
    struct IRenderContext;

    template<typename RHIImplTraits> requires std::is_base_of_v<IRenderPattern, typename RHIImplTraits::RenderPatternInterface>
class RenderPatternBase : public ObjectBase<typename RHIImplTraits::RenderPatternInterface>
{
public:
    RenderPatternBase(IRenderContext& render_context, const RenderPatternSettings& settings)
        :m_render_context_ptr(render_context.GetInterface<IRenderContext>()), m_settings(settings)
        {
        }

private:
    const Ptr<IRenderContext> m_render_context_ptr;
    const RenderPatternSettings m_settings;
};
ARISENRHI_END_NAMESPACE

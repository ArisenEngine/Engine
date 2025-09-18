#pragma once
#include "IRenderPass.h"
#include "IRenderPattern.h"
#include "ObjectBase.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<IRenderPass, typename RHIImplTraits::RenderPassInterface>
class RenderPassBase : public ObjectBase<typename RHIImplTraits::RenderPassInterface>
{
public:
    RenderPassBase(const IRenderPattern& render_pattern, const RenderPassSettings& settings)
    {
        /
    }

    
};



ARISENRHI_END_NAMESPACE

#pragma once
#include "IRenderPass.h"
#include "ObjectBase.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<IRenderPass, typename RHIImplTraits::RenderPassInterface>
class RenderPassBase : public ObjectBase<typename RHIImplTraits::RenderPassInterface>
{
public:
    
};
ARISENRHI_END_NAMESPACE
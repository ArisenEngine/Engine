#include "RenderContextBase.h"

ARISENRHI_BEGIN_NAMEPSACE
RenderContextBase::RenderContextBase(IDevice& device, const RenderContextSettings& settings)
    :ContextBase(device, ContextType::Render)
    ,mSettings(settings)
{
}
ARISENRHI_END_NAMESPACE

#include "RenderContextBase.h"

ArisenRHI::RenderContextBase::RenderContextBase(IDevice& device, const RenderContextSettings& settings)
    :ContextBase(device, ContextType::Render)
    ,mSettings(settings)
{
}

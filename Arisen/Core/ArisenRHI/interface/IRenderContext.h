#pragma once
#include "BasicTypes.h"
#include "IRHIContext.h"
#include "RHIMacros.h"
#include "RHITypes.h"

ARISENRHI_BEGIN_NAMEPSACE
struct RenderContextSettings
{
    FrameSize frameSize{800, 600};
    TextureFormat textureFormat{TextureFormat::BGRA8Unorm};
    TextureFormat depthStencilFormat{TextureFormat::UnKnown};
    uint32_t frameBuffersCount{3};
};

struct IRenderContext : public IRHIContext
{
    using Settings = RenderContextSettings;

    const virtual Settings& GetSettings() const noexcept = 0;
};
ARISENRHI_END_NAMESPACE
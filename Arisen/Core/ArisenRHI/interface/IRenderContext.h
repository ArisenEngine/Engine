#pragma once
#include "BasicTypes.h"
#include "IRHIContext.h"
#include "RHIMacros.h"
#include "RHITypes.h"

ARISENRHI_BEGIN_NAMEPSACE
struct RenderContextSettings
{
    FrameSize frame_size{800, 600};
    TextureFormat texture_format{TextureFormat::BGRA8Unorm};
    TextureFormat depth_stencil_format{TextureFormat::UnKnown};
    uint32_t frame_buffers_Count{3};
    ContextOption options{ContextOption::DefaultProgramBindingsInitialization};
};

struct IRenderContext : public IRHIContext
{
    const virtual RenderContextSettings& GetSettings() const noexcept = 0;
};
ARISENRHI_END_NAMESPACE
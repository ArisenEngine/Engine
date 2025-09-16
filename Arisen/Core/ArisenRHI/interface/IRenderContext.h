#pragma once
#include "IRHIContext.h"
#include "RHIMacros.h"
#include "RHITypes.h"
#include "IViewState.h"
#include "IRenderPattern.h"

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

    [[nodiscard]] virtual Ptr<IRenderPattern> CreateRenderPattern(const RenderPatternSettings& Settings) noexcept = 0;
    [[nodiscard]] virtual Ptr<IViewState> CreateViewState(const ViewSettings& view_settings) noexcept = 0;
};
ARISENRHI_END_NAMESPACE
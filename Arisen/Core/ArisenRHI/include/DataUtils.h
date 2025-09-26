#pragma once
#include "DataType.h"
#include "IRenderContext.h"
#include "ITexture.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE

Rect GetRect(FrameSize frameSize);

TextureSettings ConvertToTextureSettings(const RenderContextSettings& render_context_settings, uint32_t frame_index);



ARISENRHI_END_NAMESPACE

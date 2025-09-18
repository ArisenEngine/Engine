#include "DataUtils.h"
ARISENRHI_BEGIN_NAMEPSACE

Rect GetRect(FrameSize frameSize)
{
    return Rect{{0.f, 0.f}, {frameSize.GetWidth(), frameSize.GetHeight()}};
}

TextureSettings ConvertToTextureSettings(const RenderContextSettings& render_context_settings,
    uint32_t frame_index)
{
    TextureSettings texture_settings;
    texture_settings.type = TextureType::FrameBuffer;
    texture_settings.dimension_type = TextureDimensionType::Tex2D;
    texture_settings.format = render_context_settings.texture_format;
    texture_settings.frame_index_opt = frame_index;

    return texture_settings;
}
ARISENRHI_END_NAMESPACE
#pragma once
#include "IRenderPass.h"
#include "ITexture.h"
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE
struct Frame
{
    const uint32_t index = 0;
    Ptr<ITexture> screen_texture;
    Ptr<IRenderPass> screen_pass;

    Frame(uint32_t frame_index)
        :index(frame_index){}
};

ARISENRHI_END_NAMESPACE
#pragma once
#include "DataType.h"
#include "IObject.h"
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE
class TextureViewBase;

struct RenderPassSettings
{
    std::vector<TextureViewBase> attachments;
    FrameSize frame_size;
};

struct IRenderPass : IObject
{
    
};
ARISENRHI_END_NAMESPACE

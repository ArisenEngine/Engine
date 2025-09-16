#include "DataUtils.h"
ARISENRHI_BEGIN_NAMEPSACE

Rect GetRect(FrameSize frameSize)
{
    return Rect{{0.f, 0.f}, {frameSize.GetWidth(), frameSize.GetHeight()}};
}

ARISENRHI_END_NAMESPACE
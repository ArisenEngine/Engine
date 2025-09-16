#pragma once
#include <vector>
#include "DataType.h"

ARISENRHI_BEGIN_NAMEPSACE
struct ViewSettings
{
    std::vector<Rect> Viewports;
    std::vector<Rect> Scissors;
};

struct IViewState : IObject
{
};
ARISENRHI_END_NAMESPACE
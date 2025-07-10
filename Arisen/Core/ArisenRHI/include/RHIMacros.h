#pragma once
#include <memory>

#define ARISENRHI_BEGIN_NAMEPSACE \
    namespace ArisenRHI\
    {

#define ARISENRHI_END_NAMESPACE\
    }

#define AutoPtr(ClassName) std::shared_ptr<ClassName>
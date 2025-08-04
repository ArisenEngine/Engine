#pragma once
#include "Logger/Logger.h"

using ArisenEngine::Debugger::Logger;
namespace ArisenRHI{}
using namespace ArisenRHI;

#define ARISENRHI_D3D12_BEGIN_NAMEPSACE \
    namespace ArisenRHID3D12\
    {

#define ARISENRHI_D3D12_END_NAMESPACE\
    }

#define CHECK_D3D_HR(hr, Message)\
    do\
    {\
        if (FAILED(hr))\
        {\
            LOG_RHI_FATAL_AND_THROW(Message);\
        }\
    } while (0)

#pragma once
#include "Logger/Logger.h"

using ArisenEngine::Debugger::Logger;

#define CHECK_D3D_HR(hr, Message)\
    do\
    {\
        if (FAILED(hr))\
        {\
            LOG_RHI_FATAL_AND_THROW(Message);\
        }\
    } while (0)

#pragma once
#include "CoreHALCommon.h"
#include "String/String.h"

namespace ArisenEngine::HAL
{
    class HAL_DLL PlatformPath
    {
    public:
        static String GetExecutableDirectory();
    };
}

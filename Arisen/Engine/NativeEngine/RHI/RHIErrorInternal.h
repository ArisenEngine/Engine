#pragma once
#include "RHIErrorExports.h"

namespace ArisenEngine::RHI
{
    /**
     * @brief Set the last error for the current thread.
     * @param code The error code.
     * @param message Optional custom error message.
     */
    void SetLastError(RHI_ErrorCode code, const char* message = nullptr);
}

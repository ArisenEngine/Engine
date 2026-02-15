#pragma once
#include "../Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    /**
     * @brief RHI Error codes
     */
    enum class EErrorCode
    {
        None = 0,                        ///< No error
        OutOfMemory = 1,                 ///< Memory allocation failed
        InvalidHandle = 2,              ///< Invalid resource handle
        DeviceLost = 3,                 ///< GPU device was lost
        ValidationFailed = 4,           ///< Validation layer error
        InitializationFailed = 5,       ///< RHI initialization failed
        ShaderCompilationFailed = 6,    ///< Shader compilation error
        PipelineCreationFailed = 7,     ///< Pipeline creation error
        InvalidParameter = 8,           ///< Invalid function parameter
        UnsupportedFeature = 9,         ///< Feature not supported
        Unknown = 99                     ///< Unknown error
    };

    /**
     * @brief Get the last error code from the current thread.
     */
    RHI_DLL EErrorCode GetLastError();

    /**
     * @brief Get a human-readable message for the last error.
     */
    RHI_DLL const char* GetLastErrorMessage();

    /**
     * @brief Clear the current thread's error state.
     */
    RHI_DLL void ClearError();

    /**
     * @brief Set the last error for the current thread.
     * @internal This should only be called by RHI implementations.
     */
    RHI_DLL void SetLastError(EErrorCode code, const char* message = nullptr);

} // namespace ArisenEngine::RHI

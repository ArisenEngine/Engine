#pragma once
#include "EngineCommon.h"

/**
 * @file RHIErrorExports.h
 * @brief RHI Error Handling API
 * 
 * Thread-safe error handling for RHI operations.
 * Errors are stored per-thread using TLS.
 */

/// RHI Error codes
enum RHI_ErrorCode
{
    RHI_ERROR_NONE = 0,                     ///< No error
    RHI_ERROR_OUT_OF_MEMORY = 1,            ///< Memory allocation failed
    RHI_ERROR_INVALID_HANDLE = 2,           ///< Invalid resource handle
    RHI_ERROR_DEVICE_LOST = 3,              ///< GPU device was lost
    RHI_ERROR_VALIDATION_FAILED = 4,        ///< Validation layer error
    RHI_ERROR_INITIALIZATION_FAILED = 5,    ///< RHI initialization failed
    RHI_ERROR_SHADER_COMPILATION_FAILED = 6,///< Shader compilation error
    RHI_ERROR_PIPELINE_CREATION_FAILED = 7, ///< Pipeline creation error
    RHI_ERROR_INVALID_PARAMETER = 8,        ///< Invalid function parameter
    RHI_ERROR_UNSUPPORTED_FEATURE = 9,      ///< Feature not supported
    RHI_ERROR_UNKNOWN = 99                  ///< Unknown error
};

/**
 * @brief Get the last error code from the current thread.
 * @return The error code from the most recent RHI operation that failed.
 */
extern "C" ENGINE_DLL RHI_ErrorCode RHI_GetLastError();

/**
 * @brief Get a human-readable message for the last error.
 * @return Null-terminated string describing the error. 
 *         The string is valid until the next error occurs or RHI_ClearError is called.
 *         Returns empty string if no error.
 */
extern "C" ENGINE_DLL const char* RHI_GetLastErrorMessage();

/**
 * @brief Clear the current thread's error state.
 */
extern "C" ENGINE_DLL void RHI_ClearError();


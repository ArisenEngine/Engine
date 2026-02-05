#include "RHIErrorExports.h"
#include <thread>
#include <string>

namespace
{
    // Thread-local error storage
    thread_local RHI_ErrorCode s_LastError = RHI_ERROR_NONE;
    thread_local std::string s_LastErrorMessage;

    // Error code to message mapping
    const char* GetErrorString(RHI_ErrorCode code)
    {
        switch (code)
        {
            case RHI_ERROR_NONE:                      return "";
            case RHI_ERROR_OUT_OF_MEMORY:             return "Out of memory";
            case RHI_ERROR_INVALID_HANDLE:            return "Invalid resource handle";
            case RHI_ERROR_DEVICE_LOST:               return "GPU device was lost";
            case RHI_ERROR_VALIDATION_FAILED:         return "Validation layer error";
            case RHI_ERROR_INITIALIZATION_FAILED:     return "RHI initialization failed";
            case RHI_ERROR_SHADER_COMPILATION_FAILED: return "Shader compilation failed";
            case RHI_ERROR_PIPELINE_CREATION_FAILED:  return "Pipeline creation failed";
            case RHI_ERROR_INVALID_PARAMETER:         return "Invalid function parameter";
            case RHI_ERROR_UNSUPPORTED_FEATURE:       return "Feature not supported";
            case RHI_ERROR_UNKNOWN:                   return "Unknown error";
            default:                                  return "Unknown error code";
        }
    }
} // anonymous namespace

extern "C" ENGINE_DLL RHI_ErrorCode RHI_GetLastError()
{
    return s_LastError;
}

extern "C" ENGINE_DLL const char* RHI_GetLastErrorMessage()
{
    if (s_LastErrorMessage.empty() && s_LastError != RHI_ERROR_NONE)
    {
        return GetErrorString(s_LastError);
    }
    return s_LastErrorMessage.c_str();
}

extern "C" ENGINE_DLL void RHI_ClearError()
{
    s_LastError = RHI_ERROR_NONE;
    s_LastErrorMessage.clear();
}

// Internal function for other RHI modules to set errors
namespace ArisenEngine::RHI
{
    void SetLastError(RHI_ErrorCode code, const char* message)
    {
        s_LastError = code;
        if (message)
        {
            s_LastErrorMessage = message;
        }
        else
        {
            s_LastErrorMessage = GetErrorString(code);
        }
    }
} // namespace ArisenEngine::RHI


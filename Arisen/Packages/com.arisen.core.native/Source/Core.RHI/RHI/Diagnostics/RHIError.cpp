#include "RHIError.h"
#include "Base/FoundationMinimal.h"

namespace
{
    using namespace ArisenEngine::RHI;

    // Thread-local error storage
    thread_local EErrorCode s_LastError = EErrorCode::None;
    thread_local ArisenEngine::String s_LastErrorMessage;

    // Error code to message mapping
    const char* GetErrorString(EErrorCode code)
    {
        switch (code)
        {
        case EErrorCode::None: return "";
        case EErrorCode::OutOfMemory: return "Out of memory";
        case EErrorCode::InvalidHandle: return "Invalid resource handle";
        case EErrorCode::DeviceLost: return "GPU device was lost";
        case EErrorCode::ValidationFailed: return "Validation layer error";
        case EErrorCode::InitializationFailed: return "RHI initialization failed";
        case EErrorCode::ShaderCompilationFailed: return "Shader compilation failed";
        case EErrorCode::PipelineCreationFailed: return "Pipeline creation error";
        case EErrorCode::InvalidParameter: return "Invalid function parameter";
        case EErrorCode::UnsupportedFeature: return "Feature not supported";
        case EErrorCode::Unknown: return "Unknown error";
        default: return "Unknown error code";
        }
    }
} // anonymous namespace

namespace ArisenEngine::RHI
{
    EErrorCode GetLastError()
    {
        return s_LastError;
    }

    const char* GetLastErrorMessage()
    {
        if (s_LastErrorMessage.IsEmpty() && s_LastError != EErrorCode::None)
        {
            return GetErrorString(s_LastError);
        }
        return s_LastErrorMessage.c_str();
    }

    void ClearError()
    {
        s_LastError = EErrorCode::None;
        s_LastErrorMessage.Clear();
    }

    void SetLastError(EErrorCode code, const char* message)
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

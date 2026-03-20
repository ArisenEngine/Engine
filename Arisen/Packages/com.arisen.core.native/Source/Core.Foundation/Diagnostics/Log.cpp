#include "Log.h"
#include "../String/String.h"

namespace ArisenEngine::Diagnostics
{
    ILogHandler* Log::s_Handler = nullptr;

    void Log::SetHandler(ILogHandler* handler)
    {
        s_Handler = handler;
    }

    void Log::InternalLog(LogLevel level, const char* msg, const std::source_location& loc, const char* thread_name)
    {
        if (s_Handler)
        {
            LogSourceLocation location{
                loc.file_name(),
                loc.function_name(),
                loc.line()
            };

            switch (level)
            {
            case LogLevel::Trace: s_Handler->Trace(msg, location, thread_name);
                break;
            case LogLevel::Debug: s_Handler->Debug(msg, location, thread_name);
                break;
            case LogLevel::Info: s_Handler->Info(msg, location, thread_name);
                break;
            case LogLevel::Warning: s_Handler->Warning(msg, location, thread_name);
                break;
            case LogLevel::Error: s_Handler->Error(msg, location, thread_name);
                break;
            case LogLevel::Fatal: s_Handler->Fatal(msg, location, thread_name);
                break;
            default: s_Handler->Log(level, msg, location, thread_name);
                break;
            }
        }
    }
}

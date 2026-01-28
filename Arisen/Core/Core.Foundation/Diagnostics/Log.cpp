#include "Log.h"
#include "../String/String.h"

namespace ArisenEngine
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
            s_Handler->Log(level, msg, location, thread_name);
        }
    }
}

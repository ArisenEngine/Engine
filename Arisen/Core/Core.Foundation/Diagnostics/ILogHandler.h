#pragma once
#include <cstdint>

namespace ArisenEngine
{
    enum class LogLevel : uint8_t
    {
        Trace = 0x01,
        Debug = 0x02,
        Info = 0x04,
        Warning = 0x08,
        Error = 0x10,
        Fatal = 0x20
    };

    struct LogSourceLocation
    {
        const char* file;
        const char* function;
        uint32_t line;
    };

    class ILogHandler
    {
    public:
        virtual ~ILogHandler() = default;
        virtual void Log(LogLevel level, const char* msg, const LogSourceLocation& location, const char* thread_name = nullptr) = 0;
    };
}

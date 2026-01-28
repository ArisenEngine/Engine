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

        // Base log method
        virtual void Log(LogLevel level, const char* msg, const LogSourceLocation& location, const char* thread_name = nullptr) = 0;

        // Specialized virtual methods with default implementations
        virtual void Trace(const char* msg, const LogSourceLocation& loc, const char* thread)   { Log(LogLevel::Trace, msg, loc, thread); }
        virtual void Debug(const char* msg, const LogSourceLocation& loc, const char* thread)   { Log(LogLevel::Debug, msg, loc, thread); }
        virtual void Info(const char* msg, const LogSourceLocation& loc, const char* thread)    { Log(LogLevel::Info, msg, loc, thread); }
        virtual void Warning(const char* msg, const LogSourceLocation& loc, const char* thread) { Log(LogLevel::Warning, msg, loc, thread); }
        virtual void Error(const char* msg, const LogSourceLocation& loc, const char* thread)   { Log(LogLevel::Error, msg, loc, thread); }
        virtual void Fatal(const char* msg, const LogSourceLocation& loc, const char* thread)   { Log(LogLevel::Fatal, msg, loc, thread); }
    };
}

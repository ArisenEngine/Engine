#pragma once

#include "../Base/BasicMacros.h"
#include "../Base/StandardHeaders.h"
#include "../String/String.h"
#include "ILogHandler.h"

namespace ArisenEngine::Diagnostics
{
    class FOUNDATION_DLL Log
    {
    public:
        static void SetHandler(ILogHandler* handler);
        static ILogHandler* GetHandler() { return s_Handler; }

        // Generic log methods (support const char*, std::string, etc.)
        template <typename T>
        static void Trace(const T& msg, std::source_location loc = std::source_location::current(),
                          const char* thread_name = nullptr)
        {
            InternalLogTyped(LogLevel::Trace, msg, loc, thread_name);
        }

        template <typename T>
        static void Debug(const T& msg, std::source_location loc = std::source_location::current(),
                          const char* thread_name = nullptr)
        {
            InternalLogTyped(LogLevel::Debug, msg, loc, thread_name);
        }

        template <typename T>
        static void Info(const T& msg, std::source_location loc = std::source_location::current(),
                         const char* thread_name = nullptr)
        {
            InternalLogTyped(LogLevel::Info, msg, loc, thread_name);
        }

        template <typename T>
        static void Warning(const T& msg, std::source_location loc = std::source_location::current(),
                            const char* thread_name = nullptr)
        {
            InternalLogTyped(LogLevel::Warning, msg, loc, thread_name);
        }

        template <typename T>
        static void Error(const T& msg, std::source_location loc = std::source_location::current(),
                          const char* thread_name = nullptr)
        {
            InternalLogTyped(LogLevel::Error, msg, loc, thread_name);
        }

        template <typename T>
        static void Fatal(const T& msg, std::source_location loc = std::source_location::current(),
                          const char* thread_name = nullptr)
        {
            InternalLogTyped(LogLevel::Fatal, msg, loc, thread_name);
        }

        // Formatted log methods
        template <typename... Args>
        static void TraceF(std::format_string<Args...> fmt, Args&&... args)
        {
            LogFormat(LogLevel::Trace, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

        template <typename... Args>
        static void DebugF(std::format_string<Args...> fmt, Args&&... args)
        {
            LogFormat(LogLevel::Debug, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

        template <typename... Args>
        static void InfoF(std::format_string<Args...> fmt, Args&&... args)
        {
            LogFormat(LogLevel::Info, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

        template <typename... Args>
        static void WarningF(std::format_string<Args...> fmt, Args&&... args)
        {
            LogFormat(LogLevel::Warning, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

        template <typename... Args>
        static void ErrorF(std::format_string<Args...> fmt, Args&&... args)
        {
            LogFormat(LogLevel::Error, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

        template <typename... Args>
        static void FatalF(std::format_string<Args...> fmt, Args&&... args)
        {
            LogFormat(LogLevel::Fatal, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

    private:
        template <typename T>
        static void InternalLogTyped(ArisenEngine::LogLevel level, const T& msg, const std::source_location& loc,
                                     const char* thread_name)
        {
            if constexpr (std::is_convertible_v<T, const char*>)
            {
                InternalLog(level, static_cast<const char*>(msg), loc, thread_name);
            }
            else if constexpr (requires { msg.c_str(); })
            {
                InternalLog(level, msg.c_str(), loc, thread_name);
            }
            else
            {
                ArisenEngine::String s = std::format("{}", msg);
                InternalLog(level, s.c_str(), loc, thread_name);
            }
        }

        template <typename... Args>
        static void LogFormat(ArisenEngine::LogLevel level, auto fmt, std::source_location loc, Args&&... args)
        {
            ArisenEngine::String msg = std::format(fmt, std::forward<Args>(args)...);
            InternalLog(level, msg.c_str(), loc);
        }

        static void InternalLog(LogLevel level, const char* msg, const std::source_location& loc,
                                const char* thread_name = nullptr);

        static ILogHandler* s_Handler;
    };
}

// Basic logging macros
#define LOG_TRACE(msg) ArisenEngine::Diagnostics::Log::Trace(msg)
#define LOG_DEBUG(msg) ArisenEngine::Diagnostics::Log::Debug(msg)
#define LOG_INFO(msg)  ArisenEngine::Diagnostics::Log::Info(msg)
#define LOG_WARN(msg)  ArisenEngine::Diagnostics::Log::Warning(msg)
#define LOG_ERROR(msg) ArisenEngine::Diagnostics::Log::Error(msg)
#define LOG_FATAL(msg) ArisenEngine::Diagnostics::Log::Fatal(msg)

// Formatted logging macros
#define LOG_TRACEF(fmt, ...) ArisenEngine::Diagnostics::Log::TraceF(fmt, __VA_ARGS__)
#define LOG_DEBUGF(fmt, ...) ArisenEngine::Diagnostics::Log::DebugF(fmt, __VA_ARGS__)
#define LOG_INFOF(fmt, ...)  ArisenEngine::Diagnostics::Log::InfoF(fmt, __VA_ARGS__)
#define LOG_WARNF(fmt, ...)  ArisenEngine::Diagnostics::Log::WarningF(fmt, __VA_ARGS__)
#define LOG_ERRORF(fmt, ...) ArisenEngine::Diagnostics::Log::ErrorF(fmt, __VA_ARGS__)
#define LOG_FATALF(fmt, ...) ArisenEngine::Diagnostics::Log::FatalF(fmt, __VA_ARGS__)

#define LOG_FATAL_AND_THROW(msg)           \
    do                                     \
    {                                      \
        ArisenEngine::Diagnostics::Log::Fatal(msg);      \
        throw std::runtime_error(msg);     \
    } while (0)

#define LOG_ERROR_AND_THROW(msg)           \
    do                                     \
    {                                      \
        ArisenEngine::Diagnostics::Log::Error(msg);      \
        throw std::runtime_error(msg);     \
    } while (0)

#define LOG_FATAL_AND_THROW_F(fmt, ...)                            \
    do                                                             \
    {                                                              \
        String _msg = String::Format(fmt, __VA_ARGS__);            \
        ArisenEngine::Diagnostics::Log::Fatal(_msg.c_str());       \
        throw std::runtime_error(_msg.c_str());                    \
    } while (0)

#define LOG_ERROR_AND_THROW_F(fmt, ...)                            \
    do                                                             \
    {                                                              \
        String _msg = String::Format(fmt, __VA_ARGS__);            \
        ArisenEngine::Diagnostics::Log::Error(_msg.c_str());       \
        throw std::runtime_error(_msg.c_str());                    \
    } while (0)

/**
 * @brief Specialization for std::format support of engine String.
 */
template <>
struct std::formatter<ArisenEngine::String> : std::formatter<std::string_view>
{
    auto format(const ArisenEngine::String& s, format_context& ctx) const
    {
        return std::formatter<std::string_view>::format(static_cast<std::string_view>(s), ctx);
    }
};

#pragma once

#include "../Base/BasicMacros.h"
#include "../Base/StandardHeaders.h"
#include "ILogHandler.h"

namespace ArisenEngine
{
    class FOUNDATION_DLL Log
    {
    public:
        static void SetHandler(ILogHandler* handler);
        static ILogHandler* GetHandler() { return s_Handler; }

        // Generic log methods (support const char*, std::string, etc.)
        template<typename T>
        static void Trace(const T& msg, std::source_location loc = std::source_location::current(), const char* thread_name = nullptr) {
            InternalLogTyped(LogLevel::Trace, msg, loc, thread_name);
        }

        template<typename T>
        static void Debug(const T& msg, std::source_location loc = std::source_location::current(), const char* thread_name = nullptr) {
            InternalLogTyped(LogLevel::Debug, msg, loc, thread_name);
        }

        template<typename T>
        static void Info(const T& msg, std::source_location loc = std::source_location::current(), const char* thread_name = nullptr) {
            InternalLogTyped(LogLevel::Info, msg, loc, thread_name);
        }

        template<typename T>
        static void Warning(const T& msg, std::source_location loc = std::source_location::current(), const char* thread_name = nullptr) {
            InternalLogTyped(LogLevel::Warning, msg, loc, thread_name);
        }

        template<typename T>
        static void Error(const T& msg, std::source_location loc = std::source_location::current(), const char* thread_name = nullptr) {
            InternalLogTyped(LogLevel::Error, msg, loc, thread_name);
        }

        template<typename T>
        static void Fatal(const T& msg, std::source_location loc = std::source_location::current(), const char* thread_name = nullptr) {
            InternalLogTyped(LogLevel::Fatal, msg, loc, thread_name);
        }

        // Formatted log methods
        template<typename... Args>
        static void TraceF(std::format_string<Args...> fmt, Args&&... args) {
            LogFormat(LogLevel::Trace, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

        template<typename... Args>
        static void DebugF(std::format_string<Args...> fmt, Args&&... args) {
            LogFormat(LogLevel::Debug, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

        template<typename... Args>
        static void InfoF(std::format_string<Args...> fmt, Args&&... args) {
            LogFormat(LogLevel::Info, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

        template<typename... Args>
        static void WarningF(std::format_string<Args...> fmt, Args&&... args) {
            LogFormat(LogLevel::Warning, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

        template<typename... Args>
        static void ErrorF(std::format_string<Args...> fmt, Args&&... args) {
            LogFormat(LogLevel::Error, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

        template<typename... Args>
        static void FatalF(std::format_string<Args...> fmt, Args&&... args) {
            LogFormat(LogLevel::Fatal, fmt, std::source_location::current(), std::forward<Args>(args)...);
        }

    private:
        template<typename T>
        static void InternalLogTyped(LogLevel level, const T& msg, const std::source_location& loc, const char* thread_name) {
            if constexpr (std::is_convertible_v<T, const char*>) {
                InternalLog(level, static_cast<const char*>(msg), loc, thread_name);
            } else if constexpr (requires { msg.c_str(); }) {
                InternalLog(level, msg.c_str(), loc, thread_name);
            } else {
                std::string s = std::format("{}", msg);
                InternalLog(level, s.c_str(), loc, thread_name);
            }
        }

        template<typename... Args>
        static void LogFormat(LogLevel level, std::format_string<Args...> fmt, std::source_location loc, Args&&... args) {
            std::string msg = std::format(fmt, std::forward<Args>(args)...);
            InternalLog(level, msg.c_str(), loc);
        }

        static void InternalLog(LogLevel level, const char* msg, const std::source_location& loc, const char* thread_name = nullptr);

        static ILogHandler* s_Handler;
    };
}

// Basic logging macros
#define LOG_TRACE(msg) ArisenEngine::Log::Trace(msg)
#define LOG_DEBUG(msg) ArisenEngine::Log::Debug(msg)
#define LOG_INFO(msg)  ArisenEngine::Log::Info(msg)
#define LOG_WARN(msg)  ArisenEngine::Log::Warning(msg)
#define LOG_ERROR(msg) ArisenEngine::Log::Error(msg)
#define LOG_FATAL(msg) ArisenEngine::Log::Fatal(msg)

// Formatted logging macros
#define LOG_TRACEF(fmt, ...) ArisenEngine::Log::TraceF(fmt, __VA_ARGS__)
#define LOG_DEBUGF(fmt, ...) ArisenEngine::Log::DebugF(fmt, __VA_ARGS__)
#define LOG_INFOF(fmt, ...)  ArisenEngine::Log::InfoF(fmt, __VA_ARGS__)
#define LOG_WARNF(fmt, ...)  ArisenEngine::Log::WarningF(fmt, __VA_ARGS__)
#define LOG_ERRORF(fmt, ...) ArisenEngine::Log::ErrorF(fmt, __VA_ARGS__)
#define LOG_FATALF(fmt, ...) ArisenEngine::Log::FatalF(fmt, __VA_ARGS__)

#define LOG_FATAL_AND_THROW(msg)           \
    do                                     \
    {                                      \
        ArisenEngine::Log::Fatal(msg);      \
        throw std::runtime_error(msg);     \
    } while (0)

#define LOG_ERROR_AND_THROW(msg)           \
    do                                     \
    {                                      \
        ArisenEngine::Log::Error(msg);      \
        throw std::runtime_error(msg);     \
    } while (0)

#define LOG_FATAL_AND_THROW_F(fmt, ...)                            \
    do                                                             \
    {                                                              \
        std::string _msg = std::format(fmt, __VA_ARGS__);           \
        ArisenEngine::Log::Fatal(_msg.c_str());                    \
        throw std::runtime_error(_msg);                            \
    } while (0)

#define LOG_ERROR_AND_THROW_F(fmt, ...)                            \
    do                                                             \
    {                                                              \
        std::string _msg = std::format(fmt, __VA_ARGS__);           \
        ArisenEngine::Log::Error(_msg.c_str());                    \
        throw std::runtime_error(_msg);                            \
    } while (0)

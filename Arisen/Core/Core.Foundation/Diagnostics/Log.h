#pragma once
#include "ILogHandler.h"
#include "CoreFoundationCommon.h"
#include <cstdlib>
#include <stdexcept>
#include <string>

namespace ArisenEngine::Infra { class String; }

namespace ArisenEngine
{
    class FOUNDATION_DLL Log
    {
    public:
        static void SetHandler(ILogHandler* handler);
        static ILogHandler* GetHandler() { return s_Handler; }

        static void Trace(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Debug(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Info(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Warning(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Error(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Fatal(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);

        // String overloads
        static void Trace(const ArisenEngine::Infra::String& msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Debug(const ArisenEngine::Infra::String& msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Info(const ArisenEngine::Infra::String& msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Warning(const ArisenEngine::Infra::String& msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Error(const ArisenEngine::Infra::String& msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Fatal(const ArisenEngine::Infra::String& msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);

    private:
        static ILogHandler* s_Handler;
    };
}

#define LOG_TRACE(msg) ArisenEngine::Log::Trace(msg)
#define LOG_DEBUG(msg) ArisenEngine::Log::Debug(msg)
#define LOG_INFO(msg)  ArisenEngine::Log::Info(msg)
#define LOG_WARN(msg)  ArisenEngine::Log::Warning(msg)
#define LOG_ERROR(msg) ArisenEngine::Log::Error(msg)
#define LOG_FATAL(msg) ArisenEngine::Log::Fatal(msg)

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

#ifndef EXECUTE_CODE
#define EXECUTE_CODE(code) do { code; } while (0)
#endif

#ifndef DEBUG_OP
#ifdef _DEBUG
#define DEBUG_OP(x) x
#else
#define DEBUG_OP(x) ((void)0)
#endif
#endif

#undef assert
#define assert(condition)                                                   \
do {                                                                    \
    if (!(condition)) {                                                 \
        ArisenEngine::Log::Fatal("Assertion failed: (" #condition ")"); \
        std::abort();                                                   \
    }                                                                   \
} while (0)

#ifndef ASSERT
#define ASSERT(x) DEBUG_OP(assert(x))
#endif

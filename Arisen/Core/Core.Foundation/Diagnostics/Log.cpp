#include "Log.h"
#include "../String/String.h"

namespace ArisenEngine
{
    ILogHandler* Log::s_Handler = nullptr;

    void Log::SetHandler(ILogHandler* handler)
    {
        s_Handler = handler;
    }

    void Log::Trace(const char* msg, const char* thread_name, const char* cs_trace)
    {
        if (s_Handler) s_Handler->Log(LogLevel::Trace, msg, thread_name, cs_trace);
    }

    void Log::Debug(const char* msg, const char* thread_name, const char* cs_trace)
    {
        if (s_Handler) s_Handler->Log(LogLevel::Debug, msg, thread_name, cs_trace);
    }

    void Log::Info(const char* msg, const char* thread_name, const char* cs_trace)
    {
        if (s_Handler) s_Handler->Log(LogLevel::Info, msg, thread_name, cs_trace);
    }

    void Log::Warning(const char* msg, const char* thread_name, const char* cs_trace)
    {
        if (s_Handler) s_Handler->Log(LogLevel::Warning, msg, thread_name, cs_trace);
    }

    void Log::Error(const char* msg, const char* thread_name, const char* cs_trace)
    {
        if (s_Handler) s_Handler->Log(LogLevel::Error, msg, thread_name, cs_trace);
    }

    void Log::Fatal(const char* msg, const char* thread_name, const char* cs_trace)
    {
        if (s_Handler) s_Handler->Log(LogLevel::Fatal, msg, thread_name, cs_trace);
    }

    void Log::Trace(const Infra::String& msg, const char* thread_name, const char* cs_trace) { Trace(msg.c_str(), thread_name, cs_trace); }
    void Log::Debug(const Infra::String& msg, const char* thread_name, const char* cs_trace) { Debug(msg.c_str(), thread_name, cs_trace); }
    void Log::Info(const Infra::String& msg, const char* thread_name, const char* cs_trace) { Info(msg.c_str(), thread_name, cs_trace); }
    void Log::Warning(const Infra::String& msg, const char* thread_name, const char* cs_trace) { Warning(msg.c_str(), thread_name, cs_trace); }
    void Log::Error(const Infra::String& msg, const char* thread_name, const char* cs_trace) { Error(msg.c_str(), thread_name, cs_trace); }
    void Log::Fatal(const Infra::String& msg, const char* thread_name, const char* cs_trace) { Fatal(msg.c_str(), thread_name, cs_trace); }
}

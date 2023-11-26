#pragma once
#include"../Common/CommandHeaders.h"
namespace NebulaEngine::Debugger
{
    using LogCallback = void(*)(u32, const wchar_t*, const wchar_t*);

    class Logger final
    {
    public:
        Logger() = delete;
        
        enum class LogLevel: NebulaEngine::u8
        {
         
            Trace = 0x01, // finer-grained info for debugging
            Log = 0x02,   // fine-grained info

            
            Info = 0x04, // coarse-grained info
            Warning = 0x08, // harmful situation info
            Error = 0x10, // errors but app can still run
            Fatal = 0x20 // severe errors that will lead to abort
            
        };

        static void Log(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Info(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Warning(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Error(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Fatal(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void Trace(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        static void SetServerityLevel(LogLevel level);
  
        static void BindCallback(LogCallback callback);

        static bool m_IsInitialize;
        static LogCallback m_LogCallback;
        static void Exit();
        static bool Initialize();
    private:
        
        static void StackTrace(std::string* stack_info);

        static void Warning_Threaded(const std::string* msg, const std::string* thread_name, const std::string* invoker_thread_id, const std::string* cs_trace);
    };
}

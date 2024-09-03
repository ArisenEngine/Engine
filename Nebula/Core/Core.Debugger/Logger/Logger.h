#pragma once
#include "Common/CommandHeaders.h"
#include"../DllHeader.h"

#pragma warning(disable : 4996)

namespace NebulaEngine::Debugger
{
    using LogCallback = void(*)(u32, const char*, const char*, const char*);

    class DEBUGGER_DLL Logger final
    {
    public:
        NO_COPY_NO_MOVE(Logger)
        NO_COMPARE(Logger)

        enum class DEBUGGER_DLL LogLevel: NebulaEngine::u8
        {
            Trace = 0x01,
            // finer-grained info for debugging
            Log = 0x02,
            // fine-grained info
            Info = 0x04,
            // coarse-grained info
            Warning = 0x08,
            // harmful situation info
            Error = 0x10,
            // errors but app can still run
            Fatal = 0x20 // severe errors that will lead to abort
        };

        void Log(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        void Info(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        void Warning(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        void Error(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        void Fatal(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);
        void Trace(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);

        void Log(const std::string&& msg);
        void Info(const std::string&& msg);
        void Warning(const std::string&& msg);
        void Error(const std::string&& msg);
        void Fatal(const std::string&& msg);
        void Trace(const std::string&& msg);

        void Log(const std::wstring&& msg);
        void Info(const std::wstring&& msg);
        void Warning(const std::wstring&& msg);
        void Error(const std::wstring&& msg);
        void Fatal(const std::wstring&& msg);
        void Trace(const std::wstring&& msg);


        void SetServerityLevel(LogLevel level);
        void BindCallback(LogCallback callback);
        bool Initialize();

        static Logger& GetInstance();
        

        static void Shutdown();
        

    private:
        bool m_IsInitialize;
        LogCallback m_LogCallback;
        Logger();
    };
}

#define LOG_INFO(msg) NebulaEngine::Debugger::Logger::GetInstance().Info(msg);
// TODO: support formated log
// #define LOG_INFO_FORMAT(format,  ...) NebulaEngine::Debugger::Logger::GetInstance().Info(msg);
#define LOG_DEBUG(msg) DEBUG_OP(NebulaEngine::Debugger::Logger::GetInstance().Log(msg);)
#define LOG_WARN(msg) NebulaEngine::Debugger::Logger::GetInstance().Warning(msg);
#define LOG_ERROR(msg) NebulaEngine::Debugger::Logger::GetInstance().Error(msg);
#define LOG_FATAL(msg) NebulaEngine::Debugger::Logger::GetInstance().Fatal(msg);
#define LOG_FATAL_AND_THROW(msg) NebulaEngine::Debugger::Logger::GetInstance().Fatal(msg); \
                                 throw std::runtime_error(msg);
#define LOG_TRACE(msg) NebulaEngine::Debugger::Logger::GetInstance().Trace(msg);

#define ASSERT(x) DEBUG_OP(assert(x);)

#pragma once
#include "Common/CommandHeaders.h"
#include"../CoreDebuggerCommon.h"

#pragma warning(disable : 4996)

namespace ArisenEngine::Debugger
{
    using LogCallback = void(*)(UInt32, const char*, const char*, const char*);

    class DEBUGGER_DLL Logger final
    {
    public:
        NO_COPY_NO_MOVE(Logger)
        NO_COMPARE(Logger)

        enum class LogLevel: ArisenEngine::UInt8
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

        void Log(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr); // CS_IGNORE_GEN
        void Info(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);// CS_IGNORE_GEN
        void Warning(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);// CS_IGNORE_GEN
        void Error(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);// CS_IGNORE_GEN
        void Fatal(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);// CS_IGNORE_GEN
        void Trace(const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr);// CS_IGNORE_GEN

        
        void Log(const std::string&& msg);
        void Info(const std::string&& msg);
        void Warning(const std::string&& msg);
        void Error(const std::string&& msg);
        void Fatal(const std::string&& msg, bool needThrow = false);
        void Trace(const std::string&& msg);
        
        void Log(const std::wstring&& msg);
        void Info(const std::wstring&& msg);
        void Warning(const std::wstring&& msg);
        void Error(const std::wstring&& msg);
        void Fatal(const std::wstring&& msg, bool needThrow = false);
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



#define LOG_INFO(msg) EXECUTE_CODE(ArisenEngine::Debugger::Logger::GetInstance().Info(std::move(msg));)
// TODO: support formated log
// #define LOG_INFO_FORMAT(format,  ...) ArisenEngine::Debugger::Logger::GetInstance().Info(msg);
#define LOG_DEBUG(msg) EXECUTE_CODE(DEBUG_OP(ArisenEngine::Debugger::Logger::GetInstance().Log(std::move(msg));))
#define LOG_WARN(msg) EXECUTE_CODE(ArisenEngine::Debugger::Logger::GetInstance().Warning(std::move(msg));)
#define LOG_ERROR(msg) EXECUTE_CODE(ArisenEngine::Debugger::Logger::GetInstance().Error(std::move(msg));)
#define LOG_FATAL(msg) EXECUTE_CODE(ArisenEngine::Debugger::Logger::GetInstance().Fatal(std::move(msg));)
#define LOG_FATAL_AND_THROW(msg) EXECUTE_CODE(ArisenEngine::Debugger::Logger::GetInstance().Fatal(std::move(msg), true);)
#define LOG_TRACE(msg) EXECUTE_CODE(ArisenEngine::Debugger::Logger::GetInstance().Trace(std::move(msg));)

#include <cstdlib> // for abort()

#undef assert // 确保不会使用默认的 assert 宏
#define assert(condition)                                                   \
do {                                                                    \
    if (!(condition)) {                                                 \
        std::string msg = "Assertion failed: (" #condition "), file " \
            + std::string(__FILE__) +                \
            ", line " + std::to_string(__LINE__);    \
        LOG_FATAL(msg)                           \
        std::abort();                                                   \
    }                                                                   \
} while (0);                    


#define ASSERT(x) DEBUG_OP(assert(x);)

extern "C" DEBUGGER_DLL int dummy_function();
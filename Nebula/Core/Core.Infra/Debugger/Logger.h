#pragma once


#include"../Common/CommandHeaders.h"


namespace NebulaEngine::Debugger
{
    class Logger final
    {
    public:
        Logger() = delete;

        /**
         * \brief
        * enum severity_level
         {
            trace          = 0,
            debug          = 1,
            info           = 2,
            warning        = 3,
            error          = 4,
            fatal          = 5
         };
         */
        enum class LogLevel: NebulaEngine::u8
        {
         
            Trace = 0x01, // finer-grained info for debugging
            Log = 0x02,   // fine-grained info

            
            Info = 0x04, // coarse-grained info
            Warning = 0x08, // harmful situation info
            Error = 0x10, // errors but app can still run
            Fatal = 0x20 // severe errors that will lead to abort
            
        };

        static void Log(const wchar_t* msg, const char* thread_name = nullptr);
        static void Info(const wchar_t* msg, const char* thread_name = nullptr);
        static void Warning(const wchar_t* msg, const char* thread_name = nullptr);
        static void Error(const wchar_t* msg, const char* thread_name = nullptr);
        static void Fatal(const wchar_t* msg, const char* thread_name = nullptr);
        static void Trace(const wchar_t* msg, const char* thread_name = nullptr);
        static void SetServerityLevel(LogLevel level);
  
      
        static bool m_IsInitialize;
        
    private:
        static void Initialize();
        static void StackTrace(std::string& stack_info);
    };
}

#pragma once
#include <iostream>
#include<boost/log/trivial.hpp>
#include<boost/log/utility/setup/file.hpp>
#include<boost/log/expressions.hpp>

#include"./Common/CommandHeaders.h"


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
            Log = 0x01,
            Info = 0x02,
            Warning = 0x04,
            Error = 0x08,
            Fatal = 0x10,
            Trace = 0x20
        };

        static void Log(const wchar_t* msg);
        static void Info();
        static void Warning();
        static void Error();
        static void Fatal();
        static void Trace();
        static void SetServerityLevel(LogLevel level);
  
      
        static bool m_IsInitialize;
        
    private:
        static void Initialize();
       /* static void Log(
            NebulaEngine::u8 level,
            const wchar_t* msg,
            const wchar_t* file,
            const wchar_t* caller,
            NebulaEngine::u32 lines,
            const wchar_t* threadName);*/
    };
}

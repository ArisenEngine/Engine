#include "Logger.h"


using namespace NebulaEngine::Debugger;
namespace logging = boost::log;

bool Logger::m_IsInitialize = false;

void Logger::Initialize()
{
    if (m_IsInitialize) return;

    logging::add_file_log(L"Debugger.log");

    Logger::SetServerityLevel(LogLevel::Info);
}

void Logger::SetServerityLevel(LogLevel level)
{
    logging::trivial::severity_level boost_level;
    
    switch (level)
    {
    case LogLevel::Error:
        boost_level = logging::trivial::severity_level::error;
        break;
    case LogLevel::Fatal:
        boost_level = logging::trivial::severity_level::fatal;
        break;
    case LogLevel::Info:
        boost_level = logging::trivial::severity_level::info;
        break;
    case LogLevel::Log:
        boost_level = logging::trivial::severity_level::debug;
        break;
    case LogLevel::Trace:
        boost_level = logging::trivial::severity_level::trace;
        break;
    case LogLevel::Warning:
        boost_level = logging::trivial::severity_level::warning;
        break;
    }

    logging::core::get()->set_filter
    (
        logging::trivial::severity >= boost_level
    );
}


//static void Log(
//    NebulaEngine::u8 level,
//            const wchar_t* msg,
//            const wchar_t* file,
//            const wchar_t* caller,
//            NebulaEngine::u32 lines,
//            const wchar_t* threadName)
//{
//    BOOST_LOG_TRIVIAL(trace) << msg;
//}
//
//void Logger::Log(const wchar_t* msg)
//{
//    
//    Initialize();
//    Log((u8)LogLevel::Log, msg, nullptr, nullptr, 0, nullptr);
//    
//}

#include "Logger.h"


using namespace NebulaEngine::Debugger;
namespace logging = boost::log;
namespace keywords = boost::log::keywords;

bool Logger::m_IsInitialize = false;

void Logger::Initialize()
{
    if (m_IsInitialize) return;

    logging::add_file_log(
        keywords::file_name = "Debugger.log"
    );

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


void Logger::Log(const wchar_t* msg)
{
    
    Initialize();
    BOOST_LOG_TRIVIAL(fatal) << msg;
}

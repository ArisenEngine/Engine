#include <spdlog/spdlog.h>
#include <spdlog/async.h>
#include <spdlog/cfg/env.h>
#include <spdlog/fmt/ostr.h>
#include <spdlog/sinks/basic_file_sink.h>
#include <filesystem>
#include <stdexcept>
#include <iostream>
#include <thread>
#include <sstream>

#if defined(__has_include) && __has_include(<stacktrace>) && __cpp_lib_stacktrace >= 202011
    #define HAS_STD_STACKTRACE 1
    #include <stacktrace>
#else
    #define HAS_STD_STACKTRACE 0
#endif

#include "Logger.h"
#include "../../Core.Foundation/Diagnostics/Log.h"

using namespace ArisenEngine::Diagnostics;

inline std::string GetStacktrace()
{
#if HAS_STD_STACKTRACE
    std::stringstream trace_stream;
    auto trace = std::stacktrace::current();
    for (size_t i = 1; i < trace.size(); ++i) { 
        const auto& entry = trace[i];
        if (!entry.description().empty() || !entry.source_file().empty()) {
            trace_stream << i << "> " << entry.source_file() << "(" << entry.source_line() << "): " << entry.description() << "\n";
        }
    }
    return trace_stream.str();
#else
    return "[stacktrace not available]";
#endif
}

Logger::Logger(): m_IsInitialize(false), m_LogCallback(nullptr)
{
}

#ifdef _WIN32
#include <windows.h>
#endif

bool Logger::Initialize()
{
	if (m_IsInitialize) return true;

	try
	{
        std::filesystem::path log_dir;
#ifdef _WIN32
        wchar_t exePathW[MAX_PATH]{};
        GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
        log_dir = std::filesystem::path(exePathW).parent_path() / "logs";
#else
        log_dir = std::filesystem::absolute(std::filesystem::path("logs"));
#endif
        std::error_code _ec;
        std::filesystem::create_directories(log_dir, _ec);

        const auto log_file = (log_dir / "log.log").string();

		constexpr size_t queue_size = 8192;
		constexpr size_t num_threads = 1;  
        spdlog::init_thread_pool(queue_size, num_threads);

        auto async_file = spdlog::basic_logger_mt<spdlog::async_factory>("log", log_file, true);
		spdlog::set_default_logger(async_file);

		spdlog::flush_every(std::chrono::seconds(3));
		spdlog::flush_on(spdlog::level::info);
		
#if _DEBUG
		spdlog::set_level(spdlog::level::trace);
#else
		spdlog::set_level(spdlog::level::info);
#endif
		
		spdlog::set_pattern("[%Y-%m-%d %T.%e][process %p][thread %t][%l] %v");

        // Register with Foundation Bridge
        ArisenEngine::Log::SetHandler(this);
	}
	catch (const spdlog::spdlog_ex &ex)	
	{
		std::printf("Log initialization failed: %s\n", ex.what());
		return false;
	}
	
	m_IsInitialize = true;
	return true;
}

Logger& Logger::GetInstance()
{
	static Logger _log_instnace;
	return _log_instnace;
}

void Logger::Shutdown()
{
    ArisenEngine::Log::SetHandler(nullptr);
    if (auto* logger = spdlog::default_logger_raw())
    {
        logger->flush();
    }
	spdlog::shutdown();
	GetInstance().m_IsInitialize = false;
}

void Logger::SetServerityLevel(LogLevel level)
{
	switch (level)
	{
	case LogLevel::Error:
		spdlog::set_level(spdlog::level::err);
		break;
	case LogLevel::Fatal:
		spdlog::set_level(spdlog::level::critical);
		break;
	case LogLevel::Info:
		spdlog::set_level(spdlog::level::info);
		break;
	case LogLevel::Debug:
		spdlog::set_level(spdlog::level::debug);
		break;
	case LogLevel::Trace:
		spdlog::set_level(spdlog::level::trace);
		break;
	case LogLevel::Warning:
		spdlog::set_level(spdlog::level::warn);
		break;
	}
}

void Logger::BindCallback(LogCallback callback)
{
	m_LogCallback = callback;
}

void Logger::Log(LogLevel level, const char* msg, const char* thread_name, const char* cs_trace)
{
    spdlog::level::level_enum spd_level;
    bool include_trace = true;

    switch (level)
    {
    case LogLevel::Trace: spd_level = spdlog::level::trace; break;
    case LogLevel::Debug: spd_level = spdlog::level::debug; include_trace = false; break;
    case LogLevel::Info:  spd_level = spdlog::level::info;  include_trace = false; break;
    case LogLevel::Warning: spd_level = spdlog::level::warn; break;
    case LogLevel::Error: spd_level = spdlog::level::err; break;
    case LogLevel::Fatal: spd_level = spdlog::level::critical; break;
    default: spd_level = spdlog::level::info; break;
    }

    std::string msg_str = msg ? msg : "";
    std::string trace_str = GetStacktrace() + (cs_trace ? cs_trace : "");
    std::string content = include_trace ? (msg_str + "\n" + trace_str + "\n") : msg_str;

    spdlog::default_logger()->log(spd_level, content);

    if (m_LogCallback)
    {
        std::stringstream ss;
        ss << std::this_thread::get_id();
        m_LogCallback(static_cast<UInt32>(level), ss.str().c_str(), msg_str.c_str(), trace_str.c_str());
    }
}
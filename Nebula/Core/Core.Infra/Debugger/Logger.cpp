#include <spdlog/spdlog.h>
#include <spdlog/async.h>
#include <spdlog/cfg/env.h>   // support for loading levels from the environment variable
#include <spdlog/fmt/ostr.h> // support for user defined types
#include <spdlog/sinks/basic_file_sink.h>
#include <stacktrace>
#include "Logger.h"


using namespace NebulaEngine::Debugger;

bool Logger::m_IsInitialize = false;
LogCallback Logger::m_LogCallback = nullptr;

const wchar_t* char_to_wchar(const char* c)
{
	const size_t cSize = strlen(c) + 1;
	size_t converted = 0;
	wchar_t* wc = new wchar_t[cSize];
	mbstowcs_s(&converted, wc, cSize, c, cSize -1 );

	return wc;
}

#define DO_LOG_MESSAGE(spd_level, callback_level, msg, thread_name, cs_trace)                                 \
	auto msg_str = std::string(msg != nullptr ? msg : "");                                                    \
	auto thread_name_str = std::string(thread_name != nullptr ? thread_name : "");                            \
	auto cs_trace_str = std::string(cs_trace != nullptr ? cs_trace : "");                                     \
                                                                                                              \
	std::stringstream trace_stream;                                                                           \
	trace_stream << std::stacktrace::current();                                                               \
	auto trace_str = trace_stream.str() + cs_trace_str;                                                       \
                                                                                                              \
	std::string content = std::string(msg) + "\n" + trace_str + "\n";                                         \
	spdlog::default_logger()->spd_level(content);                                                             \
                                                                                                              \
	std::stringstream thread_id_stream;                                                                       \
	thread_id_stream << std::this_thread::get_id();                                                           \
                                                                                                              \
	if (m_LogCallback != nullptr)                                                                             \
	{                                                                                                         \
		m_LogCallback((u32)LogLevel::callback_level, thread_id_stream.str().c_str(), msg, trace_str.c_str()); \
	}                                                                                                         \

void NebulaEngine::Debugger::Logger::Exit()
{
	// Release all spdlog resources, and drop all loggers in the registry.
	// This is optional (only mandatory if using windows + async log).
	spdlog::shutdown();
}

bool Logger::Initialize()
{
	if (m_IsInitialize) return false;

	try
	{
		// init spdlog
		auto async_file =
			spdlog::basic_logger_mt<spdlog::async_factory>("log", "logs/debugger.log", true);

		spdlog::set_default_logger(async_file);
	
		// Flush all *registered* loggers using a worker thread every 3 seconds.
		// note: registered loggers *must* be thread safe for this to work correctly!
		spdlog::flush_every(std::chrono::seconds(3));

		// default
#if _DEBUG
		spdlog::set_level(spdlog::level::trace);
#else
		spdlog::set_level(spdlog::level::info);
#endif
		

		spdlog::set_pattern("[%Y-%m-%d %T.%e][process %p][thread %t][%l] %v");
	}
	catch (const spdlog::spdlog_ex &ex)	
	{
		std::printf("Log initialization failed: %s\n", ex.what());
		return false;
	}
	
	m_IsInitialize = true;
	return true;
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
	case LogLevel::Log:
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

void NebulaEngine::Debugger::Logger::BindCallback(LogCallback callback)
{
	m_LogCallback = callback;
}


void Logger::Log(const char* msg, const char* thread_name, const char* cs_trace)
{
	DO_LOG_MESSAGE(debug, Log, msg, thread_name, cs_trace);
}

void Logger::Info(const char* msg, const char* thread_name, const char* cs_trace)
{
	DO_LOG_MESSAGE(info, Info, msg, thread_name, cs_trace);
}

void Logger::Warning(const char* msg, const char* thread_name, const char* cs_trace)
{
	DO_LOG_MESSAGE(warn, Warning, msg, thread_name, cs_trace);
}

void Logger::Trace(const char* msg, const char* thread_name, const char* cs_trace)
{
	DO_LOG_MESSAGE(trace, Trace, msg, thread_name, cs_trace);
}

void Logger::Error(const char* msg, const char* thread_name, const char* cs_trace)
{
	DO_LOG_MESSAGE(error, Error, msg, thread_name, cs_trace);
}

void Logger::Fatal(const char* msg, const char* thread_name, const char* cs_trace)
{
	DO_LOG_MESSAGE(critical, Fatal, msg, thread_name, cs_trace);
}

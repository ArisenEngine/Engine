#include <spdlog/spdlog.h>
#include <spdlog/async.h>
#include <spdlog/cfg/env.h>   // support for loading levels from the environment variable
#include <spdlog/fmt/ostr.h> // support for user defined types
#include <spdlog/sinks/basic_file_sink.h>
#include <stacktrace>
#include "Logger.h"


using namespace ArisenEngine::Debugger;

#define DO_LOG_MESSAGE(spd_level, callback_level, msg, thread_name, cs_trace)                                 \
	std::string msg_str { msg != nullptr ? msg : "" };													      \
	std::string thread_name_str { thread_name != nullptr ? thread_name : "" };                                \
	std::string cs_trace_str { cs_trace != nullptr ? cs_trace : "" };                                         \
                                                                                                              \
	std::stringstream trace_stream;                                                                           \
	trace_stream << std::stacktrace::current();                                                               \
	std::string trace_str { trace_stream.str() + cs_trace_str };                                              \
                                                                                                              \
	std::string content { std::string(msg) + "\n" + trace_str + "\n" };                                       \
	spdlog::default_logger()->spd_level(content);                                                             \
                                                                                                              \
	std::stringstream thread_id_stream;                                                                       \
	thread_id_stream << std::this_thread::get_id();                                                           \
                                                                                                              \
	if (m_LogCallback != nullptr)                                                                             \
	{                                                                                                         \
		m_LogCallback((UInt32)LogLevel::callback_level, thread_id_stream.str().c_str(), msg, trace_str.c_str()); \
	}                                                                                                         \

#define DO_LOG_MESSAGE_NO_TRACE_WRITE(spd_level, callback_level, msg, thread_name, cs_trace)                  \
	std::string msg_str { msg != nullptr ? msg : "" };													      \
	std::string thread_name_str { thread_name != nullptr ? thread_name : "" };                                \
	std::string cs_trace_str { cs_trace != nullptr ? cs_trace : "" };                                         \
                                                                                                              \
	std::stringstream trace_stream;                                                                           \
	trace_stream << std::stacktrace::current();                                                               \
	std::string trace_str { trace_stream.str() + cs_trace_str };                                              \
                                                                                                              \
	std::string content { msg };                                                                              \
	spdlog::default_logger()->spd_level(content);                                                             \
                                                                                                              \
	std::stringstream thread_id_stream;                                                                       \
	thread_id_stream << std::this_thread::get_id();                                                           \
                                                                                                              \
	if (m_LogCallback != nullptr)                                                                             \
	{                                                                                                         \
		m_LogCallback((UInt32)LogLevel::callback_level, thread_id_stream.str().c_str(), msg, trace_str.c_str()); \
	}                                                                                                         \


Logger::Logger(): m_IsInitialize(false), m_LogCallback(nullptr)
{
	std::cout<<"Logger:"<<this<<std::endl;
}

#ifdef _WIN32
#include <windows.h>
#else
#include <pthread.h>
#endif

void set_thread_affinity(int core_id) {
#ifdef _WIN32
	HANDLE thread = GetCurrentThread();
	DWORD_PTR mask = 1ULL << core_id;
	SetThreadAffinityMask(thread, mask);
#else
	cpu_set_t cpuset;
	CPU_ZERO(&cpuset);
	CPU_SET(core_id, &cpuset);
	pthread_setaffinity_np(pthread_self(), sizeof(cpu_set_t), &cpuset);
#endif
}


bool Logger::Initialize()
{
	if (m_IsInitialize) return true;

	try
	{
		// 初始化一个容量为 8192、单线程的日志线程池
		constexpr size_t queue_size = 8192;
		constexpr size_t num_threads = 1;  // 固定为单线程
		auto thread_pool = std::make_shared<spdlog::details::thread_pool>(queue_size, num_threads);

		
		// init spdlog
		auto async_file =
			spdlog::basic_logger_mt<spdlog::async_factory>("log", "logs/log.log", true);
		
		spdlog::set_default_logger(async_file);
	
		// Flush all *registered* loggers using a worker thread every 3 seconds.
		// note: registered loggers *must* be thread safe for this to work correctly!
		spdlog::flush_every(std::chrono::seconds(3));

		spdlog::flush_on(spdlog::level::err);
		
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

Logger& Logger::GetInstance()
{
	static Logger _log_instnace;
	return _log_instnace;
}

void Logger::Shutdown()
{
	// Release all spdlog resources, and drop all loggers in the registry.
	// This is optional (only mandatory if using windows + async log).
	spdlog::default_logger()->flush();
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

void ArisenEngine::Debugger::Logger::BindCallback(LogCallback callback)
{
	m_LogCallback = callback;
}

void Logger::Log(const char* msg, const char* thread_name, const char* cs_trace)
{
	DO_LOG_MESSAGE_NO_TRACE_WRITE(debug, Log, msg, thread_name, cs_trace);
}

void Logger::Info(const char* msg, const char* thread_name, const char* cs_trace)
{
	DO_LOG_MESSAGE_NO_TRACE_WRITE(info, Info, msg, thread_name, cs_trace);
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

void Logger::Log(const std::wstring&& msg)
{
	Log(String::WStringToString(msg));
}

void Logger::Log(const std::string&& msg)
{
	Log(msg.c_str());
}

void Logger::Info(const std::wstring&& msg)
{
	Info(String::WStringToString(msg));
}

void Logger::Info(const std::string&& msg)
{
	Info(msg.c_str());
}

void Logger::Warning(const std::wstring&& msg)
{
	Warning(String::WStringToString(msg));
}

void Logger::Warning(const std::string&& msg)
{
	Warning(msg.c_str());
}

void Logger::Error(const std::wstring&& msg)
{
	Error(String::WStringToString(msg));
}

void Logger::Error(const std::string&& msg)
{
	Error(msg.c_str());
}

void Logger::Fatal(const std::wstring&& msg, bool needThrow)
{
	Fatal(String::WStringToString(msg));
}

void Logger::Fatal(const std::string&& msg, bool needThrow)
{
	Fatal(msg.c_str());
	if (needThrow)
	{
		throw std::runtime_error(msg);
	}
}

void Logger::Trace(const std::wstring&& msg)
{
	Trace(String::WStringToString(msg));
}

void Logger::Trace(const std::string&& msg)
{
	Trace(msg.c_str());
}
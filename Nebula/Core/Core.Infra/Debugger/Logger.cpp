#include <spdlog/spdlog.h>
#include <spdlog/async.h>
#include <spdlog/cfg/env.h>   // support for loading levels from the environment variable
#include <spdlog/fmt/ostr.h> // support for user defined types
#include <spdlog/sinks/basic_file_sink.h>


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

void NebulaEngine::Debugger::Logger::Exit()
{
	// Release all spdlog resources, and drop all loggers in the registry.
	// This is optional (only mandatory if using windows + async log).
	// spdlog::shutdown();
}

bool Logger::Initialize()
{
	if (m_IsInitialize) return false;

	// init spdlog
	auto async_file =
			spdlog::basic_logger_mt<spdlog::async_factory>("async_file_logger", "logs/async_log.txt");
	
	for (int i = 0; i < 1000000; ++i) {
		async_file->info("Async message #{}", i);
	}
	
	m_IsInitialize = true;
	return true;
}

void NebulaEngine::Debugger::Logger::StackTrace(std::string* stack_info)
{
	
}

void Logger::SetServerityLevel(LogLevel level)
{
	

	switch (level)
	{
	case LogLevel::Error:
		
		break;
	case LogLevel::Fatal:
		
		break;
	case LogLevel::Info:
		
		break;
	case LogLevel::Log:
		
		break;
	case LogLevel::Trace:
		
		break;
	case LogLevel::Warning:
		
		break;
	}

	
}

void NebulaEngine::Debugger::Logger::BindCallback(LogCallback callback)
{
	m_LogCallback = callback;
}


void Logger::Log(const char* msg, const char* thread_name, const char* cs_trace)
{
	std::string* stack_info = new std::string();

	StackTrace(stack_info);

if (cs_trace != nullptr)
{
	*stack_info += std::string(cs_trace) + "\n";
}

std::string threadName;
if (thread_name == nullptr) threadName = "";
else threadName = "[" + std::string(thread_name) + "]";

if (m_LogCallback != nullptr)
{
	//m_LogCallback((u32)LogLevel::Log, msg, stack_info.c_str());
}

}

void Logger::Info(const char* msg, const char* thread_name, const char* cs_trace)
{
	std::string* stack_info = new std::string();

	StackTrace(stack_info);
	std::string threadName;

	if (cs_trace != nullptr)
	{
		*stack_info += std::string(cs_trace) + "\n";
	}

	if (thread_name == nullptr) threadName = "";
	else threadName = "[" + std::string(thread_name) + "]";
	

	if (m_LogCallback != nullptr)
	{
		//m_LogCallback((u32)LogLevel::Info, msg, stack_info.c_str());
	}
}

void Logger::Warning_Threaded(const std::string* msg, const std::string* thread_name, const std::string* invoker_thread_id, const std::string* cs_trace)
{
	
}

void Logger::Warning(const char* msg, const char* thread_name, const char* cs_trace)
{
	
	
}

void Logger::Trace(const char* msg, const char* thread_name, const char* cs_trace)
{
	
}

void Logger::Error(const char* msg, const char* thread_name, const char* cs_trace)
{

}

void Logger::Fatal(const char* msg, const char* thread_name, const char* cs_trace)
{

	
}

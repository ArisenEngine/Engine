#include "Logger.h"


using namespace NebulaEngine::Debugger;
namespace logging = boost::log;
namespace keywords = boost::log::keywords;
namespace attrs = boost::log::attributes;

bool Logger::m_IsInitialize = false;

void Logger::Initialize()
{
	if (m_IsInitialize) return;

	m_IsInitialize = true;

	namespace fs = boost::filesystem;

	// 获取当前目录
	fs::path current = fs::initial_path<boost::filesystem::path>();

	logging::register_simple_formatter_factory<logging::trivial::severity_level, char>("Severity");

	logging::add_file_log(

		keywords::file_name = current / "/Debugger.log",
		keywords::format =
		"[%TimeStamp%] [%ProcessID%] [%ThreadID%] %ThreadName% [%Severity%] %Message%" \
		"%STACK_TRACE%"
	);

	Logger::SetServerityLevel(LogLevel::Trace);

	logging::add_common_attributes();

}

void NebulaEngine::Debugger::Logger::StackTrace(std::string& stack_info)
{
	std::stringstream trace_stream;
	trace_stream << boost::stacktrace::stacktrace() << std::endl;
	stack_info = "\n" + trace_stream.str();
	trace_stream.clear();
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


void Logger::Log(const wchar_t* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	std::string stack_info;

	StackTrace(stack_info);
	
	if (cs_trace != nullptr)
	{
		std::regex regex_newlines("\n+"); 
		std::string result = std::regex_replace(std::string(cs_trace), regex_newlines, "\n");

		stack_info += result + "\n";
	}

	std::string threadName;
	if (thread_name == nullptr) threadName = "";
	else threadName = "[" + std::string(thread_name) + "]";
	auto attri = logging::core::get()
		->add_global_attribute("ThreadName", attrs::constant<std::string>(threadName));
	BOOST_LOG_TRIVIAL(debug) << msg;
	logging::core::get()->remove_global_attribute(attri.first);

}

void Logger::Info(const wchar_t* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	std::string stack_info;

	StackTrace(stack_info);
	std::string threadName;

	if (cs_trace != nullptr)
	{
		stack_info += std::string(cs_trace) + "\n";
	}

	if (thread_name == nullptr) threadName = "";
	else threadName = "[" + std::string(thread_name) + "]";

	auto attri = logging::core::get()
		->add_global_attribute("ThreadName", attrs::constant<std::string>(threadName));

	BOOST_LOG_TRIVIAL(info) << msg;

	logging::core::get()->remove_global_attribute(attri.first);
}

void Logger::Warning(const wchar_t* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	std::string stack_info;

	StackTrace(stack_info);
	std::string threadName;

	if (cs_trace != nullptr)
	{
		std::regex regex_newlines("\n+");
		std::string result = std::regex_replace(std::string(cs_trace), regex_newlines, "\n");

		stack_info += result + "\n";
	}

	if (thread_name == nullptr) threadName = "";
	else threadName = "[" + std::string(thread_name) + "]";

	auto attri = logging::core::get()->add_global_attribute("STACK_TRACE", attrs::constant<std::string>(stack_info));
	auto thread_name_attri = logging::core::get()
		->add_global_attribute("ThreadName", attrs::constant<std::string>(threadName));
	BOOST_LOG_TRIVIAL(warning) << msg;
	logging::core::get()->remove_global_attribute(attri.first);
	logging::core::get()->remove_global_attribute(thread_name_attri.first);
}

void Logger::Trace(const wchar_t* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	std::string stack_info;

	StackTrace(stack_info);
	std::string threadName;

	if (cs_trace != nullptr)
	{
		stack_info += std::string(cs_trace) + "\n";
	}

	if (thread_name == nullptr) threadName = "";
	else threadName = "[" + std::string(thread_name) + "]";

	auto attri = logging::core::get()->add_global_attribute("STACK_TRACE", attrs::constant<std::string>(stack_info));
	auto thread_name_attri = logging::core::get()
		->add_global_attribute("ThreadName", attrs::constant<std::string>(threadName));
	BOOST_LOG_TRIVIAL(trace) << msg;
	logging::core::get()->remove_global_attribute(attri.first);
	logging::core::get()->remove_global_attribute(thread_name_attri.first);
}

void Logger::Error(const wchar_t* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	std::string stack_info;

	StackTrace(stack_info);
	std::string threadName;

	if (cs_trace != nullptr)
	{
		stack_info += std::string(cs_trace) + "\n";
	}

	if (thread_name == nullptr) threadName = "";
	else threadName = "[" + std::string(thread_name) + "]";

	auto attri = logging::core::get()->add_global_attribute("STACK_TRACE", attrs::constant<std::string>(stack_info));
	auto thread_name_attri = logging::core::get()
		->add_global_attribute("ThreadName", attrs::constant<std::string>(threadName));
	BOOST_LOG_TRIVIAL(error) << msg;
	logging::core::get()->remove_global_attribute(attri.first);
	logging::core::get()->remove_global_attribute(thread_name_attri.first);
}

void Logger::Fatal(const wchar_t* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	std::string stack_info;

	StackTrace(stack_info);
	std::string threadName;

	if (cs_trace != nullptr)
	{
		stack_info += std::string(cs_trace) + "\n";
	}

	if (thread_name == nullptr) threadName = "";
	else threadName = "[" + std::string(thread_name) + "]";

	auto attri = logging::core::get()->add_global_attribute("STACK_TRACE", attrs::constant<std::string>(stack_info));
	auto thread_name_attri = logging::core::get()
		->add_global_attribute("ThreadName", attrs::constant<std::string>(threadName));
	BOOST_LOG_TRIVIAL(fatal) << msg;
	logging::core::get()->remove_global_attribute(attri.first);
	logging::core::get()->remove_global_attribute(thread_name_attri.first);
}

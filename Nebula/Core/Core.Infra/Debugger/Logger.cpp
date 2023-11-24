#include "Logger.h"

using namespace NebulaEngine::Debugger;
namespace logging = boost::log;
namespace keywords = boost::log::keywords;
namespace attrs = boost::log::attributes;

bool Logger::m_IsInitialize = false;
LogCallback Logger::m_LogCallback = nullptr;

static boost::thread_group threads_internal;

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
	threads_internal.join_all();
}

void Logger::Initialize()
{
	if (m_IsInitialize) return;

	
	m_IsInitialize = true;
	
	namespace fs = boost::filesystem;
	
	fs::path current = fs::initial_path<boost::filesystem::path>();

	logging::register_simple_formatter_factory<logging::trivial::severity_level, char>("Severity");

	logging::add_file_log(

		keywords::file_name = current / "/Debugger.log",
		keywords::format =
		"[%TimeStamp%] [%ProcessID%] %THREAD_ID% %THREAD_NAME% [%Severity%] %Message%" \
		"%STACK_TRACE%"
	);

	Logger::SetServerityLevel(LogLevel::Trace);

	logging::add_common_attributes();

	logging::core::get()->add_global_attribute("THREAD_ID", attrs::constant<std::string>(""));
	logging::core::get()->add_global_attribute("STACK_TRACE", attrs::constant<std::string>(""));
	logging::core::get()->add_global_attribute("THREAD_NAME", attrs::constant<std::string>(""));

}

void NebulaEngine::Debugger::Logger::StackTrace(std::string* stack_info)
{
	std::stringstream trace_stream;
	trace_stream << boost::stacktrace::stacktrace() << std::endl;
	*stack_info = "\n" + trace_stream.str();
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

void NebulaEngine::Debugger::Logger::BindCallback(LogCallback callback)
{
	m_LogCallback = callback;
}


void Logger::Log(const char* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	std::string* stack_info = new std::string();

	StackTrace(stack_info);

if (cs_trace != nullptr)
{
	std::regex regex_newlines("\n+");
	std::string result = std::regex_replace(std::string(cs_trace), regex_newlines, "\n");

	*stack_info += result + "\n";
}

std::string threadName;
if (thread_name == nullptr) threadName = "";
else threadName = "[" + std::string(thread_name) + "]";
auto attri = logging::core::get()
->add_global_attribute("ThreadName", attrs::constant<std::string>(threadName));
BOOST_LOG_TRIVIAL(debug) << msg;
logging::core::get()->remove_global_attribute(attri.first);

if (m_LogCallback != nullptr)
{
	//m_LogCallback((u32)LogLevel::Log, msg, stack_info.c_str());
}

}

void Logger::Info(const char* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	std::string* stack_info = new std::string();

	StackTrace(stack_info);
	std::string threadName;

	if (cs_trace != nullptr)
	{
		*stack_info += std::string(cs_trace) + "\n";
	}

	if (thread_name == nullptr) threadName = "";
	else threadName = "[" + std::string(thread_name) + "]";

	auto attri = logging::core::get()
		->add_global_attribute("ThreadName", attrs::constant<std::string>(threadName));

	BOOST_LOG_TRIVIAL(info) << msg;

	logging::core::get()->remove_global_attribute(attri.first);

	if (m_LogCallback != nullptr)
	{
		//m_LogCallback((u32)LogLevel::Info, msg, stack_info.c_str());
	}
}

void Logger::Warning_Threaded(const std::string* msg, const std::string* thread_name, const std::string* invoker_thread_id, const std::string* cs_trace)
{
	std::string stack_info;

	std::stringstream trace_stream;
	trace_stream << boost::stacktrace::stacktrace() << std::endl;
	stack_info = "\n" + trace_stream.str();

	std::string threadName;

	if (cs_trace != nullptr)
	{
		std::regex regex_newlines("\n+");
		std::string result = std::regex_replace(*cs_trace, regex_newlines, "\n");

		stack_info += result + "\n";
	}

	if (thread_name == nullptr) threadName = "[unnamed thread]";
	else threadName = "[" + *thread_name + "]";

	BOOST_LOG_SCOPED_THREAD_TAG("THREAD_NAME", threadName);
	BOOST_LOG_SCOPED_THREAD_TAG("STACK_TRACE", stack_info);
	BOOST_LOG_SCOPED_THREAD_TAG("THREAD_ID", *invoker_thread_id);

	BOOST_LOG_TRIVIAL(warning) << *msg;

	// NOTE: must delete this cause we new in main thread and pass here
	delete msg;
	delete thread_name;
	delete invoker_thread_id;
	delete cs_trace;

	if (m_LogCallback != nullptr)
	{
		/*std::wstring w_msg = char_to_wchar(msg);
		std::wstring w_trace = char_to_wchar(stack_info.c_str());
	
		m_LogCallback((u32)LogLevel::Warning, w_msg.c_str(), w_trace.c_str());*/
	}
}

void Logger::Warning(const char* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	// Here we go. First, identify the thread.
	std::stringstream thread_id;
	thread_id << boost::this_thread::get_id();
	const std::string* thread_id_str = new std::string("[" + thread_id.str() + "]");

	threads_internal.create_thread(
		boost::bind(Warning_Threaded,
			new std::string(msg),
			thread_name == nullptr ? nullptr : new std::string(thread_name),
			thread_id_str, 
			cs_trace == nullptr ? nullptr : new std::string(cs_trace)
		));
	//Warning_Threaded(msg, thread_name, thread_id_str, cs_trace);
	
}

void Logger::Trace(const char* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	std::string* stack_info = new std::string();

	StackTrace(stack_info);
	std::string threadName;

	if (cs_trace != nullptr)
	{
		*stack_info += std::string(cs_trace) + "\n";
	}

	if (thread_name == nullptr) threadName = "";
	else threadName = "[" + std::string(thread_name) + "]";

	auto attri = logging::core::get()->add_global_attribute("STACK_TRACE", attrs::constant<std::string>(*stack_info));
	auto thread_name_attri = logging::core::get()
		->add_global_attribute("ThreadName", attrs::constant<std::string>(threadName));
	BOOST_LOG_TRIVIAL(trace) << msg;
	logging::core::get()->remove_global_attribute(attri.first);
	logging::core::get()->remove_global_attribute(thread_name_attri.first);

	if (m_LogCallback != nullptr)
	{
		//m_LogCallback((u32)LogLevel::Trace, msg, stack_info.c_str());
	}
}

void Logger::Error(const char* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	std::string* stack_info = new std::string();

	StackTrace(stack_info);
	std::string threadName;

	if (cs_trace != nullptr)
	{
		*stack_info += std::string(cs_trace) + "\n";
	}

	if (thread_name == nullptr) threadName = "";
	else threadName = "[" + std::string(thread_name) + "]";

	auto attri = logging::core::get()->add_global_attribute("STACK_TRACE", attrs::constant<std::string>(*stack_info));
	auto thread_name_attri = logging::core::get()
		->add_global_attribute("ThreadName", attrs::constant<std::string>(threadName));
	BOOST_LOG_TRIVIAL(error) << msg;

	logging::core::get()->remove_global_attribute(attri.first);
	logging::core::get()->remove_global_attribute(thread_name_attri.first);

	if (m_LogCallback != nullptr)
	{
		//m_LogCallback((u32)LogLevel::Error, msg, stack_info.c_str());
	}
}

void Logger::Fatal(const char* msg, const char* thread_name, const char* cs_trace)
{

	Initialize();

	std::string* stack_info = new std::string();

	StackTrace(stack_info);
	std::string threadName;

	if (cs_trace != nullptr)
	{
		*stack_info += std::string(cs_trace) + "\n";
	}

	if (thread_name == nullptr) threadName = "";
	else threadName = "[" + std::string(thread_name) + "]";

	auto attri = logging::core::get()->add_global_attribute("STACK_TRACE", attrs::constant<std::string>(*stack_info));
	auto thread_name_attri = logging::core::get()
		->add_global_attribute("ThreadName", attrs::constant<std::string>(threadName));
	BOOST_LOG_TRIVIAL(fatal) << msg;
	logging::core::get()->remove_global_attribute(attri.first);
	logging::core::get()->remove_global_attribute(thread_name_attri.first);

	if (m_LogCallback != nullptr)
	{
		//m_LogCallback((u32)LogLevel::Fatal, msg, stack_info.c_str());
	}
}

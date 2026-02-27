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

namespace ArisenEngine::Diagnostics
{
    static String GetStacktrace()
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
        return String(trace_stream.str());
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

#if _DEBUG
            spdlog::flush_every(std::chrono::seconds(1));
            spdlog::flush_on(spdlog::level::err);
#else
            // Production: Minimize I/O impact. Rely on OS page cache and fatal crash handling.
            spdlog::flush_every(std::chrono::seconds(5));
            spdlog::flush_on(spdlog::level::critical);
#endif
            
#if _DEBUG
            spdlog::set_level(spdlog::level::trace);
#else
            spdlog::set_level(spdlog::level::info);
#endif
            
            spdlog::set_pattern("[%Y-%m-%d %T.%e][process %p][thread %t][%l] %v");

            // Register with Foundation Bridge
            ArisenEngine::Diagnostics::Log::SetHandler(this);
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
        ArisenEngine::Diagnostics::Log::SetHandler(nullptr);
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

    void Logger::Log(LogLevel level, const char* msg, const LogSourceLocation& location, const char* thread_name)
    {
        spdlog::level::level_enum spd_level;
        bool needs_trace = false;
        switch (level)
        {
        case LogLevel::Trace:   spd_level = spdlog::level::trace; break;
        case LogLevel::Debug:   spd_level = spdlog::level::debug; break;
        case LogLevel::Info:    spd_level = spdlog::level::info; break;
        case LogLevel::Warning: spd_level = spdlog::level::warn; needs_trace = true; break;
        case LogLevel::Error:   spd_level = spdlog::level::err; needs_trace = true; break;
        case LogLevel::Fatal:   spd_level = spdlog::level::critical; needs_trace = true; break;
        default: spd_level = spdlog::level::info; break;
        }

        String full_msg = msg ? msg : "";
        String trace;
        if (needs_trace)
        {
            trace = GetStacktrace();
            if (!trace.IsEmpty())
            {
                full_msg += "\n[stacktrace]\n" + trace;
            }
        }

        spdlog::source_loc loc(location.file, static_cast<int>(location.line), location.function);
        
        // Use spdlog's native logging with source location
        if (auto logger = spdlog::default_logger())
        {
            logger->log(loc, spd_level, full_msg.GetString());
        }

        if (m_LogCallback)
        {
            // For callback, we still provide a thread ID string if not provided
            String tid;
            if (thread_name) {
                tid = thread_name;
            } else {
                std::stringstream ss;
                ss << std::this_thread::get_id();
                tid = ss.str().c_str();
            }
            
            m_LogCallback(static_cast<UInt32>(level), tid.c_str(), msg ? msg : "", trace.c_str());
        }
    }
} // namespace ArisenEngine::Diagnostics

#include "LoggerAPI.h"

namespace ArisenEngine::Diagnostics
{
    void Logger_Log(LogLevel level, const char* msg, const LogSourceLocation* location, const char* thread_name)
    {
        if (location)
            Logger::GetInstance().Log(level, msg, *location, thread_name);
    }

    void Logger_SetServerityLevel(LogLevel level)
    {
        Logger::GetInstance().SetServerityLevel(level);
    }

    void Logger_BindCallback(void* callback)
    {
        Logger::GetInstance().BindCallback(reinterpret_cast<LogCallback>(callback));
    }

    bool Logger_Initialize(bool bindCallback)
    {
        return Logger::GetInstance().Initialize();
    }

    void Logger_Shutdown()
    {
        Logger::Shutdown();
    }
}

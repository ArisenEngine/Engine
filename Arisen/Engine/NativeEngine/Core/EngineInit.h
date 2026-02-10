#pragma once

#include "Logger/Logger.h"
#include "Diagnostics/Log.h"
#include <cstdlib>
#include <csignal>
#include <exception>
#include <string>
#include <format>

#ifdef _WIN64
#include <Windows.h>
#endif
#include "../../../Core/Core.Foundation/Base/Assertion.h"

namespace ArisenEngine::Core
{
    /**
     * @brief Centralized engine initialization and crash-safe shutdown for all applications.
     * 
     * This module handles:
     * - Logger initialization and shutdown
     * - Assertion system initialization (including CRT hooks)
     * - Crash-safe cleanup on unhandled exceptions, signals, and abnormal termination
     * - Platform-specific exception handling (SEH on Windows)
     */
    class EngineInit
    {
    public:
        /**
         * @brief Initialize the engine core systems (logger, crash handlers).
         * @return true if initialization succeeded, false otherwise.
         */
        static bool Initialize()
        {
            if (!Diagnostics::Logger::GetInstance().Initialize())
            {
                return false;
            }
            
            Diagnostics::Log::SetHandler(&Diagnostics::Logger::GetInstance());
            
            ArisenEngine::InitAssertionSystem();

            SetupCrashHandlers();
            return true;
        }

        /**
         * @brief Shutdown the engine core systems gracefully.
         */
        static void Shutdown()
        {
            try
            {
                Diagnostics::Logger::Shutdown();
            }
            catch (...)
            {
                // Suppress all exceptions during shutdown
            }
        }

    private:
        /**
         * @brief Setup crash handlers for safe logger shutdown on abnormal termination.
         */
        static void SetupCrashHandlers()
        {
#ifdef _WIN64
            // Windows SEH (Structured Exception Handling)
            SetUnhandledExceptionFilter(ArisenUnhandledExceptionFilter);
#endif
            // Standard C++ exception handling
            std::set_terminate(ArisenOnTerminate);
            
            // Signal handlers
            signal(SIGABRT, ArisenOnSignal);
            signal(SIGSEGV, ArisenOnSignal);
            signal(SIGILL, ArisenOnSignal);
            signal(SIGFPE, ArisenOnSignal);
            
            // atexit handler for normal termination
            std::atexit([]() {
                Shutdown();
            });
        }

#ifdef _WIN64
        /**
         * @brief Windows SEH exception filter.
         */
        static LONG WINAPI ArisenUnhandledExceptionFilter(EXCEPTION_POINTERS* ep)
        {
            String msg = "Unknown SEH Exception";
            if (ep && ep->ExceptionRecord)
            {
                msg = String::Format("Unhandled Exception: 0x%08X at address 0x%P", 
                    ep->ExceptionRecord->ExceptionCode, ep->ExceptionRecord->ExceptionAddress);
            }
            ArisenEngine::ReportAssertionFailure("SEH Exception", "Unknown", 0, "ArisenUnhandledExceptionFilter", msg.c_str());
            Shutdown();
            return EXCEPTION_EXECUTE_HANDLER;
        }
#endif

        /**
         * @brief Handler for std::terminate.
         */
        static void ArisenOnTerminate()
        {
            ArisenEngine::ReportAssertionFailure("std::terminate", "Unknown", 0, "ArisenOnTerminate", "Engine terminated abnormally");
            Shutdown();
            std::abort();
        }

        /**
         * @brief Handler for signals (SIGABRT, SIGSEGV, etc).
         */
        static void ArisenOnSignal(int)
        {
            Shutdown();
#ifdef _WIN64
            ::ExitProcess(3);
#else
            std::_Exit(3);
#endif
        }
    };
}

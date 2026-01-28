#pragma once

#include "Common/CommandHeaders.h"
#include "../CoreDiagnosticCommon.h"
#include "../../Core.Foundation/Diagnostics/ILogHandler.h"

namespace ArisenEngine::Diagnostics
{
    using LogCallback = void(*)(UInt32, const char*, const char*, const char*);

    class DIAGNOSTIC_DLL Logger final : public ILogHandler
    {
    public:
        NO_COPY_NO_MOVE(Logger)
        NO_COMPARE(Logger)

        // Implementation of ILogHandler
        void Log(LogLevel level, const char* msg, const char* thread_name = nullptr, const char* cs_trace = nullptr) override;

        void SetServerityLevel(LogLevel level);
        void BindCallback(LogCallback callback);
        bool Initialize();

        static Logger& GetInstance();
        static void Shutdown();

    private:
        bool m_IsInitialize;
        LogCallback m_LogCallback;
        Logger();
    };
}
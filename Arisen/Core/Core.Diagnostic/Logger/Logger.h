#pragma once

#include "Base/FoundationMinimal.h"
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
        void Log(LogLevel level, const char* msg, const LogSourceLocation& location, const char* thread_name = nullptr) override;

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
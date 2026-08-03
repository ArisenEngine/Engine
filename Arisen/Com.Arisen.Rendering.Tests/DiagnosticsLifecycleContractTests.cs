using Xunit;

namespace ArisenEngine.Tests;

public sealed class DiagnosticsLifecycleContractTests
{
    [Fact]
    public void KernelInformationalChannelRemainsVisibleAtReleaseSeverity()
    {
        string managedLoggerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core/Diagnostics/Logger.cs");
        string nativeLoggerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core.native/Source/Core.Diagnostic/Logger/Logger.cpp")
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains(
            "public void Log(string message) => Logger.Info(message);",
            managedLoggerSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public void LogFormat(string format, params object[] args) => Logger.Info(string.Format(format, args));",
            managedLoggerSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public void Log(string message) => Logger.Log(message);",
            managedLoggerSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "#if _DEBUG\n            spdlog::set_level(spdlog::level::trace);\n#else\n" +
            "            spdlog::set_level(spdlog::level::info);\n#endif",
            nativeLoggerSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CorePackageOwnsFinalDiagnosticsDrain()
    {
        string corePackageSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core/CorePackage.cs");
        string nativeRuntimeSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core/Lifecycle/NativeRuntime.cs");
        string editorPackageSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/EditorPackage.cs");

        int completionMarker = corePackageSource.IndexOf(
            "[CorePackage] Completing diagnostics logging.",
            StringComparison.Ordinal);
        int diagnosticsShutdown = corePackageSource.IndexOf(
            "NativeRuntime.Shutdown();",
            completionMarker,
            StringComparison.Ordinal);
        Assert.True(completionMarker >= 0);
        Assert.True(diagnosticsShutdown > completionMarker);

        int shutdownMethod = nativeRuntimeSource.IndexOf(
            "public static void Shutdown()",
            StringComparison.Ordinal);
        Assert.True(shutdownMethod >= 0);
        Assert.Contains(
            "if (!m_DiagnosticsInitialized) return;",
            nativeRuntimeSource[shutdownMethod..],
            StringComparison.Ordinal);
        Assert.Contains(
            "Diagnostics.Logger.Dispose();",
            nativeRuntimeSource[shutdownMethod..],
            StringComparison.Ordinal);
        Assert.Contains(
            "KernelLog.InvalidateCache();",
            nativeRuntimeSource[shutdownMethod..],
            StringComparison.Ordinal);

        Assert.DoesNotContain("Logger.Dispose();", editorPackageSource, StringComparison.Ordinal);
        Assert.Contains(
            "UI Loop exited. Returning control for engine shutdown.",
            editorPackageSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NativeDiagnosticsShutdownStopsFoundationAndDrainsAsyncQueue()
    {
        string loggerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core.native/Source/Core.Diagnostic/Logger/Logger.cpp");
        string foundationLogSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core.native/Source/Core.Foundation/Diagnostics/Log.cpp");
        string managedLoggerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core/Diagnostics/Logger.cs");
        string dispatcherSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core/Diagnostics/OrderedNotificationDispatcher.cs");
        string validationSource = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_runtime.bat");

        int shutdownMethod = loggerSource.IndexOf("void Logger::Shutdown()", StringComparison.Ordinal);
        int stopAccepting = loggerSource.IndexOf(
            "m_LifecycleState = LifecycleState::StopRequested;",
            shutdownMethod,
            StringComparison.Ordinal);
        int detachFoundation = loggerSource.IndexOf(
            "Log::SetHandler(nullptr);",
            stopAccepting,
            StringComparison.Ordinal);
        int drainActiveLogs = loggerSource.IndexOf(
            "return instance.m_ActiveLogs == 0;",
            detachFoundation,
            StringComparison.Ordinal);
        int clearCallback = loggerSource.IndexOf(
            "instance.m_LogCallback = nullptr;",
            drainActiveLogs,
            StringComparison.Ordinal);
        int enqueueFlush = loggerSource.IndexOf(
            "logger->flush();",
            clearCallback,
            StringComparison.Ordinal);
        int drainAndJoin = loggerSource.IndexOf(
            "spdlog::shutdown();",
            enqueueFlush,
            StringComparison.Ordinal);

        Assert.True(shutdownMethod >= 0);
        Assert.True(stopAccepting > shutdownMethod);
        Assert.True(detachFoundation > stopAccepting);
        Assert.True(drainActiveLogs > detachFoundation);
        Assert.True(clearCallback > drainActiveLogs);
        Assert.True(enqueueFlush > clearCallback);
        Assert.True(drainAndJoin > enqueueFlush);
        Assert.Contains("ILogHandler* Log::AcquireHandler()", foundationLogSource, StringComparison.Ordinal);
        Assert.Contains("++s_ActiveHandlerCalls;", foundationLogSource, StringComparison.Ordinal);
        Assert.Contains("--s_ActiveHandlerCalls;", foundationLogSource, StringComparison.Ordinal);
        Assert.Contains("s_HandlerDrained.wait", foundationLogSource, StringComparison.Ordinal);
        Assert.Contains("lock (s_LifecycleLock)", managedLoggerSource, StringComparison.Ordinal);
        Assert.Contains("if (!IsInitialized)", managedLoggerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run", managedLoggerSource, StringComparison.Ordinal);
        Assert.Contains("BlockingCollection<QueuedNotification>", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("m_Queue.CompleteAdding();", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("m_WorkerCompleted.Wait();", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("m_Worker.Join();", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("m_Dispatch = null;", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains(
            "Core diagnostics queue did not report deterministic completion",
            validationSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ManagedDiagnosticsDrainPrecedesSubscriberAndDispatcherRelease()
    {
        string managedLoggerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.core/Diagnostics/Logger.cs");

        int disposeMethod = managedLoggerSource.IndexOf(
            "public static void Dispose()",
            StringComparison.Ordinal);
        int nativeDrain = managedLoggerSource.IndexOf(
            "LoggerAPI.Logger_Shutdown();",
            disposeMethod,
            StringComparison.Ordinal);
        int stopDispatcher = managedLoggerSource.IndexOf(
            "dispatcher.RequestStop();",
            nativeDrain,
            StringComparison.Ordinal);
        int drainDispatcher = managedLoggerSource.IndexOf(
            "dispatcher.Dispose();",
            stopDispatcher,
            StringComparison.Ordinal);
        int releaseDispatcher = managedLoggerSource.IndexOf(
            "Volatile.Write(ref s_NotificationDispatcher, null);",
            drainDispatcher,
            StringComparison.Ordinal);
        int releaseSubscribers = managedLoggerSource.IndexOf(
            "s_MessageAdded = null;",
            releaseDispatcher,
            StringComparison.Ordinal);

        Assert.True(disposeMethod >= 0);
        Assert.True(nativeDrain > disposeMethod);
        Assert.True(stopDispatcher > nativeDrain);
        Assert.True(drainDispatcher > stopDispatcher);
        Assert.True(releaseDispatcher > drainDispatcher);
        Assert.True(releaseSubscribers > releaseDispatcher);
        Assert.Contains("s_MessageCleared = null;", managedLoggerSource, StringComparison.Ordinal);
        Assert.Contains("s_AcceptsSubscribers = false;", managedLoggerSource, StringComparison.Ordinal);
        Assert.Contains("if (dispatcher?.IsDispatchThread == true)", managedLoggerSource, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Arisen")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}

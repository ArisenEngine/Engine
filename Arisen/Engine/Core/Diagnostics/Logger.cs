using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Arisen.Native.Diagnostics;

namespace ArisenEngine.Core.Diagnostics;

public static class Logger
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LogCallback(uint type, [MarshalAs(UnmanagedType.LPUTF8Str)] string msg,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string threadName, [MarshalAs(UnmanagedType.LPUTF8Str)] string trace);

    private static LogCallback? m_ReceiveLog;

    internal static void RecordLog(uint type, string msg, string threadName, string trace)
    {
        var message = new LogMessage((LogLevel)type, msg, "0", threadName, DateTime.Now, trace);

        Task.Run(() => { MessageAdded?.Invoke(message); });
    }

    static Logger()
    {
        m_ReceiveLog = new LogCallback(RecordLog);
    }

    public enum LogLevel
    {
        Trace = 0x01,
        Log = 0x02,
        Info = 0x04,
        Warning = 0x08,
        Error = 0x10,
        Fatal = 0x20
    }

    public class LogMessage
    {
        public DateTime Time { get; }
        public LogLevel LogLevel { get; }
        public string Message { get; }
        public string ThreadId { get; }
        public string ThreadName { get; }
        public string StackTrace { get; } = string.Empty;

        public string FullLogString =>
            $"[{Time}] [{LogLevel}] [ThreadId:{ThreadId}, ThreadName:{ThreadName}] \nMessage: {Message} \n" +
            (LogLevel == LogLevel.Log ? "" : StackTrace);

        internal LogMessage(LogLevel logLevel, string msg, string threadId, string threadName, DateTime time,
            string stackTrace)
        {
            Time = time;
            LogLevel = logLevel;
            Message = msg;
            ThreadId = threadId;
            ThreadName = threadName;
            StackTrace = stackTrace;
        }
    }

    public static Action<LogMessage>? MessageAdded;
    public static Action? MessageCleared;

    public static void Dispose()
    {
        LoggerAPI.Logger_Shutdown();
    }

    [Conditional("DEBUG")]
    public static void Assert(
        bool condition,
        string message = "",
        [System.Runtime.CompilerServices.CallerFilePath]
        string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber]
        int line = 0,
        [System.Runtime.CompilerServices.CallerMemberName]
        string function = "")
    {
        if (!condition)
        {
            // Optional: Call native assert if available in the future
            System.Diagnostics.Debug.Assert(condition, message);
        }
    }

    public static void Log(object msg,
        [System.Runtime.CompilerServices.CallerFilePath]
        string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber]
        int line = 0,
        [System.Runtime.CompilerServices.CallerMemberName]
        string function = "") => WriteLog(LogLevel.Log, msg, file, line, function);

    public static void Info(object msg,
        [System.Runtime.CompilerServices.CallerFilePath]
        string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber]
        int line = 0,
        [System.Runtime.CompilerServices.CallerMemberName]
        string function = "") => WriteLog(LogLevel.Info, msg, file, line, function);

    public static void Trace(object msg,
        [System.Runtime.CompilerServices.CallerFilePath]
        string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber]
        int line = 0,
        [System.Runtime.CompilerServices.CallerMemberName]
        string function = "") => WriteLog(LogLevel.Trace, msg, file, line, function);

    public static void Warning(object msg,
        [System.Runtime.CompilerServices.CallerFilePath]
        string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber]
        int line = 0,
        [System.Runtime.CompilerServices.CallerMemberName]
        string function = "") => WriteLog(LogLevel.Warning, msg, file, line, function);

    public static void Error(object msg,
        [System.Runtime.CompilerServices.CallerFilePath]
        string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber]
        int line = 0,
        [System.Runtime.CompilerServices.CallerMemberName]
        string function = "") => WriteLog(LogLevel.Error, msg, file, line, function);

    public static void Fatal(object msg,
        [System.Runtime.CompilerServices.CallerFilePath]
        string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber]
        int line = 0,
        [System.Runtime.CompilerServices.CallerMemberName]
        string function = "") => WriteLog(LogLevel.Fatal, msg, file, line, function);

    [SuppressUnmanagedCodeSecurity,
     DllImport("Core.Diagnostic.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Logger_Log")]
    private static extern unsafe void Logger_Log_Internal(Arisen.Native.Diagnostics.LogLevel level, byte* msg,
        LogSourceLocationNative* location, byte* thread_name);

    private struct LogSourceLocationNative
    {
        public unsafe byte* File;
        public unsafe byte* Function;
        public uint Line;
    }

    private static unsafe void WriteLog(LogLevel level, object msg, string file, int line, string function)
    {
        string threadName = Thread.CurrentThread.Name ?? "MainThread";

        // Map engine LogLevel to native LogLevel
        var nativeLevel = level switch
        {
            LogLevel.Trace => Arisen.Native.Diagnostics.LogLevel.Trace,
            LogLevel.Log => Arisen.Native.Diagnostics.LogLevel.Debug,
            LogLevel.Info => Arisen.Native.Diagnostics.LogLevel.Info,
            LogLevel.Warning => Arisen.Native.Diagnostics.LogLevel.Warning,
            LogLevel.Error => Arisen.Native.Diagnostics.LogLevel.Error,
            LogLevel.Fatal => Arisen.Native.Diagnostics.LogLevel.Fatal,
            _ => Arisen.Native.Diagnostics.LogLevel.Info
        };

        string msgStr = msg?.ToString() ?? string.Empty;

        // Calculate needed byte lengths (UTF8 encoding might take up to 3 bytes per char, but we check accurately)
        int msgLen = System.Text.Encoding.UTF8.GetMaxByteCount(msgStr.Length);
        int fileLen = System.Text.Encoding.UTF8.GetMaxByteCount(file.Length);
        int funcLen = System.Text.Encoding.UTF8.GetMaxByteCount(function.Length);
        int threadLen = System.Text.Encoding.UTF8.GetMaxByteCount(threadName.Length);

        // For string lengths under safe limits, use stackalloc. Otherwise use ArrayPool.
        // limit stack alloc to 2048 bytes for message. File/func/thread should never exceed limits.
        const int MaxStackAllocSize = 2048;

        byte[]? msgBuffer = null;
        Span<byte> msgSpan = msgLen <= MaxStackAllocSize
            ? stackalloc byte[msgLen]
            : (msgBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(msgLen));

        Span<byte> fileSpan = stackalloc byte[fileLen];
        Span<byte> funcSpan = stackalloc byte[funcLen];
        Span<byte> threadSpan = stackalloc byte[threadLen];

        try
        {
            // Encode strings to UTF8. Note: GetBytes writes the bytes to the span and returns the actual length.
            // We need to null-terminate the strings for C++
            int actualMsgLen = System.Text.Encoding.UTF8.GetBytes(msgStr, msgSpan);
            msgSpan[actualMsgLen] = 0; // Null terminator

            int actualFileLen = System.Text.Encoding.UTF8.GetBytes(file, fileSpan);
            fileSpan[actualFileLen] = 0;

            int actualFuncLen = System.Text.Encoding.UTF8.GetBytes(function, funcSpan);
            funcSpan[actualFuncLen] = 0;

            int actualThreadLen = System.Text.Encoding.UTF8.GetBytes(threadName, threadSpan);
            threadSpan[actualThreadLen] = 0;

            fixed (byte* pMsg = msgSpan, pFile = fileSpan, pFunc = funcSpan, pThread = threadSpan)
            {
                var loc = new LogSourceLocationNative
                {
                    File = pFile,
                    Function = pFunc,
                    Line = (uint)line
                };

                Logger_Log_Internal(nativeLevel, pMsg, &loc, pThread);
            }
        }
        finally
        {
            if (msgBuffer != null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(msgBuffer);
            }
        }

        // Also keep Console.WriteLine for now as a fallback/easier debugging
        Console.WriteLine($"[{level}] {msgStr}");
    }

    public static void Clear()
    {
        MessageCleared?.Invoke();
    }

    public static bool Initialize(bool bindCallback = false)
    {
        bool ok = LoggerAPI.Logger_Initialize(bindCallback);
        if (bindCallback && m_ReceiveLog != null && ok)
        {
            var ptr = Marshal.GetFunctionPointerForDelegate(m_ReceiveLog);
            LoggerAPI.Logger_BindCallback(ptr);
        }

        return ok;
    }
}
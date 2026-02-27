using System.Diagnostics;
using System.Runtime.InteropServices;
using Arisen.Native.Diagnostics;

namespace ArisenEngine.Core.Diagnostics;

public static class Logger
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LogCallback(uint type, [MarshalAs(UnmanagedType.LPUTF8Str)] string msg, [MarshalAs(UnmanagedType.LPUTF8Str)] string threadName, [MarshalAs(UnmanagedType.LPUTF8Str)] string trace);

    private static LogCallback? m_ReceiveLog;

    internal static void RecordLog(uint type, string msg, string threadName, string trace)
    {
        var message = new LogMessage((LogLevel)type, msg, "0", threadName, DateTime.Now, trace);

        Task.Run(() =>
        {
            MessageAdded?.Invoke(message);
        });
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

    internal class LogMessage
    {
        public DateTime Time { get; }
        public LogLevel LogLevel { get; }
        public string Message { get; }
        public string ThreadId { get; }
        public string ThreadName { get; }
        public string StackTrace { get; } = string.Empty;

        public string FullLogString => $"[{Time}] [{LogLevel}] [ThreadId:{ThreadId}, ThreadName:{ThreadName}] \nMessage: {Message} \n" + (LogLevel == LogLevel.Log ? "" : StackTrace);
       
        internal LogMessage(LogLevel logLevel, string msg, string threadId, string threadName, DateTime time, string stackTrace)
        {
            Time = time;
            LogLevel = logLevel;
            Message = msg;
            ThreadId = threadId;
            ThreadName = threadName;
            StackTrace = stackTrace;
        }
    }

    internal static Action<LogMessage>? MessageAdded;
    internal static Action? MessageCleared;

    public static void Dispose()
    {
        LoggerAPI.Logger_Shutdown();
    }

    [Conditional("DEBUG")]
    public static void Assert(
        bool condition, 
        string message = "", 
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string function = "")
    {
        if (!condition)
        {
            // Optional: Call native assert if available in the future
            System.Diagnostics.Debug.Assert(condition, message);
        }
    }
    
    public static void Log(object msg) => WriteLog(LogLevel.Log, msg);
    public static void Info(object msg) => WriteLog(LogLevel.Info, msg);
    public static void Trace(object msg) => WriteLog(LogLevel.Trace, msg);
    public static void Warning(object msg) => WriteLog(LogLevel.Warning, msg);
    public static void Error(object msg) => WriteLog(LogLevel.Error, msg);
    public static void Fatal(object msg) => WriteLog(LogLevel.Fatal, msg);

    private static void WriteLog(LogLevel level, object msg)
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

        // For now, location info is empty strings as they are harder to pass from C# easily without overhead
        // But we could use [CallerFilePath] etc. if needed later.
        LoggerAPI.Logger_Log(nativeLevel, msg.ToString() ?? "", IntPtr.Zero, threadName);
        
        // Also keep Console.WriteLine for now as a fallback/easier debugging
        Console.WriteLine($"[{level}] {msg}");
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

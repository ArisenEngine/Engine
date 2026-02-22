using System.Diagnostics;
using ArisenBinding.Arisen.Diagnostics;

namespace ArisenEngine.Core.Diagnostics;

public static class Logger
{
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
        ArisenBinding.Arisen.Diagnostics.Logger.Shutdown();
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
            ArisenBinding.Arisen.Assertion.ReportAssertionFailure("condition", file, line, function, message);
            // Optionally also trigger a C# break if the native one doesn't stop execution
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
        string trace = Environment.StackTrace;
        string threadName = Thread.CurrentThread.Name ?? "MainThread";
        // NOTE: If native Logger doesn't have direct Log methods, we should add them via partial or PInvoke
        // For now, assume they exist or will be added to AutoBinding.
        // ArisenBinding.Arisen.Diagnostics.Logger.Instance.Log(msg.ToString(), threadName, trace);
    }

    public static void Clear()
    {
        MessageCleared?.Invoke();
    }

    public static bool Initialize(bool bindCallback = false)
    {
        var instance = ArisenBinding.Arisen.Diagnostics.Logger.Instance;
        if (bindCallback && m_ReceiveLog != null)
        {
            instance.BindCallback(m_ReceiveLog);
        }
        
        return instance.Initialize();
    }
}

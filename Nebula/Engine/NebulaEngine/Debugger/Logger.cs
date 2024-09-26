using System.Diagnostics;
using System.Runtime.InteropServices;
using NebulaEngine.Debugger;

namespace NebulaEngine.Debug;

public static class Logger
{
    internal delegate void OnLogReceived (LogLevel type, [MarshalAs(UnmanagedType.LPStr)]string msg, [MarshalAs(UnmanagedType.LPStr)]string threadId,[MarshalAs(UnmanagedType.LPStr)]string trace);
    
    internal static void RecordLog(uint type, string threadId, string msg, string trace)
    {
       
        string threadName = Thread.CurrentThread.Name;
        var message = new LogMessage((LogLevel)type, msg, threadId, threadName, DateTime.Now, trace);

        Task.Run(() =>
        {
            MessageAdded?.Invoke(message);
        });

    }
    
    internal static LogCallback ReceiveLog;
    static Logger()
    {
        ReceiveLog = new LogCallback(RecordLog);
        
    }
    
    
    
    internal enum LogLevel
    {
        /// <summary>
        /// finer-grained info for debugging
        /// </summary>
        Trace = 0x01, // 
        /// <summary>
        /// fine-grained info
        /// </summary>
        Log = 0x02,   // 
        /// <summary>
        /// coarse-grained info
        /// </summary>
        Info = 0x04, // 
        /// <summary>
        /// harmful situation info
        /// </summary>
        Warning = 0x08, // 
        /// <summary>
        /// errors but app can still run
        /// </summary>
        Error = 0x10, // 
        /// <summary>
        /// severe errors that will lead to abort
        /// </summary>
        Fatal = 0x20 // 
    }

    internal class LogMessage
    {
        public DateTime Time { get; }
        public LogLevel LogLevel { get; }
        public string Message { get; }
        public string ThreadId { get; }
        public string ThreadName { get; }
        public string StackTrace { get; } = string.Empty;

        public string FullLogString
        {
            get
            {
                return $"[{Time}] [{LogLevel}] [ThreadId:{ThreadId}, ThreadName:{ThreadName}] \nMessage: {Message} \n" + (LogLevel == Logger.LogLevel.Log ? "" : StackTrace);
            }
        }
       
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
        LoggerAPI.DebuggerShutdown();
    }

    [Conditional("DEBUG")]
    public static void Assert(bool condition)
    {
        System.Diagnostics.Debug.Assert(condition);
    }
    
    public static void Log(object msg)
    {
        string trace = Environment.StackTrace;
        LoggerAPI.DebuggerLog(msg.ToString(), Thread.CurrentThread.Name, trace);
        
    }
    
    public static void Info(object msg)
    {
        string trace = Environment.StackTrace;
        LoggerAPI.DebuggerInfo(msg.ToString(), Thread.CurrentThread.Name, trace);
        
    }

    public static void Trace(object msg)
    {
        string trace = Environment.StackTrace;
        LoggerAPI.DebuggerTrace(msg.ToString(), Thread.CurrentThread.Name, trace);
        
    }

    public static void Warning(object msg)
    {
        string trace = Environment.StackTrace;
        LoggerAPI.DebuggerWarning(msg.ToString(), Thread.CurrentThread.Name, trace);
    }
    
    public static void Error(object msg)
    {
        string trace = Environment.StackTrace;
        LoggerAPI.DebuggerError(msg.ToString(), Thread.CurrentThread.Name, trace);
    }

    public static void Fatal(object msg)
    {
        string trace = Environment.StackTrace;
        LoggerAPI.DebuggerFatal(msg.ToString(), Thread.CurrentThread.Name, trace);
    }

    public static void Clear()
    {
        MessageCleared?.Invoke();
    }

    public static bool Initialize(bool bindCallback = false)
    {
        LoggerAPI.DebuggerBindCallback(ReceiveLog);
        
        return LoggerAPI.DebuggerInitialize();
        
    }
    
}
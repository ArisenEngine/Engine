using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NebulaEngine.FileSystem;

namespace NebulaEngine.Debugger;

public static class Logger
{
    internal delegate void OnLogReceived (LogLevel type, [MarshalAs(UnmanagedType.LPStr)]string msg, [MarshalAs(UnmanagedType.LPStr)]string threadId,[MarshalAs(UnmanagedType.LPStr)]string trace);
    internal static void RecordLog(LogLevel type, string threadId, string msg, string trace)
    {
       
        string threadName = Thread.CurrentThread.Name;
        var message = new LogMessage(type, msg, threadId, threadName, DateTime.Now, trace);

        Task.Run(() =>
        {
            MessageAdded?.Invoke(message);
        });

    }
    
    internal static OnLogReceived ReceiveLog;
    static Logger()
    {
        ReceiveLog = new OnLogReceived(RecordLog);
        
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
        API.Debugger.Debugger_Flush();
    }

    public static void Log(object msg)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Log(msg.ToString(), Thread.CurrentThread.Name, trace);
        
    }
    
    public static void Info(object msg)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Info(msg.ToString(), Thread.CurrentThread.Name, trace);
        
    }

    public static void Trace(object msg)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Trace(msg.ToString(), Thread.CurrentThread.Name, trace);
        
    }

    public static void Warning(object msg)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Warning(msg.ToString(), Thread.CurrentThread.Name, trace);
    }
    
    public static void Error(object msg)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Error(msg.ToString(), Thread.CurrentThread.Name, trace);
    }

    public static void Fatal(object msg)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Fatal(msg.ToString(), Thread.CurrentThread.Name, trace);
    }

    public static void Clear()
    {
        MessageCleared?.Invoke();
    }

    public static bool Initialize(bool bindCallback = false)
    {
        API.Debugger.Debugger_BindCallback(ReceiveLog);
        
        return API.Debugger.Debugger_Initialize();
        
    }
    
}
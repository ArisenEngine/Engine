using System.Diagnostics;
using System.Runtime.CompilerServices;
using NebulaEngine.FileSystem;

namespace NebulaEngine.Debugger;

public static class Logger
{
    static Logger()
    {
     
    }
    
    internal enum MessageType
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
        public MessageType MessageType { get; }
        public string Message { get; }
        public string File { get; }
        public int Line { get; }
        public int ThreadId { get; }
        public string ThreadName { get; }
        public string Caller { get; }

        public string StackTrace { get; } = string.Empty;

        public string FullLogString
        {
            get
            {
                return $"[{Time}] [{MessageType}] [ThreadId:{ThreadId}, ThreadName:{ThreadName}] \nMessage: {Message} \n" + (MessageType == Logger.MessageType.Log ? "" : StackTrace);
            }
        }
       
        internal LogMessage(MessageType messageType, string msg, string file, string caller, int line, int threadId, string threadName, string stackTrace = "")
        {
            Time = DateTime.Now;
            MessageType = messageType;
            Message = msg;
            File = Path.GetFileName(file);
            Caller = caller;
            Line = line;
            ThreadId = threadId;
            ThreadName = threadName;
            StackTrace = stackTrace;
        }
    }

    internal static Action<LogMessage>? MessageAdded;
    internal static Action? MessageCleared;
    
    private static void DoWriteMessage(MessageType type, object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        int id = Thread.CurrentThread.ManagedThreadId;
        string threadName = Thread.CurrentThread.Name;

        string trace = Environment.StackTrace;

        Task.Run(() =>
        {
            var message = new LogMessage(type, msg.ToString(), file, caller, line, id, threadName, trace);
            //WriteMessage(message);

            MessageAdded?.Invoke(message);

        });
    }
    
    public static void Log(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Log(msg.ToString(), Thread.CurrentThread.Name, trace);
        //DoWriteMessage(MessageType.Log, msg, file, caller, line);
    }
    
    public static void Info(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Info(msg.ToString(), Thread.CurrentThread.Name, trace);
        //DoWriteMessage(MessageType.Info, msg, file, caller, line);
    }

    public static void Trace(object msg, [CallerFilePath] string file = "", [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Trace(msg.ToString(), Thread.CurrentThread.Name, trace);
        //DoWriteMessage(MessageType.Info, msg, file, caller, line);
    }

    public static void Warning(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Warning(msg.ToString(), Thread.CurrentThread.Name, trace);
        //DoWriteMessage(MessageType.Warning, msg, file, caller, line);
    }
    
    public static void Error(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Error(msg.ToString(), Thread.CurrentThread.Name, trace);
        //DoWriteMessage(MessageType.Error, msg, file, caller, line);
    }

    public static void Fatal(object msg, [CallerFilePath] string file = "", [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        string trace = Environment.StackTrace;
        API.Debugger.Debugger_Fatal(msg.ToString(), Thread.CurrentThread.Name, trace);
        //DoWriteMessage(MessageType.Error, msg, file, caller, line);
    }

    public static void Clear([CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        MessageCleared?.Invoke();
    }
    
}
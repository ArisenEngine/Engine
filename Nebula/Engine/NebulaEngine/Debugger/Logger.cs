using System.Diagnostics;
using System.Runtime.CompilerServices;
using NebulaEngine.FileSystem;

namespace NebulaEngine.Debugger;

public static class Logger
{
    public static string DebuggerLogPath = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "Debugger.log";
    static ReaderWriterLock locker = new ReaderWriterLock();
    static Logger()
    {
        locker.AcquireWriterLock(Int32.MaxValue);
        if (File.Exists(DebuggerLogPath))
        {
            File.Delete(DebuggerLogPath);
        }
        locker.ReleaseLock();
    }
    
    public enum MessageType
    {
        Log = 0x01,
        Info = 0x02,
        Warning = 0x04,
        Error = 0x08
    }

    public class LogMessage
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
       
        public LogMessage(MessageType messageType, string msg, string file, string caller, int line, int threadId, string threadName, string stackTrace = "")
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

    public static Action<LogMessage>? MessageAdded;
    public static Action? MessageCleared;
    
    private static void WriteMessage(LogMessage message)
    {
        try
        {
            MessageAdded?.Invoke(message);
            
            if (GameApplication.IsDesignMode)
            {
                return;
            }
            
            FileSystemUtilities.AppendTextToFile(message.FullLogString, DebuggerLogPath);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static void DoWriteMessage(MessageType type, object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        int id = Thread.CurrentThread.ManagedThreadId;
        string threadName = Thread.CurrentThread.Name;
        string trace = Environment.StackTrace;
        
        Task.Run(() =>
        {
            var message = new LogMessage(type, msg.ToString(), file, caller, line, id, threadName, trace);
            WriteMessage(message);
            
        });
    }
    
    public static void Log(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        DoWriteMessage(MessageType.Log, msg, file, caller, line);
    }
    
    public static void Info(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        DoWriteMessage(MessageType.Info, msg, file, caller, line);
    }
    
    public static void Warning(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        DoWriteMessage(MessageType.Warning, msg, file, caller, line);
    }
    
    public static void Error(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        DoWriteMessage(MessageType.Error, msg, file, caller, line);
    }
    
    public static void Clear([CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        int id = Thread.CurrentThread.ManagedThreadId;
        string threadName = Thread.CurrentThread.Name;
        string trace = Environment.StackTrace;
        
        Task.Run(() =>
        {
            MessageCleared?.Invoke();
            var message = new LogMessage(MessageType.Log, "Clear....", file, caller, line, id, threadName, trace);
            WriteMessage(message);
            
        });
    }
    
}
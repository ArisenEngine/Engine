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
        public string MetaData => $"{File}:{Caller}({Line})";

        public LogMessage(MessageType messageType, string msg, string file, string caller, int line, int threadId, string threadName)
        {
            Time = DateTime.Now;
            MessageType = messageType;
            Message = msg;
            File = Path.GetFileName(file);
            Caller = caller;
            Line = line;
            ThreadId = threadId;
            ThreadName = threadName;
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
            
            FileSystemUtilities.AppendTextToFile($"[{message.Time}] [{message.MessageType}] {message.Message} {message.MetaData}", DebuggerLogPath);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    public static async void Log(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        int id = Thread.CurrentThread.ManagedThreadId;
        string threadName = Thread.CurrentThread.Name;
        
        await Task.Run(() =>
        {
            var message = new LogMessage(MessageType.Log, msg.ToString(), file, caller, line, id, threadName);
            WriteMessage(message);
            
        });
    }
    
    public static async void Info(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        int id = Thread.CurrentThread.ManagedThreadId;
        string threadName = Thread.CurrentThread.Name;
        
        await Task.Run(() =>
        {
            var message = new LogMessage(MessageType.Info, msg.ToString(), file, caller, line, id, threadName);
            WriteMessage(message);
            
        });
    }
    
    public static async void Warning(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        int id = Thread.CurrentThread.ManagedThreadId;
        string threadName = Thread.CurrentThread.Name;
        
        await Task.Run(() =>
        {
            var message = new LogMessage(MessageType.Warning, msg.ToString(), file, caller, line, id, threadName);
            WriteMessage(message);
            
        });
    }
    
    public static async void Error(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        int id = Thread.CurrentThread.ManagedThreadId;
        string threadName = Thread.CurrentThread.Name;
        await Task.Run(() =>
        {
            var message = new LogMessage(MessageType.Error, msg.ToString(), file, caller, line, id, threadName);
            WriteMessage(message);
            
        });
    }
    
    public static async void Clear([CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        int id = Thread.CurrentThread.ManagedThreadId;
        string threadName = Thread.CurrentThread.Name;
        await Task.Run(() =>
        {
            MessageCleared?.Invoke();
            var message = new LogMessage(MessageType.Log, "Clear....", file, caller, line, id, threadName);
            WriteMessage(message);
            
        });
    }
    
}
using System.Runtime.CompilerServices;
using NebulaEngine.FileSystem;

namespace NebulaEngine.Debugger;

public static class Logger
{
    public static string EditorLogPath = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "Editor.log";

    static Logger()
    {
        if (File.Exists(EditorLogPath))
        {
            File.Delete(EditorLogPath);
        }
    }
    
    public enum MessageType
    {
        Log = 0x00,
        Info = 0x01,
        Warning = 0x02,
        Error = 0x03
    }

    public class LogMessage
    {
        public DateTime Time { get; }
        public MessageType MessageType { get; }
        public string Message { get; }
        public string File { get; }
        public int Line { get; }
        public string Caller { get; }
        public string MetaData => $"{File}:{Caller}({Line})";

        public LogMessage(MessageType messageType, string msg, string file, string caller, int line)
        {
            Time = DateTime.Now;
            MessageType = messageType;
            Message = msg;
            File = Path.GetFileName(file);
            Caller = caller;
            Line = line;
        }
    }

    public static Action<LogMessage>? MessageAdded;
    
    private static void WriteMessage(LogMessage message)
    {
        try
        {
            MessageAdded?.Invoke(message);
            FileSystemUtilities.AppendTextToFile($"[{message.Time}] [{message.MessageType}] {message.Message} {message.MetaData}", EditorLogPath);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    public static async void Log(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        await Task.Run(() =>
        {
            var message = new LogMessage(MessageType.Log, msg.ToString(), file, caller, line);
            WriteMessage(message);
            
        });
    }
    
    public static async void Info(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        await Task.Run(() =>
        {
            var message = new LogMessage(MessageType.Info, msg.ToString(), file, caller, line);
            WriteMessage(message);
            
        });
    }
    
    public static async void Warning(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        await Task.Run(() =>
        {
            var message = new LogMessage(MessageType.Warning, msg.ToString(), file, caller, line);
            WriteMessage(message);
            
        });
    }
    
    public static async void Error(object msg, [CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        await Task.Run(() =>
        {
            var message = new LogMessage(MessageType.Error, msg.ToString(), file, caller, line);
            WriteMessage(message);
            
        });
    }
    
    public static async void Clear([CallerFilePath]string file = "", [CallerMemberName]string caller = "", [CallerLineNumber]int line = 0)
    {
        await Task.Run(() =>
        {
            var message = new LogMessage(MessageType.Log, "Clear....", file, caller, line);
            WriteMessage(message);
            
        });
    }
    
}
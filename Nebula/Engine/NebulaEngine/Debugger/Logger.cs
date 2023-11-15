using System.Data;

namespace NebulaEngine.Debugger;

public static class Logger
{
    enum MessageType
    {
        Log = 0x00,
        Info = 0x01,
        Warning = 0x02,
        Error = 0x03
    }

    class LogMessage
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
    
    
}
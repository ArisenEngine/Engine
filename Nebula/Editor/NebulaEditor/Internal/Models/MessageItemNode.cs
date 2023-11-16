using System;
using NebulaEngine.Debugger;

namespace NebulaEditor.Models;

public class MessageItemNode
{
    public string MessageText
    {
        get
        {
            return m_Message.Message;
        }
    }

    public string ThreadText
    {
        get
        {
            return $"{m_Message.ThreadId}" + (string.IsNullOrEmpty(m_Message.ThreadName) ? "" : $":{m_Message.ThreadName}");
        }
    }

    public string TimeText
    {
        get
        {
            return $"[{m_Message.Time}]";
        }
    }

    public bool ShowFullMessage
    {
        get;
        set;
    } = false;

    public long Time => m_Message.Time.Ticks;

    public DateTime DateTime => m_Message.Time;

    public Logger.MessageType MessageType => m_Message.MessageType;
    
    private Logger.LogMessage m_Message;

    public MessageItemNode(Logger.LogMessage message)
    {
        m_Message = message;
    }
}
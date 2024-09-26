using System;
using System.Diagnostics;
using Avalonia.Media;
using NebulaEngine.Debugger;

namespace NebulaEditor.Models;

using LogMessage = NebulaEngine.Debug.Logger.LogMessage;
using LogLevel = NebulaEngine.Debug.Logger.LogLevel;

public class MessageItemNode
{
    public string StackTrace
    {
        get
        {
            return m_Message.StackTrace;
        }
    }
    public string FullText
    {
        get
        {
            return this.TimeText + this.ThreadId + this.ThreadName + this.MessageText;
        }
    }
    public string MessageText
    {
        get
        {
            return m_Message.Message;
        }
    }

    public string ThreadId
    {
        get
        {
            return $"{m_Message.ThreadId}";
        }
    }
    
    public string ThreadName
    {
        get
        {
            return $"{m_Message.ThreadName}";
        }
    }

    public string TimeText
    {
        get
        {
            return $"[{m_Message.Time.TimeOfDay}]";
        }
    }
    
    public DateTime DateTime => m_Message.Time;

    internal LogLevel LogLevel => m_Message.LogLevel;
    
    private LogMessage m_Message;

    internal MessageItemNode(LogMessage message)
    {
        m_Message = message;
    }
}
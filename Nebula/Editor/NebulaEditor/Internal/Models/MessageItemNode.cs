using System;
using Avalonia.Media;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NebulaEngine.Debugger;

namespace NebulaEditor.Models;

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

    internal Logger.MessageType MessageType => m_Message.MessageType;
    
    private Logger.LogMessage m_Message;

    internal MessageItemNode(Logger.LogMessage message)
    {
        m_Message = message;
    }
}
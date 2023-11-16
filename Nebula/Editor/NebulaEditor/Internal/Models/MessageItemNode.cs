using System;
using System.Collections.Generic;
using Avalonia.Controls.Templates;
using NebulaEngine.Debugger;
using ReactiveUI;

namespace NebulaEditor.Models;

public class MessageItemNode
{
    public string MessageText
    {
        get
        {
            return !ShowFullMessage ? $"[{m_Message.Time}]{m_Message.Message}" :  $"[{m_Message.Time}]{ThreadText}{m_Message.Message}";
        }
    }

    public string ThreadText
    {
        get
        {
            return $"[Thread:{m_Message.ThreadId},Name:{m_Message.ThreadName}]";
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
using System;
using System.Diagnostics;
using Avalonia.Media;
using ArisenEngine.Core.Diagnostics;

namespace ArisenEditor.Models;

using LogMessage = Logger.LogMessage;
using LogLevel = Logger.LogLevel;

internal enum LogSource
{
    Player,
    Editor
}

internal class MessageItemNode
{
    internal LogSource Source { get; }
    internal string StackTrace
    {
        get
        {
            return m_Message.StackTrace;
        }
    }
    internal string FullText
    {
        get
        {
            return this.TimeText + this.ThreadId + this.ThreadName + this.MessageText;
        }
    }
    internal string MessageText
    {
        get
        {
            return m_Message.Message;
        }
    }

    internal string ThreadId
    {
        get
        {
            return $"{m_Message.ThreadId}";
        }
    }
    
    internal string ThreadName
    {
        get
        {
            return $"{m_Message.ThreadName}";
        }
    }

    internal string TimeText
    {
        get
        {
            return $"[{m_Message.Time.TimeOfDay}]";
        }
    }
    
    internal DateTime DateTime => m_Message.Time;

    internal LogLevel LogLevel => m_Message.LogLevel;
    
    private LogMessage m_Message;

    internal MessageItemNode(LogMessage message, LogSource source = LogSource.Player)
    {
        m_Message = message;
        Source = source;
    }
}
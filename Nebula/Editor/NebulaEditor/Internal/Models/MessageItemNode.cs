using System.Collections.Generic;
using NebulaEngine.Debugger;
using ReactiveUI;

namespace NebulaEditor.Models;

public class MessageItemNode
{
    public string MessageText
    {
        get
        {
            return $"[{m_Message.Time}]{m_Message.Message}";
        }
    }

    public Logger.MessageType MessageType => m_Message.MessageType;
    
    private Logger.LogMessage m_Message;

    public MessageItemNode(Logger.LogMessage message)
    {
        m_Message = message;
    }
}
namespace ArisenEngine.ShaderLab;

using System;
using System.Collections.Generic;

public class Preprocessor
{
    private Dictionary<string, string> m_Entries = new();
    private Dictionary<string, bool> m_Defines = new Dictionary<string, bool>();
    private Stack<bool> m_ConditionalStack = new Stack<bool>();

    public Preprocessor()
    {
        m_ConditionalStack.Push(true); // 默认代码激活
    }

    public bool IsCodeActive() => m_ConditionalStack.Peek();

    public void ProcessDirective(string directive)
    {
        var line = directive.Trim();
        if (line.StartsWith("#define "))
        {
            var macro = line.Substring(8).Trim();
            m_Defines[macro] = true;
        }
        else if (line.StartsWith("#undef "))
        {
            var macro = line.Substring(7).Trim();
            m_Defines.Remove(macro);
        }
        else if (line.StartsWith("#ifdef "))
        {
            var macro = line.Substring(7).Trim();
            bool active = m_Defines.ContainsKey(macro);
            m_ConditionalStack.Push(m_ConditionalStack.Peek() && active);
        }
        else if (line.StartsWith("#ifndef "))
        {
            var macro = line.Substring(8).Trim();
            bool active = !m_Defines.ContainsKey(macro);
            m_ConditionalStack.Push(m_ConditionalStack.Peek() && active);
        }
        else if (line.StartsWith("#else"))
        {
            if (m_ConditionalStack.Count > 1)
            {
                bool prev = m_ConditionalStack.Pop();
                bool parent = m_ConditionalStack.Peek();
                m_ConditionalStack.Push(parent && !prev);
            }
        }
        else if (line.StartsWith("#endif"))
        {
            if (m_ConditionalStack.Count > 1)
                m_ConditionalStack.Pop();
        }
        // 其它预处理指令可扩展
    }
}
namespace ArisenEngine.ShaderLab;

using System;
using System.Collections.Generic;

public class HlslParser
{
    private Lexer m_Lexer;
    private Token m_Current;

    public HlslParser(string code)
    {
        m_Lexer = new Lexer(code);
        m_Current = m_Lexer.Next();
    }

    private void NextToken() => m_Current = m_Lexer.Next();

    private bool Match(TokenType type, string text = null)
    {
        if (m_Current.type != type)
            return false;
        if (text != null && m_Current.text != text)
            return false;
        return true;
    }

    private void Expect(TokenType type, string text = null)
    {
        if (m_Current.type != type || (text != null && m_Current.text != text))
            throw new Exception(
                $"Expected {type} '{text}', got {m_Current.type} '{m_Current.text}' at line {m_Current.line}");
        NextToken();
    }

    public List<HlslStruct> ParseStructs()
    {
        var structs = new List<HlslStruct>();

        while (m_Current.type != TokenType.EndOfFile)
        {
            if (Match(TokenType.Identifier, "struct"))
            {
                NextToken();
                if (Match(TokenType.Identifier))
                {
                    string structName = m_Current.text;
                    NextToken();
                    Expect(TokenType.Symbol, "{");
                    var members = ParseStructMembers();
                    Expect(TokenType.Symbol, "}");
                    structs.Add(new HlslStruct { name = structName, members = members });
                }
                else
                {
                    throw new Exception("Expected struct name after 'struct'");
                }
            }
            else
            {
                NextToken();
            }
        }

        return structs;
    }

    private List<HlslStructMember> ParseStructMembers()
    {
        var members = new List<HlslStructMember>();

        while (!Match(TokenType.Symbol, "}"))
        {
            if (Match(TokenType.Identifier))
            {
                string typeName = m_Current.text;
                NextToken();
                if (Match(TokenType.Identifier))
                {
                    string memberName = m_Current.text;
                    NextToken();
                    Expect(TokenType.Symbol, ";");
                    members.Add(new HlslStructMember { type = typeName, name = memberName });
                }
                else
                {
                    throw new Exception("Expected member name in struct");
                }
            }
            else
            {
                // 跳过非结构体成员Token
                NextToken();
            }
        }

        return members;
    }

    public List<HlslVariable> ParseVariables()
    {
        var vars = new List<HlslVariable>();

        m_Lexer = new Lexer(m_Lexer.Peek().text); // reset lexer to full code again
        m_Current = m_Lexer.Next();

        while (m_Current.type != TokenType.EndOfFile)
        {
            if (Match(TokenType.Identifier))
            {
                string typeName = m_Current.text;
                NextToken();
                if (Match(TokenType.Identifier))
                {
                    string varName = m_Current.text;
                    NextToken();

                    string reg = null;
                    if (Match(TokenType.Symbol, ":"))
                    {
                        NextToken();
                        if (Match(TokenType.Identifier))
                        {
                            reg = m_Current.text;
                            NextToken();
                        }
                    }

                    // 跳过剩余声明
                    while (!Match(TokenType.Symbol, ";") && m_Current.type != TokenType.EndOfFile)
                        NextToken();

                    if (Match(TokenType.Symbol, ";"))
                        NextToken();

                    vars.Add(new HlslVariable { type = typeName, name = varName, register = reg });
                }
                else
                {
                    NextToken();
                }
            }
            else
            {
                NextToken();
            }
        }

        return vars;
    }
}
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Serialization;

namespace ArisenEngine.ShaderLab;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public enum TokenType
{
    Identifier, // 变量名、关键词等（如 Blend, _SrcBlend）
    IntegerLiteral, // 整数，如 0, 1, 255
    FloatLiteral, // 浮点数，如 1.0, .5, 2.75
    StringLiteral, // 字符串（如 "MyTexture"）
    Symbol, // 符号（如 {}, (), [], =, ; 等）

    PreprocessorDirective, // 预处理指令（如 #include, #pragma）

    CommentLine, // 单行注释（如 // 注释）
    CommentBlock, // 多行注释（如 /* 注释 */）

    EndOfFile // 文件结尾标志
}

public class Token
{
    public TokenType type;
    public string text;

    public int line;

    // 起始字符索引与长度，用于基于源文本的区间切片
    public int start;
    public int length;
    public override string ToString() => $"{line}: {type} => {text}";
}

public class Lexer
{
    private static readonly Regex s_TokenRegex = new Regex(
        @"(?<whitespace>\s+)"
        + @"|(?<comment>//[^\r\n]*|/\*.*?\*/)"
        + @"|(?<preprocessor>#[^\r\n]+)"
        + @"|(?<string>""([^""\\]|\\.)*"")"
        + @"|(?<float>\d+\.\d+|\.\d+)" // 放在 int 前
        + @"|(?<int>\d+)"
        + @"|(?<identifier>[A-Za-z_][A-Za-z0-9_]*)"
        + @"|(?<symbol>[{}()\[\];:,<>.+\-*/=%&|^!~?])"
        , RegexOptions.Singleline | RegexOptions.Compiled);


    private readonly string k_Input;
    private int m_Position;
    private int _line = 1;

    private List<Token> m_Tokens = new List<Token>();
    private int m_Index = 0;

    public Lexer(string kInput)
    {
        k_Input = kInput;
        Tokenize();
    }

    // 返回原始源码在 [start, end) 区间的子串，用于保持原始格式的切片
    public string Slice(int start, int endExclusive)
    {
        if (start < 0) start = 0;
        if (endExclusive > k_Input.Length) endExclusive = k_Input.Length;
        if (endExclusive <= start) return string.Empty;
        return k_Input.Substring(start, endExclusive - start);
    }

    private void Tokenize()
    {
        while (m_Position < k_Input.Length)
        {
            var match = s_TokenRegex.Match(k_Input, m_Position);

            if (!match.Success || match.Index != m_Position)
            {
                Logger.Error(
                    $"Unrecognized token at position {m_Position}, near \"{PreviewText()}\" (line {_line})");
                break;
            }

            string value = match.Value;

            if (match.Groups["whitespace"].Success)
            {
                _line += CountNewlines(value);
            }
            else if (match.Groups["comment"].Success)
            {
                var type = value.StartsWith("//") ? TokenType.CommentLine : TokenType.CommentBlock;
                m_Tokens.Add(new Token
                    { type = type, text = value, line = _line, start = m_Position, length = value.Length });
                _line += CountNewlines(value);
            }
            else if (match.Groups["preprocessor"].Success)
            {
                m_Tokens.Add(new Token
                {
                    type = TokenType.PreprocessorDirective, text = value.Trim(), line = _line, start = m_Position,
                    length = value.Length
                });
                _line += CountNewlines(value);
            }
            else if (match.Groups["string"].Success)
            {
                m_Tokens.Add(new Token
                {
                    type = TokenType.StringLiteral, text = value, line = _line, start = m_Position,
                    length = value.Length
                });
                _line += CountNewlines(value);
            }
            else if (match.Groups["float"].Success)
            {
                m_Tokens.Add(new Token
                {
                    type = TokenType.FloatLiteral, text = value, line = _line, start = m_Position, length = value.Length
                });
                _line += CountNewlines(value);
            }
            else if (match.Groups["int"].Success)
            {
                m_Tokens.Add(new Token
                {
                    type = TokenType.IntegerLiteral, text = value, line = _line, start = m_Position,
                    length = value.Length
                });
                _line += CountNewlines(value);
            }
            else if (match.Groups["identifier"].Success)
            {
                m_Tokens.Add(new Token
                {
                    type = TokenType.Identifier, text = value, line = _line, start = m_Position, length = value.Length
                });
                _line += CountNewlines(value);
            }
            else if (match.Groups["symbol"].Success)
            {
                m_Tokens.Add(new Token
                    { type = TokenType.Symbol, text = value, line = _line, start = m_Position, length = value.Length });
                _line += CountNewlines(value);
            }
            else
            {
                Logger.Error($"[ShaderLab::Lexer] Unrecognized token at line {_line}, position {m_Position}");
                break;
            }

            m_Position += value.Length;
        }

        m_Tokens.Add(new Token
            { type = TokenType.EndOfFile, text = "<EOF>", line = _line + 1, start = k_Input.Length, length = 0 });

        // TODO: 测试用
        RemovePropertiesBlock();

        SerializationUtil.Serialize(m_Tokens, "tokens.token");
    }

    private void RemovePropertiesBlock()
    {
        for (int i = 0; i < m_Tokens.Count; i++)
        {
            var token = m_Tokens[i];
            if (token.type == TokenType.Identifier && token.text == "Properties")
            {
                // 期望下一个是 {
                if (i + 1 < m_Tokens.Count && m_Tokens[i + 1].type == TokenType.Symbol && m_Tokens[i + 1].text == "{")
                {
                    int startIndex = i;
                    int braceDepth = 0;
                    i++; // skip "Properties"

                    for (; i < m_Tokens.Count; i++)
                    {
                        var t = m_Tokens[i];
                        if (t.type == TokenType.Symbol)
                        {
                            if (t.text == "{") braceDepth++;
                            else if (t.text == "}") braceDepth--;

                            if (braceDepth == 0)
                            {
                                // 到达 }，删除 [startIndex, i] 区间
                                m_Tokens.RemoveRange(startIndex, i - startIndex + 1);
                                i = startIndex - 1; // reset i
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    private string PreviewText(int maxLen = 20)
    {
        int len = Math.Min(maxLen, k_Input.Length - m_Position);
        return k_Input.Substring(m_Position, len).Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private int CountNewlines(string s)
    {
        int count = 0;
        foreach (var c in s)
            if (c == '\n')
                count++;
        return count;
    }

    public Token Peek(int lookahead = 0)
    {
        int idx = m_Index + lookahead;
        if (idx >= m_Tokens.Count)
            return m_Tokens[m_Tokens.Count - 1];
        return m_Tokens[idx];
    }

    public Token Next()
    {
        if (m_Index >= m_Tokens.Count)
            return m_Tokens[m_Tokens.Count - 1];
        return m_Tokens[m_Index++];
    }

    public bool Match(TokenType type, string text = null)
    {
        var t = Peek();
        if (t.type != type)
            return false;
        if (text != null && t.text != text)
            return false;
        return true;
    }

    public Token Expect(TokenType type, string text = null)
    {
        var t = Next();
        if (t.type != type || (text != null && t.text != text))
            Logger.Error($"Expected token {type} '{text}', got {t.type} '{t.text}' at line {t.line}");
        return t;
    }
}
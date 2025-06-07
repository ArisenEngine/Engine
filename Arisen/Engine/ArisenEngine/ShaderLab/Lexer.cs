namespace ArisenEngine.ShaderLab;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public enum TokenType
{
    Identifier,
    Number,
    StringLiteral,
    Symbol,
    PreprocessorDirective,
    CommentLine,
    CommentBlock,
    EndOfFile,
}

public class Token
{
    public TokenType type;
    public string text;
    public int line;
    public override string ToString() => $"{type}: {text}";
}

public class Lexer
{
    private static readonly Regex s_TokenRegex = new Regex(
        @"(?<whitespace>\s+)"
        + @"|(?<comment>//[^\r\n]*|/\*.*?\*/)"
        + @"|(?<preprocessor>#[^\r\n]+)"
        + @"|(?<string>""([^""\\]|\\.)*"")"
        + @"|(?<number>\d+(\.\d+)?)"
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

    private void Tokenize()
    {
        var matches = s_TokenRegex.Matches(k_Input);
        foreach (Match m in matches)
        {
            if (m.Groups["whitespace"].Success)
            {
                // Count lines in whitespace
                _line += CountNewlines(m.Value);
                continue;
            }
            else if (m.Groups["comment"].Success)
            {
                var txt = m.Value;
                var type = txt.StartsWith("//") ? TokenType.CommentLine : TokenType.CommentBlock;
                m_Tokens.Add(new Token { type = type, text = txt, line = _line });
                _line += CountNewlines(txt);
                continue;
            }
            else if (m.Groups["preprocessor"].Success)
            {
                m_Tokens.Add(new Token { type = TokenType.PreprocessorDirective, text = m.Value.Trim(), line = _line });
                _line += CountNewlines(m.Value);
                continue;
            }
            else if (m.Groups["string"].Success)
            {
                m_Tokens.Add(new Token { type = TokenType.StringLiteral, text = m.Value, line = _line });
                _line += CountNewlines(m.Value);
                continue;
            }
            else if (m.Groups["number"].Success)
            {
                m_Tokens.Add(new Token { type = TokenType.Number, text = m.Value, line = _line });
                _line += CountNewlines(m.Value);
                continue;
            }
            else if (m.Groups["identifier"].Success)
            {
                m_Tokens.Add(new Token { type = TokenType.Identifier, text = m.Value, line = _line });
                _line += CountNewlines(m.Value);
                continue;
            }
            else if (m.Groups["symbol"].Success)
            {
                m_Tokens.Add(new Token { type = TokenType.Symbol, text = m.Value, line = _line });
                _line += CountNewlines(m.Value);
                continue;
            }
            else
            {
                throw new Exception($"Unrecognized token at line {_line}");
            }
        }
        m_Tokens.Add(new Token { type = TokenType.EndOfFile, text = "<EOF>", line = _line });
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
            throw new Exception($"Expected token {type} '{text}', got {t.type} '{t.text}' at line {t.line}");
        return t;
    }
}

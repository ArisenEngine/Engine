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
        while (m_Position < k_Input.Length)
        {
            var match = s_TokenRegex.Match(k_Input, m_Position);

            if (!match.Success || match.Index != m_Position)
            {
                throw new Exception(
                    $"Unrecognized token at position {m_Position}, near \"{PreviewText()}\" (line {_line})");
            }

            string value = match.Value;

            if (match.Groups["whitespace"].Success)
            {
                _line += CountNewlines(value);
            }
            else if (match.Groups["comment"].Success)
            {
                var type = value.StartsWith("//") ? TokenType.CommentLine : TokenType.CommentBlock;
                m_Tokens.Add(new Token { type = type, text = value, line = _line });
                _line += CountNewlines(value);
            }
            else if (match.Groups["preprocessor"].Success)
            {
                m_Tokens.Add(new Token { type = TokenType.PreprocessorDirective, text = value.Trim(), line = _line });
                _line += CountNewlines(value);
            }
            else if (match.Groups["string"].Success)
            {
                m_Tokens.Add(new Token { type = TokenType.StringLiteral, text = value, line = _line });
                _line += CountNewlines(value);
            }
            else if (match.Groups["number"].Success)
            {
                m_Tokens.Add(new Token { type = TokenType.Number, text = value, line = _line });
                _line += CountNewlines(value);
            }
            else if (match.Groups["identifier"].Success)
            {
                m_Tokens.Add(new Token { type = TokenType.Identifier, text = value, line = _line });
                _line += CountNewlines(value);
            }
            else if (match.Groups["symbol"].Success)
            {
                m_Tokens.Add(new Token { type = TokenType.Symbol, text = value, line = _line });
                _line += CountNewlines(value);
            }
            else
            {
                Debug.Logger.Error($"[ShaderLab::Lexer] Unrecognized token at line {_line}, position {m_Position}");
                break;
            }

            m_Position += value.Length;
        }

        m_Tokens.Add(new Token { type = TokenType.EndOfFile, text = "<EOF>", line = _line + 1 });
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
            Debug.Logger.Error($"Expected token {type} '{text}', got {t.type} '{t.text}' at line {t.line}");
        return t;
    }
}
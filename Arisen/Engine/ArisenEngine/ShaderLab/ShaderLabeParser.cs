namespace ArisenEngine.ShaderLab;

using System;
using System.Collections.Generic;
using System.Text;

public class ShaderLabParser
{
    private Lexer m_Lexer;
    private Preprocessor m_Preprocessor;

    public ShaderLabParser(string code)
    {
        m_Lexer = new Lexer(code);
        m_Preprocessor = new Preprocessor();
    }

    private Token Current => m_Lexer.Peek();
    private Token Next() => m_Lexer.Next();
    private bool Match(TokenType type, string text = null) => m_Lexer.Match(type, text);
    private Token Expect(TokenType type, string text = null) => m_Lexer.Expect(type, text);

    public ShaderLabShader ParseShader()
    {
        var shader = new ShaderLabShader();

        while (!Match(TokenType.EndOfFile))
        {
            if (Match(TokenType.PreprocessorDirective))
            {
                var directive = Next().text;
                m_Preprocessor.ProcessDirective(directive);
                continue;
            }

            if (!m_Preprocessor.IsCodeActive())
            {
                Next();
                continue;
            }

            if (Match(TokenType.Identifier, "Shader"))
            {
                Next();
                shader.name = ParseStringOrIdentifier();
                Expect(TokenType.Symbol, "{");

                while (!Match(TokenType.Symbol, "}"))
                {
                    if (Match(TokenType.Identifier, "Properties"))
                    {
                        Next();
                        shader.properties = ParseProperties();
                    }
                    else if (Match(TokenType.Identifier, "SubShader"))
                    {
                        Next();
                        Expect(TokenType.Symbol, "{");
                        var subShader = ParseSubShader();
                        shader.subShaders.Add(subShader);
                        Expect(TokenType.Symbol, "}");
                    }
                    else
                    {
                        // 其它块忽略或跳过
                        SkipUnknownBlockOrToken();
                    }
                }
                Expect(TokenType.Symbol, "}");
                break;
            }
            else
            {
                Next();
            }
        }

        return shader;
    }

    private string ParseStringOrIdentifier()
    {
        if (Match(TokenType.StringLiteral))
        {
            var tok = Next();
            return tok.text.Trim('"');
        }
        else if (Match(TokenType.Identifier))
        {
            return Next().text;
        }
        else
        {
            throw new Exception($"Expected shader name string or identifier at line {Current.line}");
        }
    }

    private List<Property> ParseProperties()
    {
        var list = new List<Property>();
        Expect(TokenType.Symbol, "{");

        while (!Match(TokenType.Symbol, "}"))
        {
            if (!m_Preprocessor.IsCodeActive())
            {
                Next();
                continue;
            }

            if (Match(TokenType.Identifier))
            {
                var prop = new Property();
                prop.name = Next().text;
                prop.type = Expect(TokenType.Identifier).text;
                prop.displayName = Expect(TokenType.StringLiteral).text.Trim('"');
                var defaultValueSb = new StringBuilder();
                while (!Match(TokenType.Symbol, "}"))
                {
                    var tok = Next();
                    if (tok.type == TokenType.Symbol && tok.text == "}")
                        break;
                    defaultValueSb.Append(tok.text);
                    if (Match(TokenType.Symbol, ")"))
                        break;
                }
                prop.defaultValue = defaultValueSb.ToString();
                list.Add(prop);
            }
            else
            {
                Next();
            }
        }

        Expect(TokenType.Symbol, "}");
        return list;
    }

    private SubShader ParseSubShader()
    {
        var subShader = new SubShader();
        while (!Match(TokenType.Symbol, "}"))
        {
            if (!m_Preprocessor.IsCodeActive())
            {
                Next();
                continue;
            }

            if (Match(TokenType.Identifier, "Pass"))
            {
                Next();
                Expect(TokenType.Symbol, "{");
                var pass = ParsePass();
                subShader.passes.Add(pass);
                Expect(TokenType.Symbol, "}");
            }
            else if (Match(TokenType.Identifier, "Tags"))
            {
                Next();
                Expect(TokenType.Symbol, "{");
                // 简单读取所有字符串作为tag
                var tags = new List<string>();
                while (!Match(TokenType.Symbol, "}"))
                {
                    if (Match(TokenType.StringLiteral))
                        tags.Add(Next().text.Trim('"'));
                    else
                        Next();
                }
                subShader.tags = tags;
                Expect(TokenType.Symbol, "}");
            }
            else
            {
                Next();
            }
        }
        return subShader;
    }

    private Pass ParsePass()
    {
        var pass = new Pass();
        var sb = new StringBuilder();

        int braceDepth = 1;
        while (braceDepth > 0)
        {
            var tok = m_Lexer.Next();
            if (tok.type == TokenType.Symbol)
            {
                if (tok.text == "{")
                    braceDepth++;
                else if (tok.text == "}")
                    braceDepth--;
            }
            if (braceDepth > 0)
                sb.Append(tok.text + (tok.type == TokenType.Symbol ? " " : ""));
        }

        pass.hlslCode = sb.ToString();

        // 解析HLSL代码结构体和变量
        var hlslParser = new HlslParser(pass.hlslCode);
        pass.hlslStructs = hlslParser.ParseStructs();
        pass.variables = hlslParser.ParseVariables();

        return pass;
    }

    private void SkipUnknownBlockOrToken()
    {
        if (Match(TokenType.Symbol, "{"))
        {
            int depth = 1;
            Next();
            while (depth > 0)
            {
                var tok = Next();
                if (tok.type == TokenType.Symbol)
                {
                    if (tok.text == "{") depth++;
                    else if (tok.text == "}") depth--;
                }
            }
        }
        else
        {
            Next();
        }
    }
}

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

    public ShaderLabShader ParseGraphicsShader()
    {
        var shader = new ShaderLabShader();
        while (!Match(TokenType.EndOfFile))
        {
            if (Match(TokenType.PreprocessorDirective))
            {
                ProcessDirective();
                continue;
            }

            if (Match(TokenType.Identifier, "Shader"))
            {
                ProcessShader(shader);
            }
            else
            {
                Debug.Logger.Error($"Unexpected token {Current.type}, content: {Current.text} at line {Current.line}.");
                break;
            }
        }

        return shader;
    }

    private void ProcessShader(ShaderLabShader shader)
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
            else if (Match(TokenType.Identifier, "HLSLINCLUDE"))
            {
                Next();
                var includedHlsl = new IncludedHLSL()
                {
                    passIndex = -1,
                    subShaderIndex = -1
                };
                var hlslCode = new StringBuilder();
                ParseHlslCode(hlslCode);
                includedHlsl.hlslCode = hlslCode.ToString();
                shader.includedHLSLs.Add(includedHlsl);
            }
            else if (Match(TokenType.Identifier, "Fallback"))
            {
                // TODO
                Next();
                var fallback = Current.text;
                Debug.Logger.Info($"Get Fallback Info:{fallback}");
                Next();
            }
            else
            {
                Debug.Logger.Error($"[ShaderLabParser] Unexpected identifier: {Current.text} at line {Current.line} ");
                break;
            }
        }
        Expect(TokenType.Symbol, "}");
    }


    private void ProcessDirective()
    {
        var directive = Next().text;
        m_Preprocessor.ProcessDirective(directive);
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
        }
        return subShader;
    }

    private Pass ParsePass()
    {
        var pass = new Pass();
        var sb = new StringBuilder();

        while (!Match(TokenType.Symbol, "}"))
        {
            // TODO:
            Next();
        }

        return pass;
    }

    private void ParseHlslCode(StringBuilder hlslCode)
    {
        while (!Match(TokenType.Identifier, "ENDHLSL"))
        {
            while (Match(TokenType.PreprocessorDirective))
            {
                var directive = Next().text;
                m_Preprocessor.ProcessDirective(directive);
            }

            if (Match(TokenType.Identifier))
            {
                hlslCode.Append(Current.text);
                if (m_Lexer.Peek(1).type != TokenType.Symbol)
                {
                    hlslCode.Append(' ');
                }
            }
            else if (Match(TokenType.CommentBlock) || Match(TokenType.CommentLine))
            {
                Next();
                continue;
            }
            else if (Match(TokenType.Number) || Match(TokenType.Symbol))
            {
                hlslCode.Append(Current.text);
            }
            else
            {
                Debug.Logger.Error($"[ShaderLabParser] Unexpected identifier: {Current.text} in HLSL Block at line {Current.line}");
                break;
            }

            Next(); 
        }
        
        Next(); 
        
    }
}
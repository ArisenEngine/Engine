namespace ArisenEngine.ShaderLab;

using System;
using System.Collections.Generic;
using System.Text;

public class ShaderLabParser
{
    private Lexer m_Lexer;
    private Preprocessor m_Preprocessor;
    private bool m_HasErrors = false;

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
            else if (Match(TokenType.CommentLine) || Match(TokenType.CommentBlock))
            {
                Next();
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
            else if (Match(TokenType.CommentBlock) || Match(TokenType.CommentLine))
            {
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

    // TODO: to remove
    private List<Property> ParseProperties()
    {
        var list = new List<Property>();
        Expect(TokenType.Symbol, "{");

        while (!Match(TokenType.Symbol, "}"))
        {
            Next();
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
            else if (Match(TokenType.CommentBlock) || Match(TokenType.CommentLine))
            {
                Next();
            }
            else if (Match(TokenType.Identifier, "LOD"))
            {
                Next();
                // TODO: get shader lod
                Next();
            }
            else if (Match(TokenType.Identifier, "Blend"))
            {
                Next();
                // Step 1: 主颜色混合源因子
                var srcColor = ParseRenderStateFactor(); // 支持 [xxx] 和直接写关键字

                // Step 2: 主颜色混合目标因子
                var dstColor = ParseRenderStateFactor();

                // Step 3: 判断是否还有 Alpha 混合参数
                RenderStateValue? srcAlpha = null;
                RenderStateValue? dstAlpha = null;
                if (Match(TokenType.Symbol, ","))
                {
                    Next();
                    srcAlpha = ParseRenderStateFactor();
                    dstAlpha = ParseRenderStateFactor();
                }
                
                Debug.Logger.Info($"[ShaderLabParser] Processing blend factor: " +
                                  $"srcColor={srcColor}," +
                                  $" dstColor={dstColor}," +
                                  $" srcAlpha={srcAlpha}, " +
                                  $"dstAlpha={dstAlpha}");
            }
            else if (Match(TokenType.Identifier, "ZWrite"))
            {
                
            }
            else
            {
                Debug.Logger.Error($" [ShaderLabParser] Unexpected identifier: {Current.text} at line {Current.line}");
            }
            
        }
        return subShader;
    }

    RenderStateValue ParseRenderStateFactor()
    {
        if (Match(TokenType.Symbol, "["))
        {
            Next();
            // 开始解析引用
            var identifierToken = Expect(TokenType.Identifier);
            Expect(TokenType.Symbol, "]");

            return new RenderStateValue
            {
                isReference = true,
                referenceName = identifierToken.text
            };
        }
        
        // 直接关键字，如 One, SrcAlpha, Zero
        var valueToken = Next();

        if (valueToken.type == TokenType.Identifier)
        {
            return new RenderStateValue
            {
                isReference = false,
                stringValue = valueToken.text,
                kind = RenderStateValue.ValueKind.String
            };
        }

        if (valueToken.type == TokenType.FloatLiteral)
        {
            return new RenderStateValue()
            {
                isReference = false,
                floatValue = float.Parse(valueToken.text),
                kind = RenderStateValue.ValueKind.Float
            };
        }

        if (valueToken.type == TokenType.IntegerLiteral)
        {
            return new RenderStateValue()
            {
                isReference = false,
                intValue = int.Parse(valueToken.text),
                kind = RenderStateValue.ValueKind.Int
            };
        }
        
        Debug.Logger.Error($"[ShaderLabParser] Unexpected token type: {valueToken.type}, value: {valueToken.text}, line {Current.line}");
        return null;
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
            else if (Match(TokenType.IntegerLiteral) || Match(TokenType.Symbol) || Match(TokenType.FloatLiteral))
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
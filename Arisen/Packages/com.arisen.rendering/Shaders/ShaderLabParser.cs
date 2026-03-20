using ArisenEngine.Core.Diagnostics;

namespace ArisenEngine.ShaderLab;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class ShaderLabParser
{
    private Lexer m_Lexer;
    private Preprocessor m_Preprocessor;
    private SubShader _lastParsedSubShader = null;
    private readonly string m_BaseDir = string.Empty;

    public ShaderLabParser(string code)
    {
        m_Lexer = new Lexer(code);
        m_Preprocessor = new Preprocessor();
    }

    public ShaderLabParser(string code, string baseDirectory)
    {
        m_Lexer = new Lexer(code);
        m_Preprocessor = new Preprocessor();
        m_BaseDir = baseDirectory ?? string.Empty;
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
                Logger.Error($"Unexpected token {Current.type}, content: {Current.text} at line {Current.line}.");
                break;
            }
        }

        // Post-process include content root rewrite if base dir is provided
        try
        {
            if (!string.IsNullOrWhiteSpace(m_BaseDir))
            {
                var includeParents = shader.subShaders
                    .SelectMany(s => s.passes)
                    .SelectMany(p => p.includedHLSLs)
                    .Distinct()
                    .ToArray();
                var contentRoot = FindIncludeContentRoot(m_BaseDir, includeParents);
                if (!string.IsNullOrWhiteSpace(contentRoot))
                {
                    foreach (var sub in shader.subShaders)
                    foreach (var p in sub.passes)
                        p.includedHLSLs = new List<string> { contentRoot };
                }
            }
        }
        catch
        {
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
                _lastParsedSubShader = subShader;
                shader.subShaders.Add(subShader);
                Expect(TokenType.Symbol, "}");
            }
            else if (Match(TokenType.Identifier, "HLSLINCLUDE"))
            {
                var startTok = Next();
                int sliceStart = startTok.start + startTok.length;
                var includedHlsl = new IncludedHLSL()
                {
                    passIndex = -1,
                    subShaderIndex = -1
                };
                while (!Match(TokenType.Identifier, "ENDHLSL"))
                {
                    if (Match(TokenType.EndOfFile))
                    {
                        Logger.Error("Unexpected EOF while parsing HLSLINCLUDE block");
                        break;
                    }

                    if (Match(TokenType.PreprocessorDirective))
                    {
                        var directiveTok = Next();
                        m_Preprocessor.ProcessDirective(directiveTok.text);
                        continue;
                    }

                    Next();
                }

                var endTok = Expect(TokenType.Identifier, "ENDHLSL");
                int sliceEnd = endTok.start;
                includedHlsl.hlslCode = m_Lexer.Slice(sliceStart, sliceEnd);
                shader.includedHLSLs.Add(includedHlsl);
            }
            else if (Match(TokenType.Identifier, "CustomEditor"))
            {
                // 引擎外的编辑器扩展字段，不参与运行时解析，跳过
                Next();
                if (Match(TokenType.StringLiteral) || Match(TokenType.Identifier))
                    Next();
            }
            else if (Match(TokenType.Identifier, "Fallback") || Match(TokenType.Identifier, "FallBack"))
            {
                // 顶层 Fallback/FallBack 语句：记录并跳过
                Next();
                string fb = string.Empty;
                if (Match(TokenType.StringLiteral) || Match(TokenType.Identifier))
                {
                    fb = Next().text.Trim('"');
                }

                Logger.Info($"Get Fallback Info:{fb}");
                continue;
            }
            else if (Match(TokenType.CommentBlock) || Match(TokenType.CommentLine))
            {
                Next();
            }
            else
            {
                Logger.Error($"[ShaderLabParser] Unexpected identifier: {Current.text} at line {Current.line} ");
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
            else if (Match(TokenType.Identifier, "HLSLINCLUDE"))
            {
                // SubShader-level include block; capture raw code and store for later prepend
                var startTok = Next();
                int sliceStart = startTok.start + startTok.length;
                while (!Match(TokenType.Identifier, "ENDHLSL"))
                {
                    if (Match(TokenType.EndOfFile))
                    {
                        Logger.Error("Unexpected EOF while parsing SubShader HLSLINCLUDE block");
                        break;
                    }

                    if (Match(TokenType.PreprocessorDirective))
                    {
                        var directiveTok = Next();
                        m_Preprocessor.ProcessDirective(directiveTok.text);
                        continue;
                    }

                    Next();
                }

                var endTok = Expect(TokenType.Identifier, "ENDHLSL");
                int sliceEnd = endTok.start;
                var code = m_Lexer.Slice(sliceStart, sliceEnd);
                subShader.includeHlslCodes.Add(code);
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
            // 处理 SubShader 级别常见渲染状态指令（常见渲染管线写法）
            else if (Match(TokenType.Identifier, "Blend"))
            {
                // Blend [_SrcBlend] [_DstBlend] [, One One]
                Next();
                var _ = ParseRenderStateFactor();
                _ = ParseRenderStateFactor();
                if (Match(TokenType.Symbol, ","))
                {
                    Next();
                    _ = ParseRenderStateFactor();
                    _ = ParseRenderStateFactor();
                }
            }
            else if (Match(TokenType.Identifier, "BlendOp"))
            {
                // SubShader 级别：仅解析并跳过（当前不存储到 SubShader）
                // BlendOp Add[, Sub]
                Next();
                if (Match(TokenType.Identifier) || Match(TokenType.Symbol, "["))
                {
                    var _ = ParseRenderStateFactor();
                    if (Match(TokenType.Symbol, ","))
                    {
                        Next();
                        _ = ParseRenderStateFactor();
                    }
                }
            }
            else if (Match(TokenType.Identifier, "ZWrite"))
            {
                Next();
                if (Match(TokenType.Identifier) || Match(TokenType.Symbol, "["))
                {
                    var _ = ParseRenderStateFactor();
                }
            }
            else if (Match(TokenType.Identifier, "ZTest"))
            {
                Next();
                if (Match(TokenType.Identifier)) Next();
            }
            else if (Match(TokenType.Identifier, "Cull"))
            {
                Next();
                if (Match(TokenType.Identifier) || Match(TokenType.Symbol, "["))
                {
                    var _ = ParseRenderStateFactor();
                }
            }
            else if (Match(TokenType.Identifier, "AlphaToMask"))
            {
                // SubShader 级别：仅解析并跳过
                Next();
                if (Match(TokenType.Identifier) || Match(TokenType.Symbol, "["))
                {
                    var _ = ParseRenderStateFactor();
                }
            }
            else if (Match(TokenType.Identifier, "Offset"))
            {
                // SubShader 级别：仅解析并跳过
                // Offset a,b 或 [_Factor],[_Units]
                Next();
                if (Match(TokenType.Symbol, "[") || Match(TokenType.Identifier) || Match(TokenType.IntegerLiteral) ||
                    Match(TokenType.FloatLiteral))
                {
                    var _ = ParseRenderStateFactor();
                    if (Match(TokenType.Symbol, ","))
                    {
                        Next();
                        _ = ParseRenderStateFactor();
                    }
                }
            }
            else if (Match(TokenType.Identifier, "ColorMask"))
            {
                Next();
                if (Match(TokenType.Identifier) || Match(TokenType.IntegerLiteral)) Next();
            }
            else if (Match(TokenType.Identifier, "Stencil"))
            {
                Next();
                // 捕获并丢弃子块
                _ = CaptureRawBracedBlock();
            }
            else
            {
                // 其他未知标识，跳过避免卡住
                Logger.Info($"[ShaderLabParser] Skip token at SubShader: {Current.text} line {Current.line}");
                Next();
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

        Logger.Error(
            $"[ShaderLabParser] Unexpected token type: {valueToken.type}, value: {valueToken.text}, line {Current.line}");
        return null;
    }

    private Pass ParsePass()
    {
        // 解析 Pass 块
        // 目标：
        // - Name/Tags
        // - Render States: Blend/Cull/ZWrite/ZTest/ColorMask/Stencil
        // - HLSLPROGRAM .. ENDHLSL 代码块
        // - #pragma vertex/fragment/.../target/multi_compile/shader_feature

        var pass = new Pass();

        while (!Match(TokenType.Symbol, "}"))
        {
            if (Match(TokenType.CommentBlock) || Match(TokenType.CommentLine))
            {
                Next();
                continue;
            }

            // Name "ForwardBase"
            if (Match(TokenType.Identifier, "Name"))
            {
                Next();
                if (Match(TokenType.StringLiteral) || Match(TokenType.Identifier))
                {
                    pass.name = Match(TokenType.StringLiteral) ? Next().text.Trim('"') : Next().text;
                }

                continue;
            }

            // Tags { "LightMode" = "ForwardBase" }
            if (Match(TokenType.Identifier, "Tags"))
            {
                Next();
                Expect(TokenType.Symbol, "{");
                pass.tags = ParseTagsDictionary();
                Expect(TokenType.Symbol, "}");
                continue;
            }

            // Blend src dst [, srcA dstA]
            if (Match(TokenType.Identifier, "Blend"))
            {
                Next();
                var srcColor = ParseRenderStateFactor();
                var dstColor = ParseRenderStateFactor();
                RenderStateValue? srcAlpha = null;
                RenderStateValue? dstAlpha = null;
                if (Match(TokenType.Symbol, ","))
                {
                    Next();
                    srcAlpha = ParseRenderStateFactor();
                    dstAlpha = ParseRenderStateFactor();
                }

                pass.states.Blend = new BlendState
                    { SrcColor = srcColor, DstColor = dstColor, SrcAlpha = srcAlpha, DstAlpha = dstAlpha };
                continue;
            }

            // Cull Back/Front/Off
            if (Match(TokenType.Identifier, "Cull"))
            {
                Next();
                if (Match(TokenType.Identifier))
                    pass.states.Cull = Next().text;
                continue;
            }

            // ZWrite On/Off
            if (Match(TokenType.Identifier, "ZWrite"))
            {
                Next();
                if (Match(TokenType.Identifier))
                    pass.states.ZWrite = Next().text;
                continue;
            }

            // ZTest LEqual/Less/.../Always
            if (Match(TokenType.Identifier, "ZTest"))
            {
                Next();
                if (Match(TokenType.Identifier))
                    pass.states.ZTest = Next().text;
                continue;
            }

            // ColorMask RGBA/0/None
            if (Match(TokenType.Identifier, "ColorMask"))
            {
                Next();
                if (Match(TokenType.Identifier))
                    pass.states.ColorMask = Next().text;
                else if (Match(TokenType.IntegerLiteral))
                    pass.states.ColorMask = Next().text;
                continue;
            }

            // Stencil { ... }（暂存原始文本）
            if (Match(TokenType.Identifier, "Stencil"))
            {
                Next();
                pass.states.StencilRaw = CaptureRawBracedBlock();
                continue;
            }

            // HLSLPROGRAM ... ENDHLSL（基于源文本切片，并移除特定工具链指令行）
            if (Match(TokenType.Identifier, "HLSLPROGRAM"))
            {
                var startTok = Next();
                int sliceStart = startTok.start + startTok.length;
                var removed = new List<(int s, int e)>();
                while (!Match(TokenType.Identifier, "ENDHLSL"))
                {
                    if (Match(TokenType.EndOfFile))
                    {
                        Logger.Error("Unexpected EOF while parsing HLSLPROGRAM block");
                        break;
                    }

                    if (Match(TokenType.PreprocessorDirective))
                    {
                        var directiveTok = Next();
                        var directive = directiveTok.text;
                        if (directive.StartsWith("#pragma "))
                        {
                            ProcessPragma(directive, pass);
                            removed.Add((directiveTok.start, directiveTok.start + directiveTok.length));
                        }
                        else
                        {
                            m_Preprocessor.ProcessDirective(directive);
                            if (directive.StartsWith("#include_with_pragmas "))
                            {
                                var path = ExtractIncludePath(directive);
                                if (!string.IsNullOrEmpty(path))
                                {
                                    var dir = ExtractIncludeParentDir(path);
                                    if (!string.IsNullOrEmpty(dir)) pass.includedHLSLs.Add(dir);
                                }

                                removed.Add((directiveTok.start, directiveTok.start + directiveTok.length));
                            }
                            else if (directive.StartsWith("#include "))
                            {
                                var path = ExtractIncludePath(directive);
                                if (!string.IsNullOrEmpty(path))
                                {
                                    var dir = ExtractIncludeParentDir(path);
                                    if (!string.IsNullOrEmpty(dir)) pass.includedHLSLs.Add(dir);
                                }
                                // 保留 include 语句在源码中，这样 dxc 可按 -I 解析
                                // 因此这里不将其移除
                            }
                        }

                        continue;
                    }

                    Next();
                }

                var endTok = Expect(TokenType.Identifier, "ENDHLSL");
                int sliceEnd = endTok.start;
                var raw = m_Lexer.Slice(sliceStart, sliceEnd);
                var body = RemoveSlices(raw, sliceStart, removed);
                // Prepend SubShader-level HLSLINCLUDE code and inline #include content previously tracked
                var sb = new StringBuilder();
                // try to locate nearest SubShader to grab its include blocks
                // (parser structure guarantees current pass belongs to last parsed SubShader)
                if (_lastParsedSubShader != null && _lastParsedSubShader.includeHlslCodes.Count > 0)
                {
                    foreach (var incCode in _lastParsedSubShader.includeHlslCodes)
                        sb.AppendLine(incCode);
                }

                // Inline pass-level previously captured include code if any existed at root shader (global HLSLINCLUDE)
                // Note: shader.includedHLSLs 保持原样，不在此处内联
                sb.Append(body);
                pass.hlslCode = sb.ToString();
                continue;
            }

            // 其它未覆盖标识，先跳过，避免死循环
            Next();
        }

        return pass;
    }

    // 解析 Tags 字典：支持 "Key" = "Value"，或无等号的连续字符串（退化为列表合并为原文）
    private Dictionary<string, string> ParseTagsDictionary()
    {
        var dict = new Dictionary<string, string>();
        while (!Match(TokenType.Symbol, "}"))
        {
            if (Match(TokenType.StringLiteral) || Match(TokenType.Identifier))
            {
                var keyTok = Next();
                string key = keyTok.type == TokenType.StringLiteral ? keyTok.text.Trim('"') : keyTok.text;
                if (Match(TokenType.Symbol, "="))
                {
                    Next();
                    if (Match(TokenType.StringLiteral) || Match(TokenType.Identifier))
                    {
                        var valTok = Next();
                        string val = valTok.type == TokenType.StringLiteral ? valTok.text.Trim('"') : valTok.text;
                        dict[key] = val;
                    }
                }
                else
                {
                    // 不带等号，记录为自身
                    dict[key] = string.Empty;
                }
            }
            else
            {
                Next();
            }
        }

        return dict;
    }

    // 捕获 { ... } 的原始文本（包括嵌套），用于 Stencil 等复杂块的占位保存
    private string CaptureRawBracedBlock()
    {
        var sb = new StringBuilder();
        Expect(TokenType.Symbol, "{");
        sb.Append("{");
        int depth = 1;
        while (depth > 0 && !Match(TokenType.EndOfFile))
        {
            var tok = Next();
            sb.Append(tok.text);
            if (tok.type == TokenType.Symbol)
            {
                if (tok.text == "{") depth++;
                else if (tok.text == "}") depth--;
            }
        }

        return sb.ToString();
    }

    // 处理 #pragma 指令，提取入口与目标、宏切换等
    private void ProcessPragma(string directive, Pass pass)
    {
        // 形如：#pragma vertex VSMain
        //      #pragma fragment PSMain
        //      #pragma geometry GSMain
        //      #pragma hull HSMain
        //      #pragma domain DSMain
        //      #pragma target ps_6_8
        //      #pragma multi_compile KEY1 KEY2
        //      #pragma shader_feature FEATURE_A FEATURE_B

        var line = directive.Trim();
        var parts = line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return;

        string kind = parts[1];
        if (kind == "vertex" && parts.Length >= 3)
            pass.vertexEntry = parts[2];
        else if (kind == "fragment" && parts.Length >= 3)
            pass.fragmentEntry = parts[2];
        else if (kind == "geometry" && parts.Length >= 3)
            pass.geometryEntry = parts[2];
        else if (kind == "hull" && parts.Length >= 3)
            pass.hullEntry = parts[2];
        else if (kind == "domain" && parts.Length >= 3)
            pass.domainEntry = parts[2];
        else if (kind == "target" && parts.Length >= 3)
            pass.target = parts[2];
        else if (kind == "multi_compile" && parts.Length >= 3)
        {
            for (int i = 2; i < parts.Length; i++)
                pass.multiCompile.Add(parts[i]);
        }
        else if (kind == "shader_feature" && parts.Length >= 3)
        {
            for (int i = 2; i < parts.Length; i++)
                pass.shaderFeature.Add(parts[i]);
        }
    }

    // 从预处理指令中提取 include 路径（支持 "path" 或 <path> 形式）
    private string ExtractIncludePath(string directive)
    {
        int quoteStart = directive.IndexOf('"');
        if (quoteStart >= 0)
        {
            int quoteEnd = directive.IndexOf('"', quoteStart + 1);
            if (quoteEnd > quoteStart) return directive.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        int lt = directive.IndexOf('<');
        if (lt >= 0)
        {
            int gt = directive.IndexOf('>', lt + 1);
            if (gt > lt) return directive.Substring(lt + 1, gt - lt - 1);
        }

        return string.Empty;
    }

    // 提取包含路径的父级目录（保持相对路径风格，统一使用正斜杠）
    private string ExtractIncludeParentDir(string include)
    {
        if (string.IsNullOrEmpty(include)) return string.Empty;
        var norm = include.Replace('\\', '/');
        int idx = norm.LastIndexOf('/');
        if (idx <= 0) return string.Empty;
        return norm.Substring(0, idx);
    }

    // 根据 pass.includedHLSLs 中的父级相对路径，推断通用的内容根（起点目录中向上查找包含该首段目录名的最近祖先）
    private static string? FindIncludeContentRoot(string startDir, IEnumerable<string> includeParents)
    {
        try
        {
            string anchor = includeParents
                .Select(p => (p ?? string.Empty).Replace('\\', '/'))
                .Where(p => p.Contains('/'))
                .Select(p => p.Split('/')[0])
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(anchor))
            {
                var dir = new DirectoryInfo(startDir);
                for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
                {
                    if (dir.GetDirectories(anchor).Length > 0)
                        return dir.FullName;
                }
            }
        }
        catch
        {
        }

        try
        {
            var anchor = includeParents
                .Select(p => (p ?? string.Empty).Replace('\\', '/'))
                .Where(p => p.Contains('/'))
                .Select(p => p.Split('/')[0])
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(anchor))
            {
                var probe = startDir.Replace('\\', '/');
                var token = "/" + anchor + "/";
                var idx = probe.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (idx > 0)
                {
                    return probe.Substring(0, idx).Replace('/', Path.DirectorySeparatorChar);
                }
            }
        }
        catch
        {
        }

        return null;
    }

    // 根据全局源码起点偏移，移除若干 (start,end) 片段，保持其它内容原样
    private string RemoveSlices(string rawSlice, int sliceGlobalStart, List<(int s, int e)> globalRanges)
    {
        if (string.IsNullOrEmpty(rawSlice) || globalRanges == null || globalRanges.Count == 0)
            return rawSlice;

        globalRanges.Sort((a, b) => a.s.CompareTo(b.s));
        var sb = new StringBuilder(rawSlice.Length);
        int cursorGlobal = sliceGlobalStart;
        foreach (var (s, e) in globalRanges)
        {
            int sClamped = Math.Max(s, sliceGlobalStart);
            int eClamped = Math.Min(e, sliceGlobalStart + rawSlice.Length);
            if (eClamped <= sClamped) continue;

            // 追加 [cursor, sClamped)
            int localStart = cursorGlobal - sliceGlobalStart;
            int localEnd = sClamped - sliceGlobalStart;
            if (localEnd > localStart)
                sb.Append(rawSlice, localStart, localEnd - localStart);

            // 跳过 [sClamped, eClamped)
            cursorGlobal = eClamped;
        }

        // 追加剩余部分
        int tailLocalStart = cursorGlobal - sliceGlobalStart;
        if (tailLocalStart < rawSlice.Length)
            sb.Append(rawSlice, tailLocalStart, rawSlice.Length - tailLocalStart);

        return sb.ToString();
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
                Logger.Error(
                    $"[ShaderLabParser] Unexpected identifier: {Current.text} in HLSL Block at line {Current.line}");
                break;
            }

            Next();
        }

        Next();
    }
}
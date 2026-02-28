using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BindingGenerator;

internal static class Program
{
    // ========================================================================
    // Arisen Engine Binding Generator
    // ========================================================================
    // Scans C++ headers for ARISEN_BIND_* annotation macros and generates
    // clean C# P/Invoke code. No CppSharp, no libclang, no post-processing.
    // ========================================================================

    static string s_SourceCode = "";
    static string s_Output = "";
    static string s_ProjectName = "AutoBinding";
    static readonly string s_GenerationTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    static void Main(string[] args)
    {
        ParseArguments(args);

        if (string.IsNullOrEmpty(s_SourceCode) || string.IsNullOrEmpty(s_Output))
        {
            Console.WriteLine("Usage: BindingGenerator --source_code <path> --output <path> [--library <path>]");
            Console.WriteLine("  --source_code  Root directory containing C++ headers");
            Console.WriteLine("  --output       Root directory for generated C# output");
            return;
        }

        var outputDir = Path.Combine(s_Output, s_ProjectName);
        Console.WriteLine($"Source: {s_SourceCode}");
        Console.WriteLine($"Output: {outputDir}");

        // Clean previous output (skip bin/obj)
        if (Directory.Exists(outputDir))
        {
            CleanOutputDirectory(outputDir);
        }
        else
        {
            Directory.CreateDirectory(outputDir);
        }

        // Scan all headers
        // Scan both .h and .cpp files (bridges are in .cpp)
        var headers = Directory.GetFiles(s_SourceCode, "*.h", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(s_SourceCode, "*.cpp", SearchOption.AllDirectories))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "3rdparty" + Path.DirectorySeparatorChar))
            .ToList();

        Console.WriteLine($"Scanning {headers.Count} source files...");

        int generatedFiles = 0;
        var allGeneratedFiles = new List<string>();

        foreach (var header in headers)
        {
            // Skip the macro definitions file itself
            if (Path.GetFileName(header) == "BindingMacros.h")
                continue;

            var content = File.ReadAllText(header);

            // Skip files without binding annotations
            if (!content.Contains("ARISEN_BIND_"))
                continue;

            var relativePath = Path.GetRelativePath(s_SourceCode, header);
            Console.WriteLine($"  Processing: {relativePath}");

            var results = ProcessHeader(content, header);
            foreach (var (fileName, csContent, subDir) in results)
            {
                var targetDir = string.IsNullOrEmpty(subDir) ? outputDir : Path.Combine(outputDir, subDir);
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);
                var outPath = Path.Combine(targetDir, fileName);
                File.WriteAllText(outPath, csContent);
                var displayPath = string.IsNullOrEmpty(subDir) ? fileName : $"{subDir}/{fileName}";
                allGeneratedFiles.Add(displayPath);
                generatedFiles++;
                Console.WriteLine($"    Generated: {displayPath}");
            }
        }

        // Generate project file
        GenerateProjectFile(outputDir);

        // Generate UTF8Marshaller (always needed)
        GenerateMarshaller(outputDir);

        Console.WriteLine($"\nGeneration complete: {generatedFiles} file(s) generated.");
    }

    // ====================================================================
    // Header Processing
    // ====================================================================

    /// <summary>Compute subdirectory from namespace (e.g. "Arisen.Native.RHI" → "RHI").</summary>
    static string GetSubDirFromNamespace(string csNamespace)
    {
        // "Arisen.Native.HAL" → root (backward compat)
        // "Arisen.Native.RHI" → "RHI"
        // "Arisen.Native.ShaderCompiler" → root (backward compat for existing files)
        var parts = csNamespace.Split('.');
        if (parts.Length >= 3)
        {
            var last = parts[^1];
            if (last == "HAL" || last == "ShaderCompiler")
                return ""; // keep at root for backward compatibility
            return last;
        }
        return "";
    }

    static List<(string FileName, string Content, string SubDir)> ProcessHeader(string content, string headerPath)
    {
        var results = new List<(string, string, string)>();

        // Extract module-level metadata
        var dllName = ExtractMacroArg(content, "ARISEN_BIND_MODULE") ?? "";
        var csNamespace = ExtractMacroArg(content, "ARISEN_BIND_NAMESPACE") ?? "";

        // --- Bridge Blocks (Phase 3) ---
        // Bridges have their own metadata in the BEGIN_BRIDGE macro
        var bridgeBlocks = ParseBridgeBlocks(content);

        // If neither module metadata nor bridge blocks are found, check if we have a namespace for enums/structs
        if (string.IsNullOrEmpty(dllName) && bridgeBlocks.Count == 0 && string.IsNullOrEmpty(csNamespace))
            return results;

        var subDir = !string.IsNullOrEmpty(csNamespace) ? GetSubDirFromNamespace(csNamespace) : "";

        // --- Enums ---
        if (!string.IsNullOrEmpty(csNamespace))
        {
            var enums = ParseEnums(content);
            foreach (var e in enums)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"// Auto-generated by Arisen BindingGenerator ({s_GenerationTime}) — DO NOT EDIT");
                sb.AppendLine();
                sb.AppendLine($"namespace {csNamespace}");
                sb.AppendLine("{");
                sb.AppendLine($"    public enum {e.Name} : {MapEnumBaseType(e.BaseType)}");
                sb.AppendLine("    {");
                foreach (var (name, value) in e.Values)
                {
                    if (value != null)
                        sb.AppendLine($"        {name} = {value},");
                    else
                        sb.AppendLine($"        {name},");
                }
                sb.AppendLine("    }");
                sb.AppendLine("}");

                results.Add(($"{e.Name}.cs", sb.ToString(), subDir));
            }

            // --- Structs ---
            var structs = ParseStructs(content);
            foreach (var s in structs)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"// Auto-generated by Arisen BindingGenerator ({s_GenerationTime}) — DO NOT EDIT");
                sb.AppendLine("using System.Runtime.InteropServices;");
                sb.AppendLine();
                sb.AppendLine($"namespace {csNamespace}");
                sb.AppendLine("{");
                sb.AppendLine("    [StructLayout(LayoutKind.Sequential)]");
                sb.AppendLine($"    public struct {s.Name}");
                sb.AppendLine("    {");
                foreach (var (type, name) in s.Fields)
                {
                    var csType = MapType(type);
                    var csName = ToPascalCase(name);
                    sb.AppendLine($"        public {csType} {csName};");
                }
                sb.AppendLine("    }");
                sb.AppendLine("}");

                results.Add(($"{s.Name}.cs", sb.ToString(), subDir));
            }

            // --- Handles ---
            var handles = ParseHandles(content);
            if (handles.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"// Auto-generated by Arisen BindingGenerator ({s_GenerationTime}) — DO NOT EDIT");
                sb.AppendLine("using System;");
                sb.AppendLine("using System.Runtime.InteropServices;");
                sb.AppendLine();
                sb.AppendLine($"namespace {csNamespace}");
                sb.AppendLine("{");

                foreach (var handleName in handles)
                {
                    sb.AppendLine("    [StructLayout(LayoutKind.Sequential)]");
                    sb.AppendLine($"    public struct {handleName}");
                    sb.AppendLine("    {");
                    sb.AppendLine("        public uint Index;");
                    sb.AppendLine("        public uint Generation;");
                    sb.AppendLine("        public bool IsValid => Index != 0xFFFFFFFFu;");
                    sb.AppendLine($"        public static readonly {handleName} Invalid = new {handleName} {{ Index = 0xFFFFFFFF, Generation = 0 }};");
                    sb.AppendLine("    }");
                    sb.AppendLine();
                }

                sb.AppendLine("}");
                results.Add(("RHIHandle.cs", sb.ToString(), subDir));
            }
        }

        // --- Extern "C" functions ---
        var externFuncs = ParseExternCFunctions(content);

        if (bridgeBlocks.Count > 0)
        {
            foreach (var block in bridgeBlocks)
            {
                var blockSubDir = GetSubDirFromNamespace(block.Namespace);

                // --- P/Invoke API class (internal) ---
                var sb = new StringBuilder();
                sb.AppendLine($"// Auto-generated by Arisen BindingGenerator ({s_GenerationTime}) — DO NOT EDIT");
                sb.AppendLine("using System;");
                sb.AppendLine("using System.Runtime.InteropServices;");
                sb.AppendLine("using System.Security;");
                sb.AppendLine();
                sb.AppendLine($"namespace {block.Namespace}");
                sb.AppendLine("{");
                sb.AppendLine("    using Arisen.Native.RHI;");
                sb.AppendLine("    using Arisen.Native.ShaderCompiler;");
                sb.AppendLine();
                sb.AppendLine($"    public static class {block.ClassName}API");
                sb.AppendLine("    {");
                sb.AppendLine($"        private const string DllName = \"{block.DllName}\";");
                sb.AppendLine();

                foreach (var func in block.Functions)
                {
                    EmitPInvokeFunction(sb, func, "DllName");
                }

                sb.AppendLine("    }");
                sb.AppendLine("}");

                results.Add(($"{block.ClassName}API.cs", sb.ToString(), blockSubDir));

                // --- OOP Wrapper class ---
                var wrapperContent = EmitOopWrapper(block);
                if (!string.IsNullOrEmpty(wrapperContent))
                {
                    results.Add(($"{block.ClassName}.cs", wrapperContent, blockSubDir));
                }
            }
        }
        else if (externFuncs.Count > 0)
        {
            // Standalone extern "C" functions (like RenderWindowAPI)
            var className = Path.GetFileNameWithoutExtension(headerPath);
            var sb = new StringBuilder();
            sb.AppendLine($"// Auto-generated by Arisen BindingGenerator ({s_GenerationTime}) — DO NOT EDIT");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("using System.Security;");
            sb.AppendLine();
            sb.AppendLine($"namespace {csNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("    using Arisen.Native.RHI;");
            sb.AppendLine();
            sb.AppendLine($"    public static class {className}");
            sb.AppendLine("    {");
            sb.AppendLine($"        private const string DllName = \"{dllName}\";");
            sb.AppendLine();

            foreach (var func in externFuncs)
            {
                EmitPInvokeFunction(sb, func, "DllName");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            results.Add(($"{className}.cs", sb.ToString(), subDir));
        }

        return results;
    }

    // ====================================================================
    // P/Invoke Emitter
    // ====================================================================

    static void EmitPInvokeFunction(StringBuilder sb, FunctionInfo func, string dllConst)
    {
        var csReturnType = MapType(func.ReturnType);
        var csParams = new List<string>();

        foreach (var (type, name) in func.Parameters)
        {
            var csType = MapType(type);
            var marshalAttr = GetMarshalAttribute(type);
            if (!string.IsNullOrEmpty(marshalAttr))
                csParams.Add($"{marshalAttr} {csType} {name}");
            else
                csParams.Add($"{csType} {name}");
        }

        sb.AppendLine($"        [SuppressUnmanagedCodeSecurity, DllImport({dllConst}, CallingConvention = CallingConvention.Cdecl)]");
        sb.AppendLine($"        public static extern {csReturnType} {func.Name}({string.Join(", ", csParams)});");
        sb.AppendLine();
    }

    // ====================================================================
    // Parsers
    // ====================================================================

    static string? ExtractMacroArg(string content, string macroName)
    {
        var match = Regex.Match(content, $@"{macroName}\s*\(\s*""([^""]+)""\s*\)");
        return match.Success ? match.Groups[1].Value : null;
    }

    // --- Enum Parser ---

    record EnumInfo(string Name, string BaseType, List<(string Name, string? Value)> Values);

    static List<EnumInfo> ParseEnums(string content)
    {
        var results = new List<EnumInfo>();

        // Pattern 1: enum class Name : Type { ... };
        var pattern1 = @"ARISEN_BIND_ENUM\s*\(\s*(\w+)\s*\)\s*enum\s+class\s+(\w+)\s*:\s*(\w+)\s*\{([^}]+)\}";
        foreach (Match m in Regex.Matches(content, pattern1, RegexOptions.Singleline))
        {
            var name = m.Groups[2].Value;
            var baseType = m.Groups[3].Value;
            var body = m.Groups[4].Value;
            results.Add(new EnumInfo(name, baseType, ParseEnumBody(body)));
        }

        // Pattern 2: typedef enum Name { ... } Name; (C-style, used by all RHI enums)
        var pattern2 = @"ARISEN_BIND_ENUM\s*\(\s*(\w+)\s*\)\s*(?:///[^\n]*\n\s*)?typedef\s+enum\s+(\w+)\s*\{([^}]+)\}\s*\w+\s*;";
        foreach (Match m in Regex.Matches(content, pattern2, RegexOptions.Singleline))
        {
            var name = m.Groups[2].Value;
            var body = m.Groups[3].Value;
            // C-style enums default to int, but Vulkan values fit in uint
            results.Add(new EnumInfo(name, "int", ParseEnumBody(body)));
        }

        // Pattern 3: enum class Name { ... }; (no explicit base type)
        var pattern3 = @"ARISEN_BIND_ENUM\s*\(\s*(\w+)\s*\)\s*enum\s+class\s+(\w+)\s*\{([^}]+)\}";
        foreach (Match m in Regex.Matches(content, pattern3, RegexOptions.Singleline))
        {
            var name = m.Groups[2].Value;
            // Skip if already matched by pattern1
            if (results.Any(r => r.Name == name)) continue;
            var body = m.Groups[3].Value;
            results.Add(new EnumInfo(name, "int", ParseEnumBody(body)));
        }

        return results;
    }

    static List<(string Name, string? Value)> ParseEnumBody(string body)
    {
        var values = new List<(string, string?)>();
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.Trim().TrimEnd(',').Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//") || trimmed.StartsWith("#"))
                continue;

            if (trimmed.Contains('='))
            {
                var parts = trimmed.Split('=', 2);
                values.Add((parts[0].Trim(), parts[1].Trim()));
            }
            else
            {
                values.Add((trimmed, null));
            }
        }
        return values;
    }

    // --- Struct Parser ---

    record StructInfo(string Name, List<(string Type, string Name)> Fields);

    static List<StructInfo> ParseStructs(string content)
    {
        var results = new List<StructInfo>();

        var pattern = @"ARISEN_BIND_STRUCT\s*\(\s*(\w+)\s*\)\s*(?:typedef\s+)?struct\s+(\w+)\s*\{(.*?)[\r\n]+\s*\}\s*;";
        foreach (Match m in Regex.Matches(content, pattern, RegexOptions.Singleline))
        {
            var name = m.Groups[2].Value;
            var body = m.Groups[3].Value;

            var fields = new List<(string, string)>();
            // Use Singleline to allow matching across lines in the body
            var fieldPattern = @"((?:const\s+)?[\w:\*]+(?:\s+const)?)\s+([\w]+)[^;]*;";
            var resultsInBody = Regex.Matches(body, fieldPattern, RegexOptions.Singleline);
            foreach (Match fieldMatch in resultsInBody)
            {
                var type = fieldMatch.Groups[1].Value.Trim();
                var fieldName = fieldMatch.Groups[2].Value.Trim();
                fields.Add((type, fieldName));
            }

            results.Add(new StructInfo(name, fields));
        }

        return results;
    }

    // --- Extern "C" Function Parser ---

    record FunctionInfo(string ReturnType, string Name, List<(string Type, string Name)> Parameters);

    static List<FunctionInfo> ParseExternCFunctions(string content)
    {
        var results = new List<FunctionInfo>();

        // Find extern "C" blocks
        var externCPattern = @"extern\s+""C""\s*\{([^}]+(?:\{[^}]*\}[^}]*)*)\}";
        foreach (Match blockMatch in Regex.Matches(content, externCPattern, RegexOptions.Singleline))
        {
            var block = blockMatch.Groups[1].Value;
            ParseFunctionsInBlock(block, results);
        }

        // Also match single-line extern "C" declarations
        var singlePattern = @"extern\s+""C""\s+\w+\s+(\w[\w\s\*]*?)\s+(\w+)\s*\(([^)]*)\)\s*;";
        foreach (Match m in Regex.Matches(content, singlePattern))
        {
            // Skip functions with C++ reference types — not C-ABI compatible
            if (m.Value.Contains("&&") || m.Groups[3].Value.Contains("&"))
                continue;

            var func = ParseFunctionSignature(m.Value);
            if (func != null)
                results.Add(func);
        }

        return results;
    }

    static void ParseFunctionsInBlock(string block, List<FunctionInfo> results)
    {
        // Match function declarations: EXPORT_MACRO ReturnType FunctionName(params);
        var funcPattern = @"(?:\w+_DLL\s+)?(\w[\w\s\*]*?)\s+(\w+)\s*\(([^)]*)\)\s*(?:;|{)";
        foreach (Match m in Regex.Matches(block, funcPattern))
        {
            var retType = m.Groups[1].Value.Trim();
            var name = m.Groups[2].Value.Trim();
            var paramsStr = m.Groups[3].Value.Trim();

            // Skip functions with C++ reference types (&&, &) — not C-ABI compatible
            if (paramsStr.Contains("&&") || paramsStr.Contains("&"))
                continue;

            // Skip DLL export macro names that look like return types
            if (retType.EndsWith("_DLL"))
            {
                // The actual return type is missing, this is probably "EXPORT void func()"
                // Re-parse: DLL_MACRO RetType Name(...)
                var reParse = Regex.Match(m.Value, @"\w+_DLL\s+(\w[\w\s\*]*?)\s+(\w+)\s*\(([^)]*)\)\s*;");
                if (reParse.Success)
                {
                    retType = reParse.Groups[1].Value.Trim();
                    name = reParse.Groups[2].Value.Trim();
                    paramsStr = reParse.Groups[3].Value.Trim();
                }
            }

            // Skip if name looks like a macro
            if (name.Contains("_DLL") || name == "dummy_core_hal_function")
                continue;

            var parameters = ParseParameters(paramsStr);
            results.Add(new FunctionInfo(retType, name, parameters));
        }
    }

    static FunctionInfo? ParseFunctionSignature(string signature)
    {
        var m = Regex.Match(signature, @"extern\s+""C""\s+\w+\s+(\w[\w\s\*]*?)\s+(\w+)\s*\(([^)]*)\)\s*;");
        if (!m.Success) return null;

        return new FunctionInfo(
            m.Groups[1].Value.Trim(),
            m.Groups[2].Value.Trim(),
            ParseParameters(m.Groups[3].Value.Trim())
        );
    }

    static List<(string Type, string Name)> ParseParameters(string paramsStr)
    {
        var parameters = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(paramsStr) || paramsStr == "void")
            return parameters;

        foreach (var param in paramsStr.Split(','))
        {
            var p = param.Trim();
            if (string.IsNullOrWhiteSpace(p)) continue;

            // Handle default values: Type x = 0 or Type x { 0 }
            // We want to stop at the first space before the name, but default values can have spaces too.
            // Let's strip default values first.
            if (p.Contains('='))
                p = p[..p.IndexOf('=')].Trim();
            
            // Handle { nullptr } etc.
            if (p.Contains('{'))
                p = p[..p.IndexOf('{')].Trim();

            // Find the last word as the parameter name
            var lastSpace = p.LastIndexOf(' ');
            if (lastSpace < 0) continue;

            var type = p[..lastSpace].Trim();
            var name = p[(lastSpace + 1)..].Trim();

            // Handle namespace-qualified types: RHI::EProgramStage → EProgramStage
            if (type.Contains("::"))
            {
                var lastColons = type.LastIndexOf("::");
                type = type[(lastColons + 2)..];
            }

            // Handle case where * is attached to name: "Type *name"
            if (name.StartsWith("*"))
            {
                type += "*";
                name = name[1..];
            }

            parameters.Add((type, name));
        }

        return parameters;
    }

    // --- Handle Parser ---

    static List<string> ParseHandles(string content)
    {
        var results = new List<string>();
        var pattern = @"ARISEN_BIND_HANDLE\s*\(\s*(\w+)\s*\)";
        foreach (Match m in Regex.Matches(content, pattern))
        {
            results.Add(m.Groups[1].Value);
        }
        return results;
    }

    // --- Bridge Block Parser ---

    record BridgeBlock(string ClassName, string DllName, string Namespace, List<FunctionInfo> Functions);

    static List<BridgeBlock> ParseBridgeBlocks(string content)
    {
        var results = new List<BridgeBlock>();

        var pattern = @"ARISEN_BIND_BEGIN_BRIDGE\s*\(\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""\s*\)(.*?)ARISEN_BIND_END_BRIDGE\s*\(\s*\)";
        foreach (Match m in Regex.Matches(content, pattern, RegexOptions.Singleline))
        {
            var className = m.Groups[1].Value;
            var dllName = m.Groups[2].Value;
            var ns = m.Groups[3].Value;
            var body = m.Groups[4].Value;

            var functions = new List<FunctionInfo>();
            ParseFunctionsInBlock(body, functions);
            if (functions.Count > 0)
                results.Add(new BridgeBlock(className, dllName, ns, functions));
        }

        return results;
    }

    static string? EmitOopWrapper(BridgeBlock block)
    {
        // Object-oriented wrapper generator (stub for now, as requested in Phase 3)
        // Returning null will skip generating the wrapper .cs file but allow the static API to work.
        return null;
    }

    // ====================================================================
    // Type Mapping
    // ====================================================================

    static readonly Dictionary<string, string> s_TypeMap = new(StringComparer.Ordinal)
    {
        ["void"]            = "void",
        ["void*"]           = "IntPtr",
        ["const void*"]     = "IntPtr",
        ["bool"]            = "bool",
        ["uint8_t"]         = "byte",
        ["UInt8"]           = "byte",
        ["int8_t"]          = "sbyte",
        ["SInt8"]           = "sbyte",
        ["uint16_t"]        = "ushort",
        ["UInt16"]          = "ushort",
        ["int16_t"]         = "short",
        ["SInt16"]          = "short",
        ["uint32_t"]        = "uint",
        ["UInt32"]          = "uint",
        ["int32_t"]         = "int",
        ["SInt32"]          = "int",
        ["uint64_t"]        = "ulong",
        ["UInt64"]          = "ulong",
        ["int64_t"]         = "long",
        ["SInt64"]          = "long",
        ["float"]           = "float",
        ["Float32"]         = "float",
        ["double"]          = "double",
        ["size_t"]          = "nuint",
        ["SIZE_T"]          = "nuint",
        ["const char*"]     = "string",
        ["const wchar_t*"]  = "string",
        ["char*"]           = "IntPtr",
        ["wchar_t*"]        = "IntPtr",
        ["WindowID"]        = "uint",
    };

    // Platform-specific type aliases (HAL types → IntPtr)
    static readonly HashSet<string> s_OpaquePointerTypes = new(StringComparer.Ordinal)
    {
        "WindowHandle", "WindowProc", "WindowExitResize", "WindowResize",
        "HWND", "HINSTANCE", "HDC", "HGLRC",
    };

    static string MapType(string cppType)
    {
        var normalized = cppType.Trim();

        // 1. Try full match first (important for "const char*" → string)
        if (s_TypeMap.TryGetValue(normalized, out var mappedFull))
            return mappedFull;

        // 2. Strip const from start and end
        if (normalized.StartsWith("const "))
            normalized = normalized[6..].Trim();
        if (normalized.EndsWith(" const"))
            normalized = normalized[..^6].Trim();

        // Handle namespace-qualified types: ArisenEngine::Float32 → Float32
        if (normalized.Contains("::"))
        {
            var lastColons = normalized.LastIndexOf("::");
            normalized = normalized[(lastColons + 2)..].Trim();
        }

        // Direct match
        if (s_TypeMap.TryGetValue(normalized, out var mapped))
            return mapped;

        // Opaque pointer types
        if (s_OpaquePointerTypes.Contains(normalized))
            return "IntPtr";

        // Manually map Window to uint (it's a wrapper for WindowID)
        if (normalized == "Window")
            return "uint";

        // Any pointer type → IntPtr
        if (normalized.Contains("*"))
            return "IntPtr";

        // Reference to known type
        if (normalized.EndsWith("&"))
        {
            var inner = normalized[..^1].Trim();
            if (s_TypeMap.TryGetValue(inner, out var innerMapped))
                return innerMapped;
        }

        // RHIHandleInterop → RHIHandle
        if (normalized == "RHIHandleInterop")
            return "RHIHandle";

        // Enum types - pass through (they'll be defined in their own files)
        return normalized;
    }

    static string MapEnumBaseType(string cppBaseType)
    {
        return cppBaseType switch
        {
            "uint32_t" => "uint",
            "uint16_t" => "ushort",
            "uint8_t"  => "byte",
            "int32_t"  => "int",
            "int16_t"  => "short",
            "int8_t"   => "sbyte",
            _          => "uint"
        };
    }

    static string? GetMarshalAttribute(string cppType)
    {
        var normalized = cppType.Trim();
        if (normalized == "const char*")
            return "[MarshalAs(UnmanagedType.LPUTF8Str)]";
        if (normalized == "const wchar_t*")
            return "[MarshalAs(UnmanagedType.LPWStr)]";
        return null;
    }

    // ====================================================================
    // Utilities
    // ====================================================================

    static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        // Already PascalCase?
        if (char.IsUpper(name[0])) return name;
        return char.ToUpper(name[0]) + name[1..];
    }

    static void ParseArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--source_code" && i + 1 < args.Length)
                s_SourceCode = args[i + 1];
            else if (args[i] == "--output" && i + 1 < args.Length)
                s_Output = args[i + 1];
            // --library is accepted but not used in the new generator
        }
    }

    static void CleanOutputDirectory(string dirPath)
    {
        if (!Directory.Exists(dirPath)) return;

        // Clean .cs files recursively (including subdirectories like RHI/)
        foreach (var file in Directory.GetFiles(dirPath, "*.cs", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        // Remove empty subdirectories (skip bin/obj)
        foreach (var dir in Directory.GetDirectories(dirPath))
        {
            var name = Path.GetFileName(dir);
            if (name == "bin" || name == "obj") continue;
            if (Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length == 0)
                Directory.Delete(dir, true);
        }
    }

    // ====================================================================
    // Project File Generation
    // ====================================================================

    static void GenerateProjectFile(string outputDir)
    {
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var projPath = Path.Combine(outputDir, "AutoBinding.csproj");
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <BaseOutputPath>..\..\..\x64\</BaseOutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include=""ArisenEngine"" />
  </ItemGroup>

</Project>
";
        File.WriteAllText(projPath, content);
        Console.WriteLine("Generated AutoBinding.csproj (no CppSharp dependency)");
    }

    static void GenerateMarshaller(string outputDir)
    {
        var path = Path.Combine(outputDir, "UTF8Marshaller.cs");
        var content = "// Auto-generated by Arisen BindingGenerator (" + s_GenerationTime + @") — DO NOT EDIT
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Arisen.Native
{
    public static class UTF8Marshaller
    {
        public static string NativeToUTF8(IntPtr nativePtr)
        {
            if (nativePtr == IntPtr.Zero) return string.Empty;
            return Marshal.PtrToStringUTF8(nativePtr) ?? string.Empty;
        }

        public static unsafe string NativeToUTF8(IntPtr* nativePtr)
        {
            if (nativePtr == null || *nativePtr == IntPtr.Zero) return string.Empty;
            return Marshal.PtrToStringUTF8(*nativePtr) ?? string.Empty;
        }

        public static IntPtr UTF8ToNative(string str)
        {
            if (str == null) return IntPtr.Zero;
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0);
            return ptr;
        }
    }
}
";
        File.WriteAllText(path, content);
        Console.WriteLine("Generated UTF8Marshaller.cs");
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BindingGenerator.Models;

namespace BindingGenerator.Parser;

public static class BindingParser
{
    // --- Enum Parser ---
    public static List<EnumInfo> ParseEnums(string content)
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

    private static List<(string Name, string? Value)> ParseEnumBody(string body)
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
    public static List<StructInfo> ParseStructs(string content)
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

    // --- Handle Parser ---
    public static List<string> ParseHandles(string content)
    {
        var results = new List<string>();
        var pattern = @"ARISEN_BIND_HANDLE\s*\(\s*(\w+)\s*\)";
        foreach (Match m in Regex.Matches(content, pattern))
        {
            results.Add(m.Groups[1].Value);
        }
        return results.Distinct().ToList();
    }

    // --- Extern "C" Function Parser ---
    public static List<FunctionInfo> ParseExternCFunctions(string content)
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

    public static void ParseFunctionsInBlock(string block, List<FunctionInfo> results)
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

    private static FunctionInfo? ParseFunctionSignature(string signature)
    {
        var m = Regex.Match(signature, @"extern\s+""C""\s+\w+\s+(\w[\w\s\*]*?)\s+(\w+)\s*\(([^)]*)\)\s*;");
        if (!m.Success) return null;

        return new FunctionInfo(
            m.Groups[1].Value.Trim(),
            m.Groups[2].Value.Trim(),
            ParseParameters(m.Groups[3].Value.Trim())
        );
    }

    public static List<(string Type, string Name)> ParseParameters(string paramsStr)
    {
        var parameters = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(paramsStr) || paramsStr == "void")
            return parameters;

        foreach (var param in paramsStr.Split(','))
        {
            var p = param.Trim();
            if (string.IsNullOrWhiteSpace(p)) continue;

            // Handle default values: Type x = 0 or Type x { 0 }
            if (p.Contains('='))
                p = p[..p.IndexOf('=')].Trim();
            
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

    // --- Bridge Block Parser ---
    public static List<BridgeBlock> ParseBridgeBlocks(string content)
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
}

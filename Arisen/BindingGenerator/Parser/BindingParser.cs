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

        // Pattern 1: ARISEN_BIND_ENUM(Name) [optional comments/newlines] enum class Name : Type { ... };
        var pattern1 = @"ARISEN_BIND_ENUM\s*\(\s*(\w+)\s*\)\s*(?:/\*.*?\*/|//[^\n]*\n|\s)*enum\s+class\s+(\w+)\s*:\s*(\w+)\s*\{([^}]+)\}";
        foreach (Match m in Regex.Matches(content, pattern1, RegexOptions.Singleline))
        {
            var name = m.Groups[2].Value;
            var baseType = m.Groups[3].Value;
            var body = m.Groups[4].Value;
            results.Add(new EnumInfo(name, baseType, ParseEnumBody(body)));
        }

        // Pattern 2: ARISEN_BIND_ENUM(Name) [optional comments/newlines] typedef enum Name { ... } Name; OR typedef enum { ... } Name;
        var pattern2 =
            @"ARISEN_BIND_ENUM\s*\(\s*(\w+)\s*\)\s*(?:/\*.*?\*/|//[^\n]*\n|\s)*typedef\s+enum\s*(\s+\w+)?\s*\{([^}]+)\}\s*(\w+)\s*;";
        foreach (Match m in Regex.Matches(content, pattern2, RegexOptions.Singleline))
        {
            var name = m.Groups[4].Value;
            var body = m.Groups[3].Value;
            // C-style enums default to int, but Vulkan values fit in uint
            results.Add(new EnumInfo(name, "int", ParseEnumBody(body)));
        }

        // Pattern 3: ARISEN_BIND_ENUM(Name) [optional comments/newlines] enum class Name { ... }; (no explicit base type)
        var pattern3 = @"ARISEN_BIND_ENUM\s*\(\s*(\w+)\s*\)\s*(?:/\*.*?\*/|//[^\n]*\n|\s)*enum\s+class\s+(\w+)\s*\{([^}]+)\}";
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
        var seenNames = new HashSet<string>();

        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.Trim().TrimEnd(',').Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//") || trimmed.StartsWith("#"))
                continue;

            string name;
            string? val = null;

            if (trimmed.Contains('='))
            {
                var parts = trimmed.Split('=', 2);
                name = parts[0].Trim();
                val = parts[1].Trim();
                if (string.IsNullOrWhiteSpace(val)) val = null;
            }
            else
            {
                name = trimmed;
            }

            if (!seenNames.Contains(name))
            {
                seenNames.Add(name);
                values.Add((name, val));
            }
        }

        return values;
    }

    // --- Struct Parser ---
    public static List<StructInfo> ParseStructs(string content)
    {
        var results = new List<StructInfo>();

        // Support both:
        // ARISEN_BIND_STRUCT(Name) struct Name { ... };
        // ARISEN_BIND_STRUCT(Name) typedef struct { ... } Name;
        var pattern = @"ARISEN_BIND_STRUCT\s*\(\s*(\w+)\s*\)\s*(?:/\*.*?\*/|//[^\n]*\n|\s)*(?:typedef\s+)?struct\s*(\s+\w+)?\s*\{(.*?)\}\s*(\w+)?\s*;";
        foreach (Match m in Regex.Matches(content, pattern, RegexOptions.Singleline))
        {
            var macroName = m.Groups[1].Value;
            var structName = m.Groups[2].Value.Trim();
            var body = m.Groups[3].Value;
            var typedefName = m.Groups[4].Value.Trim();

            var finalName = string.IsNullOrEmpty(typedefName) ? structName : typedefName;
            if (string.IsNullOrEmpty(finalName)) finalName = macroName;

            var fields = new List<(string, string)>();
            var fieldPattern = @"((?:const\s+)?[\w:\*]+(?:\s+const)?)\s+([\w]+)[^;]*;";
            var resultsInBody = Regex.Matches(body, fieldPattern, RegexOptions.Singleline);
            foreach (Match fieldMatch in resultsInBody)
            {
                var type = fieldMatch.Groups[1].Value.Trim();
                var fieldName = fieldMatch.Groups[2].Value.Trim();
                fields.Add((type, fieldName));
            }

            results.Add(new StructInfo(finalName, fields));
        }

        // Handle standalone structs (Opaque/Handles)
        var standalonePattern = @"ARISEN_BIND_STRUCT\s*\(\s*(\w+)\s*\)\s*;?";
        foreach (Match m in Regex.Matches(content, standalonePattern))
        {
            var name = m.Groups[1].Value;
            if (results.Any(r => r.Name == name)) continue;
            
            // Generate as opaque 64-bit handle
            results.Add(new StructInfo(name, new List<(string, string)> { ("uint64_t", "Handle") }));
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
        // A bridge block can contain local helpers. Only explicitly exported functions
        // belong to the generated ABI surface.
        var funcPattern = @"\w+_DLL\s+(\w[\w\s\*]*?)\s+(\w+)\s*\(([^)]*)\)\s*(?:;|{)";
        foreach (Match m in Regex.Matches(block, funcPattern))
        {
            var retType = m.Groups[1].Value.Trim();
            var name = m.Groups[2].Value.Trim();
            var paramsStr = m.Groups[3].Value.Trim();

            // Skip functions with C++ reference types (&&, &) — not C-ABI compatible
            if (paramsStr.Contains("&&") || paramsStr.Contains("&"))
                continue;

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

            // Handle C-style array: "Type name[4]"
            if (name.EndsWith("]"))
            {
                var bracketIdx = name.IndexOf('[');
                if (bracketIdx != -1)
                {
                    type += name[bracketIdx..];
                    name = name[..bracketIdx].Trim();
                }
            }

            parameters.Add((type, name));
        }

        return parameters;
    }

    // --- Bridge Block Parser ---
    public static List<BridgeBlock> ParseBridgeBlocks(string content)
    {
        var results = new List<BridgeBlock>();

        var pattern =
            @"ARISEN_BIND_BEGIN_BRIDGE\s*\(\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""\s*\)(.*?)ARISEN_BIND_END_BRIDGE\s*\(\s*\)";
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

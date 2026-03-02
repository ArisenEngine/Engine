using System;
using System.Collections.Generic;

namespace BindingGenerator.Parser;

public static class TypeMapper
{
    private static readonly Dictionary<string, string> s_TypeMap = new(StringComparer.Ordinal)
    {
        ["void"] = "void",
        ["void*"] = "IntPtr",
        ["const void*"] = "IntPtr",
        ["bool"] = "bool",
        ["uint8_t"] = "byte",
        ["UInt8"] = "byte",
        ["int8_t"] = "sbyte",
        ["SInt8"] = "sbyte",
        ["uint16_t"] = "ushort",
        ["UInt16"] = "ushort",
        ["int16_t"] = "short",
        ["SInt16"] = "short",
        ["uint32_t"] = "uint",
        ["UInt32"] = "uint",
        ["int32_t"] = "int",
        ["SInt32"] = "int",
        ["uint64_t"] = "ulong",
        ["UInt64"] = "ulong",
        ["int64_t"] = "long",
        ["SInt64"] = "long",
        ["float"] = "float",
        ["Float32"] = "float",
        ["double"] = "double",
        ["size_t"] = "nuint",
        ["SIZE_T"] = "nuint",
        ["const char*"] = "string",
        ["const wchar_t*"] = "string",
        ["char*"] = "IntPtr",
        ["wchar_t*"] = "IntPtr",
        ["WindowID"] = "uint",
    };

    private static readonly HashSet<string> s_OpaquePointerTypes = new(StringComparer.Ordinal)
    {
        "WindowHandle", "WindowProc", "WindowExitResize", "WindowResize",
        "HWND", "HINSTANCE", "HDC", "HGLRC",
    };

    public static string MapType(string cppType)
    {
        var normalized = cppType.Trim();

        // 1. Extract array bounds (e.g. `float[4]` or `const float color[4]`)
        bool isArray = false;
        if (normalized.EndsWith("]"))
        {
            var openBracket = normalized.LastIndexOf('[');
            if (openBracket != -1)
            {
                isArray = true;
                normalized = normalized[..openBracket].Trim();
            }
        }

        // 2. Try full match first (important for "const char*" → string)
        if (s_TypeMap.TryGetValue(normalized, out var mappedFull))
        {
            return isArray ? mappedFull + "[]" : mappedFull;
        }

        // 3. Strip const from start and end
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

        string mappedLocal;

        // Direct match
        if (s_TypeMap.TryGetValue(normalized, out mappedLocal))
        {
            // Already handled in Step 2, but just in case of normalized changes
        }
        // Opaque pointer types
        else if (s_OpaquePointerTypes.Contains(normalized))
        {
            mappedLocal = "IntPtr";
        }
        // Manually map Window to uint (it's a wrapper for WindowID)
        else if (normalized == "Window")
        {
            mappedLocal = "uint";
        }
        // Any pointer type → IntPtr
        else if (normalized.Contains("*"))
        {
            mappedLocal = "IntPtr";
        }
        // Reference to known type
        else if (normalized.EndsWith("&"))
        {
            var inner = normalized[..^1].Trim();
            if (s_TypeMap.TryGetValue(inner, out var innerMapped))
                mappedLocal = innerMapped;
            else
                mappedLocal = inner;
        }
        // RHIHandleInterop → RHIHandle
        else if (normalized == "RHIHandleInterop")
        {
            mappedLocal = "RHIHandle";
        }
        else
        {
            // Enum types - pass through (they'll be defined in their own files)
            mappedLocal = normalized;
        }

        if (isArray)
        {
            mappedLocal += "[]";
        }

        return mappedLocal;
    }

    public static string MapEnumBaseType(string cppBaseType)
    {
        return cppBaseType switch
        {
            "uint32_t" or "UInt32" => "uint",
            "uint16_t" or "UInt16" => "ushort",
            "uint8_t" or "UInt8" => "byte",
            "int32_t" or "SInt32" => "int",
            "int16_t" or "SInt16" => "short",
            "int8_t" or "SInt8" => "sbyte",
            _ => "uint"
        };
    }

    public static string? GetMarshalAttribute(string cppType)
    {
        var normalized = cppType.Trim();
        if (normalized == "const char*")
            return "[MarshalAs(UnmanagedType.LPUTF8Str)]";
        if (normalized == "const wchar_t*")
            return "[MarshalAs(UnmanagedType.LPWStr)]";
        return null;
    }
}
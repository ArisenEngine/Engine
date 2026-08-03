using System.Text.RegularExpressions;

namespace Com.Arisen.Rendering.Tests;

internal static class CppSourceContractScanner
{
    public sealed record ExportParameter(
        string Name,
        string Type,
        string Declaration,
        bool IsPointerLike);

    public sealed record ExportFunction(
        string Name,
        string ReturnType,
        IReadOnlyList<ExportParameter> Parameters,
        int BodyStart,
        int BodyEnd,
        int Line);

    public static string MaskCommentsAndLiterals(string source)
    {
        char[] masked = source.ToCharArray();

        for (int index = 0; index < source.Length;)
        {
            if (TryGetRawStringEnd(source, index, out int rawStringEnd))
            {
                MaskRange(masked, index, rawStringEnd);
                index = rawStringEnd;
                continue;
            }

            if (source[index] == '/' && index + 1 < source.Length)
            {
                if (source[index + 1] == '/')
                {
                    int end = source.IndexOf('\n', index + 2);
                    end = end < 0 ? source.Length : end;
                    MaskRange(masked, index, end);
                    index = end;
                    continue;
                }

                if (source[index + 1] == '*')
                {
                    int terminator = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    int end = terminator < 0 ? source.Length : terminator + 2;
                    MaskRange(masked, index, end);
                    index = end;
                    continue;
                }
            }

            if (source[index] is '"' or '\'')
            {
                char quote = source[index];
                int end = FindQuotedLiteralEnd(source, index, quote);
                MaskRange(masked, index, end);
                index = end;
                continue;
            }

            index++;
        }

        return new string(masked);
    }

    public static bool TryFindIdentifier(
        string source,
        string identifier,
        int searchStart,
        out int identifierStart)
    {
        int candidate = searchStart;
        while ((candidate = source.IndexOf(identifier, candidate, StringComparison.Ordinal)) >= 0)
        {
            if (IsIdentifierAt(source, candidate, identifier))
            {
                identifierStart = candidate;
                return true;
            }

            candidate += identifier.Length;
        }

        identifierStart = -1;
        return false;
    }

    public static IReadOnlyList<ExportFunction> ParseExports(
        string source,
        string sourcePath,
        string exportMarker = "RHI_DLL")
    {
        var exports = new List<ExportFunction>();
        int searchStart = 0;

        while (TryFindIdentifier(source, exportMarker, searchStart, out int exportStart))
        {
            int declarationStart = exportStart + exportMarker.Length;
            int parameterStart = source.IndexOf('(', declarationStart);
            if (parameterStart < 0)
            {
                throw ParseFailure(sourcePath, source, exportStart, "has no parameter list");
            }

            int nameEnd = SkipWhitespaceBackward(source, parameterStart - 1) + 1;
            int nameStart = nameEnd;
            while (nameStart > declarationStart && IsIdentifierCharacter(source[nameStart - 1]))
            {
                nameStart--;
            }

            if (nameStart == nameEnd)
            {
                throw ParseFailure(sourcePath, source, exportStart, "has no function name");
            }

            string name = source[nameStart..nameEnd];
            string returnType = NormalizeWhitespace(source[declarationStart..nameStart]);
            if (returnType.Length == 0)
            {
                throw ParseFailure(sourcePath, source, exportStart, $"{name} has no return type");
            }

            int parameterEnd = FindMatchingDelimiter(
                source,
                parameterStart,
                '(',
                ')',
                sourcePath,
                name);
            int bodyStart = FindBodyStart(source, parameterEnd + 1, sourcePath, name, exportMarker);
            int bodyEnd = FindMatchingDelimiter(
                source,
                bodyStart,
                '{',
                '}',
                sourcePath,
                name);

            exports.Add(new ExportFunction(
                name,
                returnType,
                ParseParameters(source[(parameterStart + 1)..parameterEnd], sourcePath, source, parameterStart),
                bodyStart,
                bodyEnd,
                GetLineNumber(source, exportStart)));
            searchStart = bodyEnd + 1;
        }

        return exports;
    }

    public static int CountInvocations(string source, string functionName)
    {
        return Regex.Matches(
            source,
            $@"\b{Regex.Escape(functionName)}\s*\(",
            RegexOptions.CultureInvariant).Count;
    }

    public static bool IsIdentifierAt(string source, int start, string identifier)
    {
        if (start < 0 || start + identifier.Length > source.Length ||
            !source.AsSpan(start, identifier.Length).SequenceEqual(identifier))
        {
            return false;
        }

        bool validStart = start == 0 || !IsIdentifierCharacter(source[start - 1]);
        int end = start + identifier.Length;
        bool validEnd = end == source.Length || !IsIdentifierCharacter(source[end]);
        return validStart && validEnd;
    }

    public static bool IsIdentifierCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character == '_';
    }

    public static int GetLineNumber(string source, int offset)
    {
        int line = 1;
        for (int index = 0; index < offset; index++)
        {
            if (source[index] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    public static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Arisen")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate repository root from test output directory.");
    }

    private static IReadOnlyList<ExportParameter> ParseParameters(
        string parameterList,
        string sourcePath,
        string source,
        int parameterListOffset)
    {
        var parameters = new List<ExportParameter>();
        int parameterStart = 0;
        int parentheses = 0;
        int brackets = 0;
        int braces = 0;
        int angleBrackets = 0;

        for (int index = 0; index <= parameterList.Length; index++)
        {
            bool atEnd = index == parameterList.Length;
            char character = atEnd ? '\0' : parameterList[index];
            if (!atEnd)
            {
                switch (character)
                {
                    case '(':
                        parentheses++;
                        break;
                    case ')':
                        parentheses--;
                        break;
                    case '[':
                        brackets++;
                        break;
                    case ']':
                        brackets--;
                        break;
                    case '{':
                        braces++;
                        break;
                    case '}':
                        braces--;
                        break;
                    case '<':
                        angleBrackets++;
                        break;
                    case '>':
                        angleBrackets--;
                        break;
                }
            }

            bool separator = atEnd ||
                (character == ',' && parentheses == 0 && brackets == 0 && braces == 0 && angleBrackets == 0);
            if (!separator)
            {
                continue;
            }

            string declaration = NormalizeWhitespace(parameterList[parameterStart..index]);
            parameterStart = index + 1;
            if (declaration.Length == 0 || declaration == "void")
            {
                continue;
            }

            int nameEnd = declaration.Length;
            while (nameEnd > 0 && char.IsWhiteSpace(declaration[nameEnd - 1]))
            {
                nameEnd--;
            }

            bool isArray = nameEnd > 0 && declaration[nameEnd - 1] == ']';
            if (isArray)
            {
                int arrayStart = declaration.LastIndexOf('[', nameEnd - 1);
                if (arrayStart < 0)
                {
                    throw ParseFailure(
                        sourcePath,
                        source,
                        parameterListOffset,
                        $"has malformed array parameter '{declaration}'");
                }

                nameEnd = arrayStart;
                while (nameEnd > 0 && char.IsWhiteSpace(declaration[nameEnd - 1]))
                {
                    nameEnd--;
                }
            }

            int nameStart = nameEnd;
            while (nameStart > 0 && IsIdentifierCharacter(declaration[nameStart - 1]))
            {
                nameStart--;
            }

            if (nameStart == nameEnd)
            {
                throw ParseFailure(
                    sourcePath,
                    source,
                    parameterListOffset,
                    $"has an unnamed or unsupported parameter '{declaration}'");
            }

            string name = declaration[nameStart..nameEnd];
            string type = NormalizeWhitespace(declaration[..nameStart]);
            if (isArray)
            {
                type = $"{type}[]";
            }

            parameters.Add(new ExportParameter(
                name,
                type,
                declaration,
                isArray || type.Contains('*', StringComparison.Ordinal)));
        }

        return parameters;
    }

    private static int FindBodyStart(
        string source,
        int searchStart,
        string sourcePath,
        string functionName,
        string exportMarker)
    {
        for (int index = searchStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                return index;
            }

            if (source[index] == ';' || IsIdentifierAt(source, index, exportMarker))
            {
                break;
            }
        }

        throw ParseFailure(
            sourcePath,
            source,
            searchStart,
            $"{functionName} is not a function definition");
    }

    private static int FindMatchingDelimiter(
        string source,
        int openingIndex,
        char opening,
        char closing,
        string sourcePath,
        string functionName)
    {
        int depth = 0;
        for (int index = openingIndex; index < source.Length; index++)
        {
            if (source[index] == opening)
            {
                depth++;
            }
            else if (source[index] == closing && --depth == 0)
            {
                return index;
            }
        }

        throw ParseFailure(
            sourcePath,
            source,
            openingIndex,
            $"{functionName} has an unterminated '{opening}' delimiter");
    }

    private static int SkipWhitespaceBackward(string source, int index)
    {
        while (index >= 0 && char.IsWhiteSpace(source[index]))
        {
            index--;
        }

        return index;
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static InvalidDataException ParseFailure(
        string sourcePath,
        string source,
        int offset,
        string detail)
    {
        return new InvalidDataException(
            $"{Path.GetFileName(sourcePath)}:{GetLineNumber(source, offset)} RHI_DLL {detail}.");
    }

    private static bool TryGetRawStringEnd(string source, int start, out int end)
    {
        end = start;
        ReadOnlySpan<string> prefixes = ["R\"", "u8R\"", "uR\"", "UR\"", "LR\""];

        foreach (string prefix in prefixes)
        {
            if (!source.AsSpan(start).StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            int delimiterStart = start + prefix.Length;
            int contentStart = source.IndexOf('(', delimiterStart);
            if (contentStart < 0 || contentStart - delimiterStart > 16)
            {
                return false;
            }

            string delimiter = source[delimiterStart..contentStart];
            if (delimiter.Any(static character =>
                    char.IsWhiteSpace(character) || character is '\\' or '(' or ')'))
            {
                return false;
            }

            string terminator = $"){delimiter}\"";
            int terminatorStart = source.IndexOf(
                terminator,
                contentStart + 1,
                StringComparison.Ordinal);
            end = terminatorStart < 0
                ? source.Length
                : terminatorStart + terminator.Length;
            return true;
        }

        return false;
    }

    private static int FindQuotedLiteralEnd(string source, int start, char quote)
    {
        for (int index = start + 1; index < source.Length; index++)
        {
            if (source[index] == '\\')
            {
                index++;
                continue;
            }

            if (source[index] == quote)
            {
                return index + 1;
            }
        }

        return source.Length;
    }

    private static void MaskRange(char[] source, int start, int end)
    {
        for (int index = start; index < end; index++)
        {
            if (source[index] is not '\r' and not '\n')
            {
                source[index] = ' ';
            }
        }
    }
}

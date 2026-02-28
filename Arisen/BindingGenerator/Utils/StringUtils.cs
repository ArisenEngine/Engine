using System.Text.RegularExpressions;

namespace BindingGenerator.Utils;

public static class StringUtils
{
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        // Already PascalCase?
        if (char.IsUpper(name[0])) return name;
        return char.ToUpper(name[0]) + name[1..];
    }

    public static string? ExtractMacroArg(string content, string macroName)
    {
        var match = Regex.Match(content, $@"{macroName}\s*\(\s*""([^""]+)""\s*\)");
        return match.Success ? match.Groups[1].Value : null;
    }
}

using System;
using System.IO;
using System.Linq;

namespace ArisenBuildTool.Utils;

public static class PathUtils
{
    public static string GetRelativePath(string fromDir, string toPath)
    {
        Uri fromUri = new Uri(fromDir.EndsWith(Path.DirectorySeparatorChar) ? fromDir : fromDir + Path.DirectorySeparatorChar);
        Uri toUri = new Uri(toPath);
        return Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString()).Replace('/', '\\');
    }

    public static string ToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return string.Join("", text.Split('-').Select(w => char.ToUpper(w[0]) + w.Substring(1)));
    }
}

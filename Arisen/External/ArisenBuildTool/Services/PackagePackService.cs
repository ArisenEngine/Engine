using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ArisenBuildTool.Models;

namespace ArisenBuildTool.Services;

public static class PackagePackService
{
    private static readonly string[] s_ExcludedDirectoryNames =
    {
        ".arisen",
        ".git",
        "bin",
        "obj"
    };

    public static string Pack(PackageInfo package, string outputDirectory, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(package.Manifest.Id))
            throw new ArgumentException("Package id is required.", nameof(package));

        if (!Directory.Exists(package.DirectoryPath))
            throw new DirectoryNotFoundException($"Package directory not found: {package.DirectoryPath}");

        string packageJsonPath = Path.Combine(package.DirectoryPath, "package.json");
        if (!File.Exists(packageJsonPath))
            throw new FileNotFoundException($"Package '{package.Manifest.Id}' is missing package.json.", packageJsonPath);

        Directory.CreateDirectory(outputDirectory);

        string version = string.IsNullOrWhiteSpace(package.Manifest.Version) ? "0.0.0" : package.Manifest.Version;
        string archiveName = $"{SanitizeFileName(package.Manifest.Id)}-{SanitizeFileName(version)}.zip";
        string archivePath = Path.Combine(outputDirectory, archiveName);

        if (File.Exists(archivePath))
        {
            if (!overwrite)
                throw new IOException($"Package archive already exists: {archivePath}");

            File.Delete(archivePath);
        }

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (string file in Directory.EnumerateFiles(package.DirectoryPath, "*", SearchOption.AllDirectories)
                     .OrderBy(path => Path.GetRelativePath(package.DirectoryPath, path), StringComparer.OrdinalIgnoreCase))
        {
            string relativePath = Path.GetRelativePath(package.DirectoryPath, file).Replace('\\', '/');
            if (ShouldExclude(relativePath))
                continue;

            archive.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal);
        }

        return archivePath;
    }

    private static bool ShouldExclude(string relativePath)
    {
        string[] segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => s_ExcludedDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c));
    }
}

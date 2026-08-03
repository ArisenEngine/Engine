using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class PackageResolutionService
{
    public const int ResolvedManifestSchemaVersion = 2;

    public static List<PackageInfo> SortTopologically(Dictionary<string, PackageInfo> packageMap)
    {
        var result = new List<PackageInfo>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!packageMap.TryGetValue(id, out var package)) return;

            if (visiting.Contains(id))
            {
                Logger.Warning($"Circular dependency detected involving package '{id}'. Skipping for sort.");
                return;
            }

            visiting.Add(id);

            if (package.Manifest.Dependencies != null)
            {
                foreach (var depId in package.Manifest.Dependencies.Keys)
                {
                    Visit(depId);
                }
            }

            visiting.Remove(id);
            visited.Add(id);
            result.Add(package);
        }

        // We want to ensure all packages in the map are visited
        foreach (var id in packageMap.Keys)
        {
            Visit(id);
        }

        return result;
    }

    public static void SaveResolvedManifests(
        string profile,
        List<string> outputDirs,
        List<PackageInfo> sortedPackages,
        string fileName = "manifest.resolved.json",
        IReadOnlyList<ResolvedNativePayload>? nativePayloads = null,
        bool nativePayloadsFinalized = false,
        string? configuration = null,
        bool? enableProfiler = null)
    {
        foreach (var outDir in outputDirs)
        {
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            
            string path = Path.Combine(outDir, fileName);

            var resolvedData = new
            {
                SchemaVersion = ResolvedManifestSchemaVersion,
                Profile = profile,
                EnableProfiler = enableProfiler,
                Configuration = string.IsNullOrWhiteSpace(configuration)
                    ? null
                    : configuration.Trim(),
                NativePayloadsFinalized = nativePayloadsFinalized,
                NativePayloads = nativePayloads ?? Array.Empty<ResolvedNativePayload>(),
                ResolvedPackages = sortedPackages.Select(p => new
                {
                    Id = p.Manifest.Id,
                    Name = p.Manifest.Name,
                    Version = p.Manifest.Version,
                    Engine = p.Manifest.Engine,
                    Type = p.Manifest.Type,
                    Dependencies = p.Manifest.Dependencies ?? new Dictionary<string, string>(),
                    Services = p.Manifest.Services,
                    Subsystems = p.Manifest.Subsystems,
                    NativeRuntimes = enableProfiler.HasValue
                        ? NativeRuntimeManifestService.SelectForProfiler(p.Manifest, enableProfiler.Value)
                        : p.Manifest.NativeRuntimes,
                    NativeTests = p.Manifest.NativeTests,
                    Entry = p.Manifest.Entry,
                    // Store relative URL for portability (Relative to the output directory!)
                    Url = GetRelativeUrl(outDir, p.DirectoryPath)
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            string json = JsonSerializer.Serialize(resolvedData, options) + Environment.NewLine;
            WriteAtomically(path, json);
            Logger.Info($"Generated resolved manifest: {path}");
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        string directory = Path.GetDirectoryName(path)!;
        string stagingPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.arisen-stage-{Guid.NewGuid():N}");
        try
        {
            using (var stream = new FileStream(
                       stagingPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path)) File.Replace(stagingPath, path, destinationBackupFileName: null);
            else File.Move(stagingPath, path);
        }
        finally
        {
            if (File.Exists(stagingPath)) File.Delete(stagingPath);
        }
    }

    private static string GetRelativeUrl(string fromDir, string packageDir)
    {
        // Convert back to file:// style relative path
        string relPath = PathUtils.GetRelativePath(fromDir, packageDir).Replace('\\', '/');
        // Ensure it has / at end if it doesn't represent a file but a directory
        if (!relPath.EndsWith("/")) relPath += "/";
        return $"file://{relPath}";
    }
}

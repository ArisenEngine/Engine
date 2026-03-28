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

    public static void SaveResolvedManifest(string workspaceDir, string profile, List<PackageInfo> sortedPackages)
    {
        string fileName = $"manifest.resolved.{profile}.json";
        string path = Path.Combine(workspaceDir, fileName);

        var resolvedData = new
        {
            Profile = profile,
            Timestamp = DateTime.UtcNow.ToString("O"),
            ResolvedPackages = sortedPackages.Select(p => new
            {
                Id = p.Manifest.Id,
                Name = p.Manifest.Name,
                Version = p.Manifest.Version,
                // Store relative URL for portability
                Url = GetRelativeUrl(workspaceDir, p.DirectoryPath)
            }).ToList()
        };

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(resolvedData, options);
            File.WriteAllText(path, json);
            Logger.Info($"Generated resolved manifest: {fileName}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save resolved manifest: {ex.Message}");
        }
    }

    private static string GetRelativeUrl(string workspaceDir, string packageDir)
    {
        // Convert back to file:// style relative path
        string relPath = PathUtils.GetRelativePath(workspaceDir, packageDir).Replace('\\', '/');
        return $"file://{relPath}";
    }
}

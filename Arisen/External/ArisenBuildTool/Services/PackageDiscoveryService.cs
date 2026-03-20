using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class PackageDiscoveryService
{
    public static Dictionary<string, PackageInfo> Discover(ProjectManifest manifest, string workspaceDir)
    {
        var map = new Dictionary<string, PackageInfo>();

        if (manifest.Packages == null)
            return map;

        foreach (var req in manifest.Packages)
        {
            if (string.IsNullOrEmpty(req.Url))
            {
                Logger.Warning($"Package '{req.Id}' has no URL specified. Skipping.");
                continue;
            }

            if (!req.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning($"Package '{req.Id}' has non-file URL ({req.Url}). ArisenBuildTool currently only resolves local file:// URLs.");
                continue;
            }

            // Extract path. It could be absolute or relative to workspaceDir.
            string pathPart = Uri.UnescapeDataString(req.Url.Substring(7));
            
            // If it's relative (e.g., file://Packages/com.arisen.core or file://./Packages/...)
            string fullPath = Path.IsPathRooted(pathPart) 
                ? Path.GetFullPath(pathPart) 
                : Path.GetFullPath(Path.Combine(workspaceDir, pathPart));

            if (!Directory.Exists(fullPath))
            {
                Logger.Warning($"Directory for package '{req.Id}' not found at '{fullPath}'.");
                continue;
            }

            string packageJsonPath = Path.Combine(fullPath, "package.json");
            if (File.Exists(packageJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(packageJsonPath);
                    var pkgManifest = JsonSerializer.Deserialize<PackageManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (pkgManifest != null)
                    {
                        string packageName = string.IsNullOrEmpty(pkgManifest.Name) ? Path.GetFileName(fullPath) : pkgManifest.Name;
                        string bipartiteKey = $"{packageName}_{pkgManifest.Type}";

                        map[bipartiteKey] = new PackageInfo 
                        { 
                            Manifest = pkgManifest, 
                            DirectoryPath = fullPath 
                        };
                        Logger.Info($"Discovered package: {packageName} ({pkgManifest.Type}) at {fullPath}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error parsing {packageJsonPath}: {ex.Message}");
                }
            }
            else
            {
                Logger.Warning($"No package.json found for '{req.Id}' at '{fullPath}'.");
            }
        }

        return map;
    }
}

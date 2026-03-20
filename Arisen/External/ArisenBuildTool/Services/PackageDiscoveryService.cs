using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class PackageDiscoveryService
{
    public static Dictionary<string, PackageInfo> Discover(ProjectManifest manifest, string workspaceDir, string engineDir)
    {
        var map = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);
        var toProcess = new Queue<PackageRequirement>();

        if (manifest.Packages != null)
        {
            foreach (var req in manifest.Packages) toProcess.Enqueue(req);
        }

        if (manifest.Profiles != null)
        {
            foreach (var profile in manifest.Profiles.Values)
            {
                if (profile != null)
                {
                    foreach (var req in profile) toProcess.Enqueue(req);
                }
            }
        }

        while (toProcess.Count > 0)
        {
            var req = toProcess.Dequeue();
            if (string.IsNullOrEmpty(req.Id) || map.ContainsKey(req.Id)) continue;

            string fullPath = ResolvePackagePath(req, workspaceDir, engineDir);
            
            if (string.IsNullOrEmpty(fullPath) || !Directory.Exists(fullPath))
            {
                Logger.Warning($"Directory for package '{req.Id}' not found. Searched path: '{fullPath}'");
                continue;
            }

            string packageJsonPath = Path.Combine(fullPath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                Logger.Warning($"No package.json found for '{req.Id}' at '{fullPath}'.");
                continue;
            }

            try
            {
                var json = File.ReadAllText(packageJsonPath);
                var pkgManifest = JsonSerializer.Deserialize<PackageManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (pkgManifest != null)
                {
                    pkgManifest.Id = req.Id; 
                    string packageName = string.IsNullOrEmpty(pkgManifest.Name) ? Path.GetFileName(fullPath) : pkgManifest.Name;
                    
                    if (pkgManifest.NativeRuntimes != null && pkgManifest.NativeRuntimes.Count > 0 && pkgManifest.Entry == null)
                    {
                        pkgManifest.Type = "native";
                    }
                    else
                    {
                        pkgManifest.Type = "managed";
                    }

                    map[req.Id] = new PackageInfo 
                    { 
                        Manifest = pkgManifest, 
                        DirectoryPath = fullPath 
                    };
                    Logger.Info($"Discovered package: {req.Id} ({pkgManifest.Type}) at {fullPath}");

                    if (pkgManifest.Dependencies != null)
                    {
                        foreach (var dep in pkgManifest.Dependencies)
                        {
                            if (!map.ContainsKey(dep.Key))
                            {
                                toProcess.Enqueue(new PackageRequirement { Id = dep.Key });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error parsing {packageJsonPath}: {ex.Message}");
            }
        }

        return map;
    }

    private static string ResolvePackagePath(PackageRequirement req, string workspaceDir, string engineDir)
    {
        if (!string.IsNullOrEmpty(req.Url) && req.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            string pathPart = Uri.UnescapeDataString(req.Url.Substring(7));
            return Path.IsPathRooted(pathPart) 
                ? Path.GetFullPath(pathPart) 
                : Path.GetFullPath(Path.Combine(workspaceDir, pathPart));
        }

        string localPath = Path.GetFullPath(Path.Combine(workspaceDir, "Local", req.Id));
        if (Directory.Exists(localPath)) return localPath;

        string cachePath = Path.GetFullPath(Path.Combine(workspaceDir, ".Cache", req.Id));
        if (Directory.Exists(cachePath)) return cachePath;

        string enginePkgPath = Path.GetFullPath(Path.Combine(engineDir, "Packages", req.Id));
        if (Directory.Exists(enginePkgPath)) return enginePkgPath;

        return string.Empty;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class NativeDeploymentService
{
    public static void Deploy(List<PackageInfo> allPackages, List<string> outputDirs, string profile)
    {
        Logger.Info($"Deploying native payloads for {allPackages.Count} packages to {outputDirs.Count} directories...");

        foreach (var pkg in allPackages)
        {
            if (pkg.Manifest.NativeRuntimes == null) continue;

            // Arisen uses platform IDs like "win-x64", "linux-x64", etc.
            // For now, we only handle current platform or "win-x64" as default.
            string targetPlatform = "win-x64"; 
            
            if (pkg.Manifest.NativeRuntimes.TryGetValue(targetPlatform, out var runtimes))
            {
                foreach (var runtime in runtimes)
                {
                    // If it's just a filename (no separators), assume it's build-generated and skip deployment mapping
                    if (!runtime.Contains("/") && !runtime.Contains("\\"))
                    {
                        continue;
                    }

                    // Resolve absolute source path
                    string sourcePath = Path.GetFullPath(Path.Combine(pkg.DirectoryPath, runtime));
                    
                    if (!File.Exists(sourcePath))
                    {
                        Logger.Warning($"Static native runtime not found: {sourcePath} (Package: {pkg.Manifest.Id})");
                        continue;
                    }

                    string filename = Path.GetFileName(sourcePath);

                    foreach (var outDir in outputDirs)
                    {
                        string destPath = Path.Combine(outDir, filename);
                        
                        try
                        {
                            if (ShouldCopy(sourcePath, destPath))
                            {
                                Logger.Info($"Deploying native payload: {filename} -> {outDir}");
                                File.Copy(sourcePath, destPath, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to deploy {filename} to {outDir}: {ex.Message}");
                        }
                    }
                }
            }
        }
    }

    private static bool ShouldCopy(string source, string dest)
    {
        if (!File.Exists(dest)) return true;
        
        var sourceInfo = new FileInfo(source);
        var destInfo = new FileInfo(dest);
        
        // Copy if newer or size differs (simple heuristic)
        return sourceInfo.LastWriteTimeUtc > destInfo.LastWriteTimeUtc || sourceInfo.Length != destInfo.Length;
    }
}

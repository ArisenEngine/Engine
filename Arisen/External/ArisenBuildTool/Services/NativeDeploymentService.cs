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

            foreach (var runtime in NativeRuntimeManifestService.EnumerateForRuntime(pkg, NativeRuntimeManifestService.DefaultRuntimeIdentifier))
            {
                if (runtime.Source == NativeRuntimeSource.BuildOutput)
                {
                    continue;
                }

                string sourcePath = Path.GetFullPath(Path.Combine(pkg.DirectoryPath, runtime.Path));
                if (!NativeRuntimeManifestService.IsInsideDirectory(sourcePath, pkg.DirectoryPath))
                {
                    throw new InvalidOperationException($"Static native runtime '{runtime.Path}' escapes the package directory. Package: {pkg.Manifest.Id}");
                }
                    
                if (!File.Exists(sourcePath))
                {
                    string message = $"Static native runtime not found: {sourcePath} (Package: {pkg.Manifest.Id})";
                    if (runtime.Required)
                    {
                        throw new FileNotFoundException(message, sourcePath);
                    }

                    Logger.Warning(message);
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
                        throw;
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

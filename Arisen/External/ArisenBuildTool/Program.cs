using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using ArisenBuildTool.Models;
using ArisenBuildTool.Services;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool;

class Program
{
    static void Main(string[] args)
    {
        string workspaceDir = Path.GetFullPath(".");
        string manifestPath = string.Empty;
        string engineDir = string.Empty;

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--manifest" || args[i] == "-m") && i + 1 < args.Length)
            {
                manifestPath = Path.GetFullPath(args[i + 1]);
                workspaceDir = Path.GetDirectoryName(manifestPath) ?? workspaceDir;
            }
            else if ((args[i] == "--engine" || args[i] == "-e") && i + 1 < args.Length)
            {
                engineDir = Path.GetFullPath(args[i + 1]);
            }
        }

        if (string.IsNullOrEmpty(manifestPath))
        {
            manifestPath = Path.Combine(workspaceDir, "manifest.json");
        }

        if (string.IsNullOrEmpty(engineDir))
        {
            // Default to resolving Engine path based on the build tool's own location
            // E:\... \Engine\Arisen\External\ArisenBuildTool\bin\Debug\net9.0\
            engineDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        }

        string logPath = Path.Combine(workspaceDir, "ArisenBuildTool.log");
        Logger.Initialize(logPath);
        Logger.Info("--------------------------------------------------");
        Logger.Info($"ArisenBuildTool Started. Workspace: {workspaceDir}");
        Logger.Info($"Engine Path: {engineDir}");

        if (!File.Exists(manifestPath))
        {
            string arisenManifestPath = Path.Combine(workspaceDir, "project.arisen");
            if (File.Exists(arisenManifestPath))
            {
                manifestPath = arisenManifestPath;
            }
            else
            {
                Logger.Error($"Project manifest not found at '{manifestPath}' or '{arisenManifestPath}'. Build aborted.");
                return;
            }
        }

        Logger.Info($"Loading manifest from {manifestPath}");
        ProjectManifest? manifest = null;
        try
        {
            string json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<ProjectManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to parse ProjectManifest: {ex.Message}");
            return;
        }

        if (manifest == null)
        {
            Logger.Error("ProjectManifest is null after deserialization.");
            return;
        }

        string projectName = string.IsNullOrEmpty(manifest.Name) ? "MyGame" : manifest.Name;
        string projectsDir = Path.Combine(workspaceDir, "Projects");
        Directory.CreateDirectory(projectsDir);

        // 1. Discover all packages
        var packageMap = PackageDiscoveryService.Discover(manifest, workspaceDir);
        Logger.Info($"Discovered {packageMap.Count} packages.");

        var managedPackages = packageMap.Values.Where(p => p.Manifest.Type != "native").ToList();
        var nativePackages = packageMap.Values.Where(p => p.Manifest.Type == "native").ToList();

        // 2. Generate .csproj files
        ProjectGeneratorService.GenerateForManagedPackages(workspaceDir, projectsDir, engineDir, managedPackages, packageMap);

        // 3. Generate CMake for native
        CMakeGeneratorService.Generate(engineDir, projectsDir, nativePackages, projectName);

        // 4. Generate .sln
        SolutionGeneratorService.Generate(projectsDir, engineDir, packageMap, projectName);

        Logger.Info("ArisenBuildTool: Workspace generation complete.");
    }
}

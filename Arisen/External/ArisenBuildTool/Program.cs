using System;
using System.Collections.Generic;
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
        if (args.Length > 0 && args[0].Equals("inject", StringComparison.OrdinalIgnoreCase))
        {
            RunInjectMode(args);
            return;
        }

        if (args.Length > 0 && args[0].Equals("test", StringComparison.OrdinalIgnoreCase))
        {
            RunTestMode(args);
            return;
        }

        RunGenerateMode(args);
    }

    static void RunInjectMode(string[] args)
    {
        string packageDir = string.Empty;
        string assemblyPath = string.Empty;

        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--package" || args[i] == "-p") && i + 1 < args.Length)
                packageDir = Path.GetFullPath(args[++i]);
            else if ((args[i] == "--assembly" || args[i] == "-a") && i + 1 < args.Length)
                assemblyPath = Path.GetFullPath(args[++i]);
        }

        if (string.IsNullOrEmpty(packageDir) || string.IsNullOrEmpty(assemblyPath))
        {
            Console.WriteLine("ArisenBuildTool Inject Error: --package and --assembly arguments are required.");
            Environment.Exit(1);
        }

        PackageInjectorService.Inject(packageDir, assemblyPath);
    }

    static void RunTestMode(string[] args)
    {
        string packageId = string.Empty;
        string workspaceDir = Path.GetFullPath(".");
        string engineDir = string.Empty;

        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--package" || args[i] == "-p") && i + 1 < args.Length)
                packageId = args[++i];
            else if ((args[i] == "--workspace" || args[i] == "-w") && i + 1 < args.Length)
                workspaceDir = Path.GetFullPath(args[++i]);
            else if ((args[i] == "--engine" || args[i] == "-e") && i + 1 < args.Length)
                engineDir = Path.GetFullPath(args[++i]);
        }

        if (string.IsNullOrEmpty(packageId))
        {
            Console.WriteLine("ArisenBuildTool Test Error: --package <Id> is required.");
            Environment.Exit(1);
        }

        if (string.IsNullOrEmpty(engineDir)) engineDir = FindEngineRoot(AppContext.BaseDirectory);

        string testPackageId = packageId.EndsWith(".test") ? packageId : $"{packageId}.test";
        
        Logger.Initialize(Path.Combine(workspaceDir, ".arisen", "ArisenBuildTool.Test.log"));
        Logger.Info($"ArisenBuildTool: Isolated Test Generation for {packageId}");

        // Create a Virtual Manifest for this test run
        var testManifest = new ProjectManifest
        {
            Name = $"{packageId}.TestRun",
            EngineVersion = "Current",
            Packages = new List<PackageRequirement>
            {
                new PackageRequirement { Id = "com.arisen.core", Url = "file://Local/com.arisen.core" },
                new PackageRequirement { Id = packageId, Url = $"file://Local/{packageId}" },
                new PackageRequirement { Id = testPackageId, Url = $"file://Local/{testPackageId}" },
                new PackageRequirement { Id = "com.arisen.testrunner", Url = "file://Local/com.arisen.testrunner" }
            }
        };

        // Reuse the generate logic with the virtual manifest
        ExecuteGeneration(workspaceDir, engineDir, "Testing", testManifest);
    }

    static void RunGenerateMode(string[] args)
    {
        string workspaceDir = Path.GetFullPath(".");
        string manifestPath = string.Empty;
        string engineDir = string.Empty;
        string profile = "Development";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "generate") continue;

            if ((args[i] == "--manifest" || args[i] == "-m") && i + 1 < args.Length)
            {
                manifestPath = Path.GetFullPath(args[++i]);
                workspaceDir = Path.GetDirectoryName(manifestPath) ?? workspaceDir;
            }
            else if ((args[i] == "--engine" || args[i] == "-e") && i + 1 < args.Length)
            {
                engineDir = Path.GetFullPath(args[++i]);
            }
            else if ((args[i] == "--profile") && i + 1 < args.Length)
            {
                profile = args[++i];
            }
            else if ((args[i] == "--workspace" || args[i] == "-w") && i + 1 < args.Length)
            {
                workspaceDir = Path.GetFullPath(args[++i]);
            }
        }

        if (string.IsNullOrEmpty(manifestPath))
        {
            manifestPath = Path.Combine(workspaceDir, "manifest.json");
        }

        if (string.IsNullOrEmpty(engineDir))
        {
            engineDir = FindEngineRoot(AppContext.BaseDirectory);
        }

        string logPath = Path.Combine(workspaceDir, ".arisen", "ArisenBuildTool.log");
        Logger.Initialize(logPath);
        Logger.Info($"ArisenBuildTool Generation Started. Workspace: {workspaceDir} | Profile: {profile}");
        Logger.Info($"Engine Root: {engineDir}");

        if (!File.Exists(manifestPath))
        {
            string arisenManifestPath = Path.Combine(workspaceDir, "project.arisen");
            if (File.Exists(arisenManifestPath)) manifestPath = arisenManifestPath;
            else
            {
                Logger.Error($"Project manifest not found at '{manifestPath}'. Build aborted.");
                return;
            }
        }

        ProjectManifest? manifest = null;
        try
        {
            string json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<ProjectManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest == null) throw new Exception("Deserialization returned null.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to parse ProjectManifest: {ex.Message}");
            return;
        }

        ExecuteGeneration(workspaceDir, engineDir, profile, manifest);
        Logger.Info("ArisenBuildTool: Workspace generation complete.");
    }

    static void ExecuteGeneration(string workspaceDir, string engineDir, string profile, ProjectManifest manifest)
    {
        string projectName = string.IsNullOrEmpty(manifest.Name) ? "MyGame" : manifest.Name;
        string projectsDir = Path.Combine(workspaceDir, ".arisen", "Projects", profile);
        Directory.CreateDirectory(projectsDir);

        var packageMap = PackageDiscoveryService.Discover(manifest, workspaceDir, engineDir, profile);
        var sortedPackages = PackageResolutionService.SortTopologically(packageMap);
        
        var outputDirs = new List<string>
        {
            Path.Combine(workspaceDir, ".arisen", "bin", profile, "Debug"),
            Path.Combine(workspaceDir, ".arisen", "bin", profile, "Release")
        };
        PackageResolutionService.SaveResolvedManifests(profile, outputDirs, sortedPackages);
        NativeDeploymentService.Deploy(sortedPackages, outputDirs, profile);

        var managedPackages = sortedPackages.Where(p => 
            p.Manifest.Type == "hybrid" || p.Manifest.Entry != null || 
            Directory.Exists(Path.Combine(p.DirectoryPath, "Managed")) || 
            Directory.GetFiles(p.DirectoryPath, "*.cs", SearchOption.AllDirectories).Length > 0
        ).ToList();
        var nativePackages = sortedPackages.Where(p => 
            p.Manifest.Type == "hybrid" || p.Manifest.Type == "native" || 
            File.Exists(Path.Combine(p.DirectoryPath, "CMakeLists.txt"))
        ).ToList();

        bool isEditor = false;
        if (manifest.Profiles != null && manifest.Profiles.TryGetValue(profile, out var profileDef))
        {
            isEditor = profileDef.IsEditor;
        }

        ProjectGeneratorService.GenerateForManagedPackages(workspaceDir, projectsDir, engineDir, managedPackages, packageMap, manifest, profile, isEditor);
        CMakeGeneratorService.Generate(engineDir, projectsDir, nativePackages, projectName, manifest, profile);
        SolutionGeneratorService.Generate(projectsDir, engineDir, managedPackages, projectName, manifest, profile, isEditor);

        Console.WriteLine($"ArisenBuildTool: Solution for '{projectName}' ({profile}) generated successfully.");
    }

    private static string FindEngineRoot(string startDir)
    {
        string current = startDir;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(Path.Combine(current, "ArisenKernel")))
            {
                return current;
            }
            if (Directory.Exists(Path.Combine(current, "..", "ArisenKernel")))
            {
                return Path.GetFullPath(Path.Combine(current, ".."));
            }

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}

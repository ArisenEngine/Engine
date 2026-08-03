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

        if (args.Length > 0 && args[0].Equals("validate", StringComparison.OrdinalIgnoreCase))
        {
            RunValidateMode(args);
            return;
        }

        if (args.Length > 0 && args[0].Equals("graph", StringComparison.OrdinalIgnoreCase))
        {
            RunGraphMode(args);
            return;
        }

        if (args.Length > 0 && args[0].Equals("pack", StringComparison.OrdinalIgnoreCase))
        {
            RunPackMode(args);
            return;
        }

        if (args.Length > 0 && args[0].Equals("registry-index", StringComparison.OrdinalIgnoreCase))
        {
            RunRegistryIndexMode(args);
            return;
        }

        if (args.Length > 0 && args[0].Equals("validate-native-output", StringComparison.OrdinalIgnoreCase))
        {
            RunValidateNativeOutputMode(args);
            return;
        }

        if (args.Length > 0 && args[0].Equals("finalize-native-output", StringComparison.OrdinalIgnoreCase))
        {
            RunFinalizeNativeOutputMode(args);
            return;
        }

        if (args.Length > 0 && args[0].Equals("deploy-runtime-metadata", StringComparison.OrdinalIgnoreCase))
        {
            RunDeployRuntimeMetadataMode(args);
            return;
        }

        if (args.Length > 0 && args[0].Equals("manifest-info", StringComparison.OrdinalIgnoreCase))
        {
            RunManifestInfoMode(args);
            return;
        }

        RunGenerateMode(args);
    }

    static void RunDeployRuntimeMetadataMode(string[] args)
    {
        string workspaceDir = string.Empty;
        string engineDir = string.Empty;
        string profile = string.Empty;
        string outputRoot = string.Empty;
        string configuration = string.Empty;
        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--workspace" || args[i] == "-w") && i + 1 < args.Length)
                workspaceDir = Path.GetFullPath(args[++i]);
            else if ((args[i] == "--engine" || args[i] == "-e") && i + 1 < args.Length)
                engineDir = Path.GetFullPath(args[++i]);
            else if (args[i] == "--profile" && i + 1 < args.Length)
                profile = args[++i];
            else if (args[i] == "--output-root" && i + 1 < args.Length)
                outputRoot = Path.GetFullPath(args[++i]);
            else if ((args[i] == "--configuration" || args[i] == "-c") && i + 1 < args.Length)
                configuration = args[++i];
        }

        if (string.IsNullOrWhiteSpace(workspaceDir) ||
            string.IsNullOrWhiteSpace(profile) ||
            string.IsNullOrWhiteSpace(outputRoot))
        {
            Console.Error.WriteLine(
                "ArisenBuildTool Runtime Metadata Error: --workspace, --profile, and --output-root are required.");
            Environment.ExitCode = 1;
            return;
        }

        if (string.IsNullOrWhiteSpace(engineDir))
        {
            engineDir = FindEngineRoot(AppContext.BaseDirectory);
        }

        Logger.Initialize(Path.Combine(workspaceDir, ".arisen", "ArisenBuildTool.log"));
        string manifestPath = Path.Combine(workspaceDir, "manifest.json");
        if (!TryReadManifest(manifestPath, out ProjectManifest? manifest) || manifest == null)
        {
            Environment.ExitCode = 1;
            return;
        }

        PackageValidationResult validation = PackageValidationService.Validate(
            manifest,
            workspaceDir,
            engineDir,
            profile);
        PackageValidationService.LogSummary(validation);
        if (!validation.Success)
        {
            Logger.Error("Runtime metadata deployment aborted because package validation failed.");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            RuntimePackageMetadataDeploymentResult result =
                RuntimePackageMetadataDeploymentService.Deploy(
                    manifest,
                    profile,
                    validation.SortedPackages,
                    outputRoot,
                    configuration);
            Logger.Info(
                $"Runtime metadata deployed {result.PackageCount} package descriptor(s) to '{result.OutputRoot}'.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Runtime metadata deployment failed: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    static void RunFinalizeNativeOutputMode(string[] args)
    {
        string workspaceDir = string.Empty;
        string engineDir = string.Empty;
        string profile = string.Empty;
        string configuration = string.Empty;
        string outputRoot = string.Empty;
        string manifestPath = string.Empty;
        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--workspace" || args[i] == "-w") && i + 1 < args.Length)
                workspaceDir = Path.GetFullPath(args[++i]);
            else if ((args[i] == "--engine" || args[i] == "-e") && i + 1 < args.Length)
                engineDir = Path.GetFullPath(args[++i]);
            else if (args[i] == "--profile" && i + 1 < args.Length)
                profile = args[++i];
            else if ((args[i] == "--configuration" || args[i] == "-c") && i + 1 < args.Length)
                configuration = args[++i];
            else if (args[i] == "--output-root" && i + 1 < args.Length)
                outputRoot = Path.GetFullPath(args[++i]);
            else if (args[i] == "--manifest" && i + 1 < args.Length)
                manifestPath = Path.GetFullPath(args[++i]);
        }

        if (string.IsNullOrWhiteSpace(workspaceDir) ||
            string.IsNullOrWhiteSpace(profile) ||
            string.IsNullOrWhiteSpace(configuration) ||
            string.IsNullOrWhiteSpace(outputRoot))
        {
            Console.Error.WriteLine(
                "ArisenBuildTool Native Output Finalization Error: --workspace, --profile, --configuration, and --output-root are required.");
            Environment.ExitCode = 1;
            return;
        }

        if (string.IsNullOrWhiteSpace(engineDir))
        {
            engineDir = FindEngineRoot(AppContext.BaseDirectory);
        }

        Logger.Initialize(Path.Combine(workspaceDir, ".arisen", "ArisenBuildTool.log"));
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            manifestPath = Path.Combine(workspaceDir, "manifest.json");
        }
        if (!TryReadManifest(manifestPath, out ProjectManifest? manifest) || manifest == null)
        {
            Environment.ExitCode = 1;
            return;
        }

        PackageValidationResult validation = PackageValidationService.Validate(
            manifest,
            workspaceDir,
            engineDir,
            profile);
        PackageValidationService.LogSummary(validation);
        if (!validation.Success)
        {
            Logger.Error("Native output finalization aborted because package validation failed.");
            Environment.ExitCode = 1;
            return;
        }

        bool enableProfiler = manifest.Profiles != null &&
            manifest.Profiles.TryGetValue(profile, out ProfileDefinition? profileDefinition) &&
            profileDefinition.EnableProfiler;

        NativePayloadInventoryResult inventory = NativePayloadIntegrityService.BuildInventory(
            validation.SortedPackages,
            outputRoot,
            configuration,
            enableProfiler);
        foreach (string warning in inventory.Warnings) Logger.Warning(warning);
        foreach (string error in inventory.Errors) Logger.Error(error);
        if (!inventory.Success)
        {
            Logger.Error("Native output finalization aborted because payload identity validation failed.");
            Environment.ExitCode = 1;
            return;
        }

        PackageResolutionService.SaveResolvedManifests(
            profile,
            new List<string> { outputRoot },
            validation.SortedPackages,
            nativePayloads: inventory.Payloads,
            nativePayloadsFinalized: true,
            configuration: configuration,
            enableProfiler: enableProfiler);

        string resolvedManifestPath = Path.Combine(outputRoot, "manifest.resolved.json");
        NativeOutputValidationResult outputValidation = NativeOutputValidationService.Validate(
            resolvedManifestPath,
            outputRoot,
            configuration);
        NativeOutputValidationService.LogSummary(outputValidation);
        if (!outputValidation.Success) Environment.ExitCode = 1;
    }

    static void RunManifestInfoMode(string[] args)
    {
        string manifestPath = string.Empty;
        string field = string.Empty;
        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--manifest" || args[i] == "-m") && i + 1 < args.Length)
                manifestPath = Path.GetFullPath(args[++i]);
            else if (args[i] == "--field" && i + 1 < args.Length)
                field = args[++i];
        }

        if (string.IsNullOrWhiteSpace(manifestPath) || string.IsNullOrWhiteSpace(field))
        {
            Console.Error.WriteLine(
                "ArisenBuildTool Manifest Info Error: --manifest <path> and --field <name|profiles> are required.");
            Environment.ExitCode = 1;
            return;
        }

        if (!TryReadManifest(manifestPath, out var manifest) || manifest == null)
        {
            Environment.ExitCode = 1;
            return;
        }

        if (field.Equals("name", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(string.IsNullOrWhiteSpace(manifest.Name) ? "MyGame" : manifest.Name);
            return;
        }

        if (field.Equals("profiles", StringComparison.OrdinalIgnoreCase))
        {
            var profiles = manifest.Profiles?.Keys.ToArray() ?? ["Development", "Production"];
            foreach (string profile in profiles)
            {
                Console.WriteLine(profile);
            }
            return;
        }

        Console.Error.WriteLine(
            $"ArisenBuildTool Manifest Info Error: unsupported field '{field}'.");
        Environment.ExitCode = 1;
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

        Logger.Initialize(Path.Combine(workspaceDir, ".arisen", "ArisenBuildTool.Test.log"));
        Logger.Info($"ArisenBuildTool: Isolated Test Generation for {packageId}");
        Logger.Info($"Workspace: {workspaceDir}");
        Logger.Info($"Engine Root: {engineDir}");

        string testPackageId = packageId.EndsWith(".test", StringComparison.OrdinalIgnoreCase) ? packageId : $"{packageId}.test";
        if (!ValidateLocalTestPackage(workspaceDir, packageId, isRequired: true))
        {
            Environment.Exit(1);
        }

        if (!ValidateLocalTestPackage(workspaceDir, testPackageId, isRequired: true))
        {
            Logger.Error($"Expected companion test package '{testPackageId}' at '{Path.Combine(workspaceDir, "Local", testPackageId)}'.");
            Logger.Error("Create the companion .test package or pass an existing package id that already ends with '.test'.");
            Environment.Exit(1);
        }

        foreach (var requiredInfrastructurePackage in new[] { "com.arisen.core", "com.arisen.testrunner" })
        {
            if (!ValidateLocalTestPackage(workspaceDir, requiredInfrastructurePackage, isRequired: true))
            {
                Logger.Error($"Package test generation requires local infrastructure package '{requiredInfrastructurePackage}'.");
                Environment.Exit(1);
            }
        }

        Logger.Info($"Companion test package: {testPackageId}");

        // Create a Virtual Manifest for this test run
        var testManifest = new ProjectManifest
        {
            Name = $"{packageId}.TestRun",
            EngineVersion = "Current",
            Packages = new List<PackageRequirement>
            {
                new PackageRequirement { Id = "com.arisen.core", Url = "file://Local/com.arisen.core", Version = "1.0.0" },
                new PackageRequirement { Id = packageId, Url = $"file://Local/{packageId}", Version = "1.0.0" },
                new PackageRequirement { Id = testPackageId, Url = $"file://Local/{testPackageId}", Version = "1.0.0" },
                new PackageRequirement { Id = "com.arisen.testrunner", Url = "file://Local/com.arisen.testrunner", Version = "1.0.0" }
            },
            Profiles = new Dictionary<string, ProfileDefinition>
            {
                ["Testing"] = new ProfileDefinition()
            }
        };

        Logger.Info("Virtual test manifest packages:");
        foreach (var package in testManifest.Packages)
        {
            Logger.Info($"  - {package.Id} ({package.Url})");
        }

        string testProjectsDir = Path.Combine(workspaceDir, ".arisen", "Projects", "Testing");
        Directory.CreateDirectory(testProjectsDir);
        string testManifestPath = Path.Combine(testProjectsDir, "manifest.test.json");
        File.WriteAllText(
            testManifestPath,
            JsonSerializer.Serialize(
                testManifest,
                new JsonSerializerOptions { WriteIndented = true }));

        // Reuse the generate logic with the persisted virtual manifest so post-build
        // finalization validates the same graph that generated the isolated workspace.
        ExecuteGeneration(
            workspaceDir,
            engineDir,
            "Testing",
            testManifest,
            testManifestPath);
    }

    private static bool ValidateLocalTestPackage(string workspaceDir, string packageId, bool isRequired)
    {
        string packageDir = Path.Combine(workspaceDir, "Local", packageId);
        string packageJsonPath = Path.Combine(packageDir, "package.json");

        if (Directory.Exists(packageDir) && File.Exists(packageJsonPath))
        {
            return true;
        }

        if (isRequired)
        {
            Logger.Error($"Required local package '{packageId}' was not found at '{packageDir}', or it is missing package.json.");
        }
        else
        {
            Logger.Warning($"Optional local package '{packageId}' was not found at '{packageDir}', or it is missing package.json.");
        }

        return false;
    }

    static void RunValidateMode(string[] args)
    {
        string workspaceDir = Path.GetFullPath(".");
        string manifestPath = string.Empty;
        string engineDir = string.Empty;
        string profile = "Development";

        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--manifest" || args[i] == "-m") && i + 1 < args.Length)
            {
                manifestPath = Path.GetFullPath(args[++i]);
                workspaceDir = Path.GetDirectoryName(manifestPath) ?? workspaceDir;
            }
            else if ((args[i] == "--engine" || args[i] == "-e") && i + 1 < args.Length)
            {
                engineDir = Path.GetFullPath(args[++i]);
            }
            else if (args[i] == "--profile" && i + 1 < args.Length)
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

        Logger.Initialize(Path.Combine(workspaceDir, ".arisen", "ArisenBuildTool.Validate.log"));
        Logger.Info($"ArisenBuildTool Validation Started. Workspace: {workspaceDir} | Profile: {profile}");
        Logger.Info($"Engine Root: {engineDir}");

        if (!TryReadManifest(manifestPath, out var manifest))
        {
            Environment.Exit(1);
        }

        var result = PackageValidationService.Validate(manifest!, workspaceDir, engineDir, profile);
        PackageValidationService.LogSummary(result);
        Environment.Exit(result.Success ? 0 : 1);
    }

    static void RunGraphMode(string[] args)
    {
        string workspaceDir = Path.GetFullPath(".");
        string manifestPath = string.Empty;
        string engineDir = string.Empty;
        string profile = "Development";
        string format = "text";
        string outputPath = string.Empty;

        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--manifest" || args[i] == "-m") && i + 1 < args.Length)
            {
                manifestPath = Path.GetFullPath(args[++i]);
                workspaceDir = Path.GetDirectoryName(manifestPath) ?? workspaceDir;
            }
            else if ((args[i] == "--engine" || args[i] == "-e") && i + 1 < args.Length)
            {
                engineDir = Path.GetFullPath(args[++i]);
            }
            else if (args[i] == "--profile" && i + 1 < args.Length)
            {
                profile = args[++i];
            }
            else if ((args[i] == "--workspace" || args[i] == "-w") && i + 1 < args.Length)
            {
                workspaceDir = Path.GetFullPath(args[++i]);
            }
            else if ((args[i] == "--format" || args[i] == "-f") && i + 1 < args.Length)
            {
                format = args[++i];
            }
            else if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
            {
                outputPath = Path.GetFullPath(args[++i]);
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

        Logger.Initialize(Path.Combine(workspaceDir, ".arisen", "ArisenBuildTool.Graph.log"));
        Logger.Info($"ArisenBuildTool Graph Started. Workspace: {workspaceDir} | Profile: {profile} | Format: {format}");
        Logger.Info($"Engine Root: {engineDir}");

        if (!TryReadManifest(manifestPath, out var manifest))
        {
            Environment.Exit(1);
        }

        var validation = PackageValidationService.Validate(manifest!, workspaceDir, engineDir, profile);
        if (!validation.Success)
        {
            PackageValidationService.LogSummary(validation);
            Environment.Exit(1);
        }

        string graph;
        try
        {
            graph = PackageGraphService.Render(validation, profile, format);
        }
        catch (ArgumentException ex)
        {
            Logger.Error(ex.Message);
            Environment.Exit(1);
            return;
        }

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllText(outputPath, graph);
            Logger.Info($"Wrote package graph to {outputPath}");
        }
        else
        {
            Console.WriteLine(graph);
        }
    }

    static void RunPackMode(string[] args)
    {
        string workspaceDir = Path.GetFullPath(".");
        string manifestPath = string.Empty;
        string engineDir = string.Empty;
        string profile = "Development";
        string packageId = string.Empty;
        string outputDir = string.Empty;
        bool overwrite = false;

        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--package" || args[i] == "-p") && i + 1 < args.Length)
            {
                packageId = args[++i];
            }
            else if ((args[i] == "--manifest" || args[i] == "-m") && i + 1 < args.Length)
            {
                manifestPath = Path.GetFullPath(args[++i]);
                workspaceDir = Path.GetDirectoryName(manifestPath) ?? workspaceDir;
            }
            else if ((args[i] == "--engine" || args[i] == "-e") && i + 1 < args.Length)
            {
                engineDir = Path.GetFullPath(args[++i]);
            }
            else if (args[i] == "--profile" && i + 1 < args.Length)
            {
                profile = args[++i];
            }
            else if ((args[i] == "--workspace" || args[i] == "-w") && i + 1 < args.Length)
            {
                workspaceDir = Path.GetFullPath(args[++i]);
            }
            else if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
            {
                outputDir = Path.GetFullPath(args[++i]);
            }
            else if (args[i] == "--overwrite")
            {
                overwrite = true;
            }
        }

        if (string.IsNullOrWhiteSpace(packageId))
        {
            Console.WriteLine("ArisenBuildTool Pack Error: --package <Id> is required.");
            Environment.Exit(1);
        }

        if (string.IsNullOrEmpty(manifestPath))
        {
            manifestPath = Path.Combine(workspaceDir, "manifest.json");
        }

        if (string.IsNullOrEmpty(engineDir))
        {
            engineDir = FindEngineRoot(AppContext.BaseDirectory);
        }

        if (string.IsNullOrEmpty(outputDir))
        {
            outputDir = Path.Combine(workspaceDir, ".arisen", "Packages");
        }

        Logger.Initialize(Path.Combine(workspaceDir, ".arisen", "ArisenBuildTool.Pack.log"));
        Logger.Info($"ArisenBuildTool Pack Started. Workspace: {workspaceDir} | Profile: {profile} | Package: {packageId}");
        Logger.Info($"Engine Root: {engineDir}");
        Logger.Info($"Output Directory: {outputDir}");

        if (!TryReadManifest(manifestPath, out var manifest))
        {
            Environment.Exit(1);
        }

        var validation = PackageValidationService.Validate(manifest!, workspaceDir, engineDir, profile);
        PackageValidationService.LogSummary(validation);
        if (!validation.Success)
        {
            Logger.Error("ArisenBuildTool: Package pack aborted because package validation failed.");
            Environment.Exit(1);
        }

        if (!validation.PackageMap.TryGetValue(packageId, out var package))
        {
            Logger.Error($"Package '{packageId}' is not selected by workspace profile '{profile}'.");
            Environment.Exit(1);
            return;
        }

        try
        {
            string archivePath = PackagePackService.Pack(package, outputDir, overwrite);
            Logger.Info($"Packed package '{packageId}' to {archivePath}");
            Console.WriteLine(archivePath);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to pack package '{packageId}': {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void RunRegistryIndexMode(string[] args)
    {
        string sourceDir = string.Empty;
        string outputPath = string.Empty;
        string baseUrl = string.Empty;
        bool overwrite = false;

        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--source" || args[i] == "-s") && i + 1 < args.Length)
            {
                sourceDir = Path.GetFullPath(args[++i]);
            }
            else if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
            {
                outputPath = Path.GetFullPath(args[++i]);
            }
            else if (args[i] == "--base-url" && i + 1 < args.Length)
            {
                baseUrl = args[++i];
            }
            else if (args[i] == "--overwrite")
            {
                overwrite = true;
            }
        }

        if (string.IsNullOrWhiteSpace(sourceDir))
        {
            Console.WriteLine("ArisenBuildTool Registry Index Error: --source <Directory> is required.");
            Environment.Exit(1);
        }

        if (!Directory.Exists(sourceDir))
        {
            Console.WriteLine($"ArisenBuildTool Registry Index Error: source directory not found: {sourceDir}");
            Environment.Exit(1);
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(sourceDir, "registry.json");
        }

        Logger.Initialize(Path.Combine(sourceDir, "ArisenBuildTool.RegistryIndex.log"));
        Logger.Info($"ArisenBuildTool Registry Index Started. Source: {sourceDir}");
        Logger.Info($"Output: {outputPath}");
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            Logger.Info($"Base URL: {baseUrl}");
        }

        try
        {
            PackageRegistryIndexService.Write(sourceDir, outputPath, baseUrl, overwrite);
            Logger.Info($"Wrote package registry index to {outputPath}");
            Console.WriteLine(outputPath);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to write package registry index: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void RunValidateNativeOutputMode(string[] args)
    {
        string resolvedManifestPath = string.Empty;
        string outputDir = string.Empty;
        string configuration = string.Empty;

        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "--resolved-manifest" || args[i] == "-m") && i + 1 < args.Length)
            {
                resolvedManifestPath = Path.GetFullPath(args[++i]);
            }
            else if ((args[i] == "--output-dir" || args[i] == "-o") && i + 1 < args.Length)
            {
                outputDir = Path.GetFullPath(args[++i]);
            }
            else if ((args[i] == "--configuration" || args[i] == "-c") && i + 1 < args.Length)
            {
                configuration = args[++i];
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedManifestPath))
        {
            Console.WriteLine("ArisenBuildTool Native Output Validation Error: --resolved-manifest <Path> is required.");
            Environment.Exit(1);
        }

        if (string.IsNullOrWhiteSpace(outputDir))
        {
            Console.WriteLine("ArisenBuildTool Native Output Validation Error: --output-dir <Directory> is required.");
            Environment.Exit(1);
        }

        string logDir = Path.GetDirectoryName(resolvedManifestPath) ?? Path.GetFullPath(".");
        Logger.Initialize(Path.Combine(logDir, "ArisenBuildTool.NativeOutputValidate.log"));
        Logger.Info($"ArisenBuildTool Native Output Validation Started. Manifest: {resolvedManifestPath}");
        Logger.Info($"Output Directory: {outputDir}");
        if (!string.IsNullOrWhiteSpace(configuration)) Logger.Info($"Configuration: {configuration}");

        var result = NativeOutputValidationService.Validate(resolvedManifestPath, outputDir, configuration);
        NativeOutputValidationService.LogSummary(result);
        Environment.Exit(result.Success ? 0 : 1);
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

        if (!TryReadManifest(manifestPath, out var manifest))
        {
            Environment.Exit(1);
        }

        ExecuteGeneration(workspaceDir, engineDir, profile, manifest!);
        Logger.Info("ArisenBuildTool: Workspace generation complete.");
    }

    static void ExecuteGeneration(
        string workspaceDir,
        string engineDir,
        string profile,
        ProjectManifest manifest,
        string? finalizationManifestPath = null)
    {
        string projectName = string.IsNullOrEmpty(manifest.Name) ? "MyGame" : manifest.Name;
        string projectsDir = Path.Combine(workspaceDir, ".arisen", "Projects", profile);
        Directory.CreateDirectory(projectsDir);

        var validation = PackageValidationService.Validate(manifest, workspaceDir, engineDir, profile);
        PackageValidationService.LogSummary(validation);
        if (!validation.Success)
        {
            Logger.Error("ArisenBuildTool: Workspace generation aborted because package validation failed.");
            Environment.Exit(1);
        }

        var packageMap = validation.PackageMap;
        var sortedPackages = validation.SortedPackages;

        bool isEditor = false;
        bool enableProfiler = false;
        if (manifest.Profiles != null && manifest.Profiles.TryGetValue(profile, out var profileDef))
        {
            isEditor = profileDef.IsEditor;
            enableProfiler = profileDef.EnableProfiler;
        }

        var outputDirs = new List<string>
        {
            Path.Combine(workspaceDir, ".arisen", "bin", profile, "Debug"),
            Path.Combine(workspaceDir, ".arisen", "bin", profile, "Release")
        };
        PackageResolutionService.SaveResolvedManifests(
            profile,
            outputDirs,
            sortedPackages,
            enableProfiler: enableProfiler);
        PackageResolutionService.SaveResolvedManifests(
            profile,
            new List<string> { projectsDir },
            sortedPackages,
            "manifest.source.resolved.json",
            enableProfiler: enableProfiler);
        NativeDeploymentService.Deploy(sortedPackages, outputDirs, profile, enableProfiler);

        // B18: Generate launch.config.json in binary folders for explicit profile/workspace resolution
        foreach (var dir in outputDirs)
        {
            Directory.CreateDirectory(dir);
            var launchConfig = new Dictionary<string, object?>
            {
                ["Profile"] = profile,
                ["Workspace"] = Path.GetFullPath(workspaceDir)
            };
            if (!string.IsNullOrWhiteSpace(finalizationManifestPath))
            {
                launchConfig["Manifest"] = Path.GetFullPath(finalizationManifestPath);
            }
            string configJson = JsonSerializer.Serialize(launchConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(dir, "launch.config.json"), configJson);
        }

        var managedPackages = sortedPackages.Where(p =>
            p.Manifest.Type == "hybrid" || p.Manifest.Entry != null ||
            Directory.Exists(Path.Combine(p.DirectoryPath, "Managed")) ||
            Directory.GetFiles(p.DirectoryPath, "*.cs", SearchOption.AllDirectories).Length > 0
        ).ToList();
        var nativePackages = sortedPackages.Where(p =>
            p.Manifest.Type == "hybrid" || p.Manifest.Type == "native" ||
            File.Exists(Path.Combine(p.DirectoryPath, "CMakeLists.txt"))
        ).ToList();

        ProjectGeneratorService.GenerateForManagedPackages(workspaceDir, projectsDir, engineDir, managedPackages, packageMap, manifest, profile, isEditor, enableProfiler);
        CMakeGeneratorService.Generate(engineDir, projectsDir, nativePackages, projectName, manifest, profile, enableProfiler);
        SolutionGeneratorService.Generate(
            workspaceDir,
            projectsDir,
            engineDir,
            managedPackages,
            projectName,
            manifest,
            profile,
            isEditor,
            enableProfiler,
            finalizationManifestPath);

        Console.WriteLine($"ArisenBuildTool: Solution for '{projectName}' ({profile}) generated successfully.");
    }

    private static bool TryReadManifest(string manifestPath, out ProjectManifest? manifest)
    {
        manifest = null;
        if (!File.Exists(manifestPath))
        {
            string workspaceDir = Path.GetDirectoryName(manifestPath) ?? Path.GetFullPath(".");
            string arisenManifestPath = Path.Combine(workspaceDir, "project.arisen");
            if (File.Exists(arisenManifestPath)) manifestPath = arisenManifestPath;
            else
            {
                Logger.Error($"Project manifest not found at '{manifestPath}'.");
                return false;
            }
        }

        try
        {
            manifest = ManifestJson.DeserializeFile<ProjectManifest>(manifestPath);
            if (manifest == null) throw new Exception("Deserialization returned null.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to parse ProjectManifest: {ex.Message}");
            return false;
        }
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

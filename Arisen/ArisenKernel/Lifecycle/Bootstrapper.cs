using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArisenKernel.Contracts;
using ArisenKernel.Diagnostics;
using ArisenKernel.Services;
using ArisenKernel.Packages;

namespace ArisenKernel.Lifecycle;

public static class EngineBootstrapper
{
    public static void Run(string[] args)
    {
        KernelLog.Info("=== Arisen Engine Bootstrapper ===");
        
        string workspacePath = "";
        string entryPackage = "";
        string profile = "Development";
        bool profileSpecified = false;
        bool workspaceSpecified = false;
        bool allowResolvedManifestFallback = false;
        RuntimeSmokeOptions smokeOptions;
        try
        {
            smokeOptions = RuntimeSmokeOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            KernelLog.FatalFormat("[Host] FATAL ERROR: {0}", ex.Message);
            Environment.Exit(1);
            return;
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--workspace" && i + 1 < args.Length) { workspacePath = args[i + 1]; workspaceSpecified = true; }
            if (args[i] == "--entry" && i + 1 < args.Length) entryPackage = args[i + 1];
            if (args[i] == "--profile" && i + 1 < args.Length) { profile = args[i + 1]; profileSpecified = true; }
            if (args[i] == "--allow-manifest-fallback") allowResolvedManifestFallback = true;
        }

        // B18: Try to load from launch.config.json if located in the binary folder (Explicit configuration wins over deduction)
        string configPath = Path.Combine(AppContext.BaseDirectory, "launch.config.json");
        if (File.Exists(configPath))
        {
            try
            {
                using var configDoc = JsonDocument.Parse(File.ReadAllText(configPath), ManifestJson.DocumentOptions);
                var root = configDoc.RootElement;
                if (!profileSpecified && root.TryGetProperty("Profile", out var pProp)) profile = pProp.GetString() ?? profile;
                if (!workspaceSpecified && root.TryGetProperty("Workspace", out var wProp)) workspacePath = wProp.GetString() ?? workspacePath;
            }
            catch { /* Skip and fall back to deduction */ }
        }

        if (string.IsNullOrEmpty(workspacePath))
        {
            // NEW: In generated projects, we are in .arisen/bin/{profile}/{config}/
            // .. (config) -> .. (profile) -> .. (bin) -> .. (.arisen) -> Workspace Root
            workspacePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            KernelLog.InfoFormat("[Host] No --workspace provided. Deducing from location: {0}", workspacePath);
        }

        // 1. Initialize Kernel and Core Project Subsystem
        var kernel = EngineKernel.Instance;
        var registry = kernel.Services;
        
        var projectSubsystem = new ProjectSubsystem();
        registry.RegisterService<ProjectSubsystem>(projectSubsystem);
        projectSubsystem.LoadFromWorkspace(workspacePath);

        // B15: Initialize PackageSubsystem to track all loaded packages for other systems (like the Editor)
        var packageSubsystem = new PackageSubsystem();
        kernel.RegisterSubsystem(packageSubsystem);

        string manifestPath = Path.Combine(workspacePath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            KernelLog.FatalFormat("[Host] FATAL ERROR: Cannot find manifest.json at {0}", manifestPath);
            Environment.Exit(1);
        }

        KernelLog.InfoFormat("[Host] Reading Workspace Manifest: {0}", manifestPath);
        var manifestJson = ManifestJson.ParseDocumentFile(manifestPath);
        var packagesElement = manifestJson.RootElement.GetProperty("Packages");
        
        List<string> packageUrls = new();
        void AddPackages(JsonElement element)
        {
            foreach (var pkg in element.EnumerateArray())
            {
                var url = pkg.GetProperty("Url").GetString();
                if (!string.IsNullOrEmpty(url))
                {
                    if (url.StartsWith("file://"))
                    {
                        string localPath = url.Substring(7);
                        if (Path.IsPathRooted(localPath)) packageUrls.Add(localPath);
                        else packageUrls.Add(Path.Combine(workspacePath, localPath));
                    }
                    else
                    {
                        // TODO: Handle cache/URL packages
                        packageUrls.Add(url);
                    }
                }
            }
        }

        AddPackages(packagesElement);

        // Load Profile Packages
        if (manifestJson.RootElement.TryGetPropertyIC("Profiles", out var profilesElement))
        {
            if (profilesElement.TryGetPropertyIC(profile, out var profileDefinition))
            {
                KernelLog.InfoFormat("[Host] Loading Profile: {0}", profile);
                
                // NEW: Handle ProfileDefinition object (Packages, etc)
                if (profileDefinition.ValueKind == JsonValueKind.Object)
                {
                    if (profileDefinition.TryGetPropertyIC("Packages", out var profilePackages))
                    {
                        AddPackages(profilePackages);
                    }
                }
                else if (profileDefinition.ValueKind == JsonValueKind.Array)
                {
                    // Legacy support for raw package arrays in profiles
                    AddPackages(profileDefinition);
                }
            }
            else if (profile != "Development" && profile != "Production")
            {
                KernelLog.WarningFormat("[Host] WARNING: Profile '{0}' not found in manifest.json.", profile);
            }
        }

        // B11: Check for resolved manifest to skip runtime resolution and use topological order
        // PRIORITY 1: Local manifest.resolved.json co-located with binary (Modernized approach)
        string resolvedManifestPath = Path.Combine(AppContext.BaseDirectory, "manifest.resolved.json");
        
        // PRIORITY 2: Fallback to root naming convention for legacy/debug support
        if (!File.Exists(resolvedManifestPath))
        {
            resolvedManifestPath = Path.Combine(workspacePath, $"manifest.resolved.{profile}.json");
        }

        if (File.Exists(resolvedManifestPath))
        {
            if (!TryLoadResolvedPackageUrls(resolvedManifestPath, workspacePath, profile, packageUrls, out var resolvedError))
            {
                if (allowResolvedManifestFallback)
                {
                    KernelLog.WarningFormat("[Host] Failed to parse resolved manifest '{0}': {1}. Falling back to manifest.json order because --allow-manifest-fallback was specified.", resolvedManifestPath, resolvedError);
                }
                else
                {
                    KernelLog.FatalFormat("[Host] FATAL ERROR: Resolved manifest '{0}' is invalid: {1}", resolvedManifestPath, resolvedError);
                    Environment.Exit(1);
                }
            }
        }
        else
        {
            KernelLog.WarningFormat("[Host] No resolved manifest found for profile '{0}'. Runtime will use raw manifest package order.", profile);
        }

        RuntimeVisualSummaryService? visualSummaryService = null;
        if (smokeOptions.CaptureVisualSummary)
        {
            visualSummaryService = new RuntimeVisualSummaryService(
                workspacePath,
                profile,
                smokeOptions.EffectiveFrameCount - 1);
            registry.RegisterService<IRuntimeVisualSummaryService>(visualSummaryService);
            KernelLog.InfoFormat(
                "[Host] Visual summary requested for frame {0}. Output: {1}",
                visualSummaryService.CaptureFrameIndex,
                visualSummaryService.OutputPath);
        }

        // 2. Initialize Kernel (The kernel now handles topological package loading)
        kernel.Initialize(new EngineConfig
        {
            ProjectRoot = workspacePath,
            ProjectName = Path.GetFileName(workspacePath),
            PackageUrls = packageUrls,
            Platform = RuntimePlatform.Windows // TODO: Deduce from OS
        });

        KernelLog.Info("[Host] Kernel Initialization Complete.");

        KernelLog.Info("[Host] Topological Mount Complete.");
        LogRuntimeDiagnostics(kernel, packageSubsystem, workspacePath, profile, resolvedManifestPath, smokeOptions);

        if (smokeOptions.Enabled)
        {
            KernelLog.InfoFormat(
                "[Host] Smoke mode '{0}' requested. Running {1} frame(s) and exiting without application-host handoff.",
                smokeOptions.ModeName,
                smokeOptions.EffectiveFrameCount);

            if (smokeOptions.EffectiveFrameCount != smokeOptions.RequestedFrameCount)
            {
                KernelLog.InfoFormat(
                    "[Host] Smoke mode '{0}' raised requested frame count from {1} to {2}.",
                    smokeOptions.ModeName,
                    smokeOptions.RequestedFrameCount,
                    smokeOptions.EffectiveFrameCount);
            }

            if (smokeOptions.Mode == RuntimeSmokeMode.HotReload)
            {
                KernelLog.Warning("[Host] Hot-reload smoke currently exercises multi-frame scene stability. File-change recook/reload smoke awaits a runtime-owned asset-change harness.");
            }

            var smokeExitCode = kernel.RunForFrames(smokeOptions.EffectiveFrameCount);
            if (visualSummaryService != null)
            {
                if (!visualSummaryService.IsComplete)
                {
                    visualSummaryService.ReportFailure(
                        $"No native render surface captured requested frame {visualSummaryService.CaptureFrameIndex}.");
                }

                if (!visualSummaryService.Succeeded)
                {
                    KernelLog.FatalFormat(
                        "[Host] Visual summary failed: {0}",
                        visualSummaryService.FailureMessage ?? "unknown visual-summary failure");
                    smokeExitCode = 1;
                }
                else
                {
                    KernelLog.InfoFormat(
                        "[Host] Visual summary passed: {0}",
                        visualSummaryService.OutputPath);
                }
            }

            Environment.ExitCode = smokeExitCode;
            return;
        }

        // 3. Fallback to registry checks for boot takeover
        if (registry.TryGetService<IApplicationHost>(out var appHost))
        {
            KernelLog.Info("[Host] Yielding main thread to IApplicationHost (Editor/UI).");
            appHost.Run(args);
        }
        else
        {
            KernelLog.Info("[Host] No IApplicationHost detected. Engaging default bare-metal Engine tick.");
            kernel.Run();
        }
    }

    private static void LogRuntimeDiagnostics(
        EngineKernel kernel,
        PackageSubsystem packageSubsystem,
        string workspacePath,
        string profile,
        string resolvedManifestPath,
        RuntimeSmokeOptions smokeOptions)
    {
        KernelLog.Info("[Host] Runtime diagnostics:");
        KernelLog.InfoFormat("  Workspace: {0}", workspacePath);
        KernelLog.InfoFormat("  Profile: {0}", profile);
        KernelLog.InfoFormat("  SmokeMode: {0}", smokeOptions.Enabled);
        KernelLog.InfoFormat("  SmokeKind: {0}", smokeOptions.Enabled ? smokeOptions.ModeName : "<none>");
        KernelLog.InfoFormat("  SmokeFramesRequested: {0}", smokeOptions.Enabled ? smokeOptions.RequestedFrameCount : 0);
        KernelLog.InfoFormat("  SmokeFramesEffective: {0}", smokeOptions.Enabled ? smokeOptions.EffectiveFrameCount : 0);
        KernelLog.InfoFormat("  VisualSummary: {0}", smokeOptions.CaptureVisualSummary);
        KernelLog.InfoFormat("  ResolvedManifest: {0}", File.Exists(resolvedManifestPath) ? resolvedManifestPath : "<not found>");

        KernelLog.Info("  Package load order:");
        int packageIndex = 1;
        foreach (var package in packageSubsystem.GetLoadedPackagesInOrder())
        {
            KernelLog.InfoFormat("    {0}. {1} ({2}, {3})", packageIndex++, package.Id, package.Type, package.Version);
        }

        KernelLog.Info("  Subsystem init order:");
        int subsystemIndex = 1;
        foreach (var subsystem in kernel.GetInitializedSubsystemDiagnostics())
        {
            KernelLog.InfoFormat(
                "    {0}. {1} (Package: {2}, Phase: {3}, Priority: {4})",
                subsystemIndex++,
                subsystem.ClassName,
                string.IsNullOrWhiteSpace(subsystem.PackageId) ? "<kernel>" : subsystem.PackageId,
                subsystem.InitPhase,
                subsystem.Priority);
        }

        KernelLog.Info("  Registered services:");
        int serviceIndex = 1;
        foreach (var service in kernel.Services.GetRegisteredServices().OrderBy(x => x.ContractName, StringComparer.Ordinal))
        {
            KernelLog.InfoFormat(
                "    {0}. {1} -> {2} (Provider: {3})",
                serviceIndex++,
                service.ContractName,
                service.ImplementationName,
                string.IsNullOrWhiteSpace(service.ProviderPackageId) ? "<kernel>" : service.ProviderPackageId);
        }
    }

    private static bool TryLoadResolvedPackageUrls(string resolvedManifestPath, string workspacePath, string profile, List<string> packageUrls, out string error)
    {
        error = string.Empty;

        try
        {
            KernelLog.InfoFormat("[Host] Found Resolved Manifest: {0}. Using build-time topological sort.", resolvedManifestPath);
            using var resolvedJson = JsonDocument.Parse(File.ReadAllText(resolvedManifestPath), ManifestJson.DocumentOptions);
            var root = resolvedJson.RootElement;

            if (root.TryGetPropertyIC("Profile", out var resolvedProfileElement))
            {
                string? resolvedProfile = resolvedProfileElement.GetString();
                if (!string.IsNullOrWhiteSpace(resolvedProfile) && !string.Equals(resolvedProfile, profile, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"profile mismatch. Requested '{profile}', resolved manifest contains '{resolvedProfile}'.";
                    return false;
                }
            }

            if (!root.TryGetPropertyIC("ResolvedPackages", out var resolvedPackages) || resolvedPackages.ValueKind != JsonValueKind.Array)
            {
                error = "missing or invalid ResolvedPackages array.";
                return false;
            }

            var resolvedPackageUrls = new List<string>();
            string manifestDir = Path.GetDirectoryName(resolvedManifestPath)!;
            foreach (var package in resolvedPackages.EnumerateArray())
            {
                if (!package.TryGetPropertyIC("Url", out var urlElement) || urlElement.ValueKind != JsonValueKind.String)
                {
                    error = "resolved package entry is missing Url.";
                    return false;
                }

                string? url = urlElement.GetString();
                if (string.IsNullOrWhiteSpace(url))
                {
                    error = "resolved package entry contains an empty Url.";
                    return false;
                }

                string resolvedPath = ResolvePackageUrl(url, manifestDir, workspacePath);
                if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    string packageJsonPath = Path.Combine(resolvedPath, "package.json");
                    if (!File.Exists(packageJsonPath))
                    {
                        error = $"resolved package path '{resolvedPath}' does not contain package.json.";
                        return false;
                    }
                }

                resolvedPackageUrls.Add(resolvedPath);
            }

            if (resolvedPackageUrls.Count == 0)
            {
                error = "ResolvedPackages is empty.";
                return false;
            }

            packageUrls.Clear();
            packageUrls.AddRange(resolvedPackageUrls);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string ResolvePackageUrl(string url, string manifestDir, string workspacePath)
    {
        if (!url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return url;

        string localPath = Uri.UnescapeDataString(url.Substring(7));
        if (Path.IsPathRooted(localPath)) return Path.GetFullPath(localPath);

        string manifestRelativePath = Path.GetFullPath(Path.Combine(manifestDir, localPath));
        if (Directory.Exists(manifestRelativePath) || File.Exists(Path.Combine(manifestRelativePath, "package.json")))
        {
            return manifestRelativePath;
        }

        return Path.GetFullPath(Path.Combine(workspacePath, localPath));
    }
}

public static class JsonExtensions
{
    public static bool TryGetPropertyIC(this JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    public static JsonElement GetPropertyIC(this JsonElement element, string propertyName)
    {
        if (TryGetPropertyIC(element, propertyName, out var value)) return value;
        throw new KeyNotFoundException($"Property '{propertyName}' not found (case-insensitive)");
    }
}

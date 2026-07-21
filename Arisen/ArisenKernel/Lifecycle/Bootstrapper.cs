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

public sealed record EnginePackageGraphResolution(
    string WorkspacePath,
    string Profile,
    string ManifestPath,
    string ResolvedManifestPath,
    IReadOnlyList<string> PackageUrls);

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
        bool deployedLaunch = false;
        RuntimeSmokeOptions smokeOptions;
        RuntimeAssetOptions runtimeAssetOptions;
        try
        {
            smokeOptions = RuntimeSmokeOptions.Parse(args);
            runtimeAssetOptions = RuntimeAssetOptions.Parse(args);
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
                string? configuredProfile = root.TryGetProperty("Profile", out var pProp)
                    ? pProp.GetString()
                    : null;
                deployedLaunch = root.TryGetProperty("Mode", out var modeProperty) &&
                    string.Equals(modeProperty.GetString(), "Deployed", StringComparison.Ordinal);

                if (deployedLaunch && workspaceSpecified)
                {
                    throw new InvalidOperationException(
                        "A deployed launch does not permit --workspace. Runtime metadata is rooted beside the executable.");
                }

                if (deployedLaunch && profileSpecified &&
                    !string.IsNullOrWhiteSpace(configuredProfile) &&
                    !string.Equals(profile, configuredProfile, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"A deployed launch targets profile '{configuredProfile}', not '{profile}'.");
                }

                if (!profileSpecified && !string.IsNullOrWhiteSpace(configuredProfile))
                {
                    profile = configuredProfile;
                }

                if (deployedLaunch)
                {
                    workspacePath = Path.GetFullPath(AppContext.BaseDirectory);
                    KernelLog.InfoFormat(
                        "[Host] Using deployed runtime metadata rooted at: {0}",
                        workspacePath);
                }
                else if (!workspaceSpecified &&
                         root.TryGetProperty("Workspace", out var wProp) &&
                         !string.IsNullOrWhiteSpace(wProp.GetString()))
                {
                    string configuredWorkspace = wProp.GetString()!;
                    workspacePath = Path.IsPathRooted(configuredWorkspace)
                        ? Path.GetFullPath(configuredWorkspace)
                        : Path.GetFullPath(Path.Combine(
                            Path.GetDirectoryName(configPath)!,
                            configuredWorkspace));
                }
            }
            catch (Exception ex)
            {
                KernelLog.FatalFormat(
                    "[Host] FATAL ERROR: Invalid launch configuration '{0}': {1}",
                    configPath,
                    ex.Message);
                Environment.Exit(1);
                return;
            }
        }

        try
        {
            runtimeAssetOptions.Validate(profile, deployedLaunch);
        }
        catch (InvalidOperationException ex)
        {
            KernelLog.FatalFormat("[Host] FATAL ERROR: {0}", ex.Message);
            Environment.Exit(1);
            return;
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

        EnginePackageGraphResolution packageGraph;
        try
        {
            packageGraph = ResolvePackageGraph(
                workspacePath,
                profile,
                allowResolvedManifestFallback);
        }
        catch (Exception ex)
        {
            KernelLog.FatalFormat("[Host] FATAL ERROR: {0}", ex.Message);
            Environment.Exit(1);
            return;
        }

        workspacePath = packageGraph.WorkspacePath;
        profile = packageGraph.Profile;
        List<string> packageUrls = packageGraph.PackageUrls.ToList();
        string resolvedManifestPath = packageGraph.ResolvedManifestPath;
        string projectName = projectSubsystem.ActiveProject?.Name ??
            Path.GetFileName(
                workspacePath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));

        if (runtimeAssetOptions.EnableSourceAssetDiagnostics)
        {
            KernelLog.Warning(
                "[Host] Diagnostic source-asset selection is enabled for this process.");
        }

        RuntimeVisualSummaryService? visualSummaryService = null;
        if (smokeOptions.CaptureVisualSummary)
        {
            visualSummaryService = smokeOptions.Mode == RuntimeSmokeMode.WorldStreaming
                ? new RuntimeVisualSummaryService(
                    workspacePath,
                    profile,
                    smokeOptions.VisualSummaryOutputPath)
                : new RuntimeVisualSummaryService(
                    workspacePath,
                    profile,
                    smokeOptions.EffectiveFrameCount - 1,
                    smokeOptions.VisualSummaryOutputPath);
            registry.RegisterService<IRuntimeVisualSummaryService>(visualSummaryService);
            if (smokeOptions.Mode == RuntimeSmokeMode.WorldStreaming)
            {
                KernelLog.InfoFormat(
                    "[Host] Named world-streaming visual summaries requested. Output base: {0}",
                    visualSummaryService.OutputPath);
            }
            else
            {
                KernelLog.InfoFormat(
                    "[Host] Visual summary requested for frame {0}. Output: {1}",
                    visualSummaryService.CaptureFrameIndex,
                    visualSummaryService.OutputPath);
            }
        }

        // 2. Initialize Kernel (The kernel now handles topological package loading)
        try
        {
            kernel.Initialize(new EngineConfig
            {
                ProjectRoot = workspacePath,
                ProjectName = projectName,
                PackageUrls = packageUrls,
                Platform = RuntimePlatform.Windows, // TODO: Deduce from OS
                EnableSourceAssetDiagnostics = runtimeAssetOptions.EnableSourceAssetDiagnostics
            });
        }
        catch (Exception ex)
        {
            KernelLog.FatalFormat(
                "[Host] FATAL ERROR: Engine initialization failed: {0}",
                ex.Message);
            try
            {
                if (kernel.IsPackageGraphMounted)
                {
                    kernel.Shutdown();
                }
            }
            catch (Exception shutdownException)
            {
                KernelLog.ErrorFormat(
                    "[Host] Package shutdown after initialization failure also failed: {0}",
                    shutdownException.Message);
            }

            Environment.Exit(1);
            return;
        }

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

            int smokeExitCode;
            IRuntimeSmokeScenario? smokeScenario = null;
            if (smokeOptions.Mode == RuntimeSmokeMode.WorldStreaming)
            {
                if (!registry.TryGetService<IRuntimeSmokeScenarioProvider>(out var scenarioProvider))
                {
                    KernelLog.Fatal(
                        "[Host] World-streaming smoke requires an IRuntimeSmokeScenarioProvider.");
                    kernel.Shutdown();
                    Environment.ExitCode = 1;
                    return;
                }

                var context = new RuntimeSmokeScenarioContext(
                    smokeOptions.ModeName,
                    workspacePath,
                    profile,
                    smokeOptions.SmokeSummaryOutputPath,
                    visualSummaryService);
                if (!scenarioProvider.TryCreateScenario(
                        context,
                        out smokeScenario,
                        out string scenarioDiagnostic))
                {
                    KernelLog.FatalFormat(
                        "[Host] World-streaming smoke scenario creation failed: {0}",
                        scenarioDiagnostic);
                    kernel.Shutdown();
                    Environment.ExitCode = 1;
                    return;
                }

                smokeExitCode = kernel.RunSmokeScenario(
                    smokeScenario,
                    smokeOptions.EffectiveFrameCount,
                    TimeSpan.FromSeconds(45));
                KernelLog.InfoFormat(
                    smokeScenario.Succeeded
                        ? "[Host] Smoke scenario passed: {0}"
                        : "[Host] Smoke scenario failed: {0}",
                    smokeScenario.Succeeded
                        ? smokeScenario.OutputPath
                        : smokeScenario.FailureMessage ?? "unknown scenario failure");
            }
            else
            {
                smokeExitCode = kernel.RunForFrames(smokeOptions.EffectiveFrameCount);
            }

            if (visualSummaryService != null)
            {
                visualSummaryService.Seal();
                if (!visualSummaryService.IsComplete)
                {
                    visualSummaryService.ReportFailure(
                        "No native render surface completed every requested visual-summary capture.");
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
                        "[Host] {0} visual summary capture(s) passed. Output base: {1}",
                        visualSummaryService.GetCaptureResults().Count,
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

    public static EnginePackageGraphResolution ResolvePackageGraph(
        string workspacePath,
        string profile,
        bool allowResolvedManifestFallback = false,
        string? resolvedManifestPathOverride = null)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new ArgumentException("A non-empty workspace path is required.", nameof(workspacePath));
        }

        if (string.IsNullOrWhiteSpace(profile))
        {
            throw new ArgumentException("A non-empty profile is required.", nameof(profile));
        }

        string fullWorkspacePath = Path.GetFullPath(workspacePath);
        string manifestPath = Path.Combine(fullWorkspacePath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                $"Cannot find manifest.json at {manifestPath}",
                manifestPath);
        }

        KernelLog.InfoFormat("[Host] Reading Project Manifest: {0}", manifestPath);
        using JsonDocument manifestJson = ManifestJson.ParseDocumentFile(manifestPath);
        if (!manifestJson.RootElement.TryGetPropertyIC("Packages", out JsonElement packagesElement) ||
            packagesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Workspace manifest '{manifestPath}' must contain a Packages array.");
        }

        var packageUrls = new List<string>();
        void AddPackages(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("A workspace package collection must be an array.");
            }

            foreach (JsonElement package in element.EnumerateArray())
            {
                if (!package.TryGetPropertyIC("Url", out JsonElement urlElement) ||
                    urlElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(urlElement.GetString()))
                {
                    throw new InvalidDataException(
                        "A workspace package entry must contain a non-empty Url string.");
                }

                string url = urlElement.GetString()!;
                packageUrls.Add(ResolvePackageUrl(url, fullWorkspacePath, fullWorkspacePath));
            }
        }

        AddPackages(packagesElement);
        if (manifestJson.RootElement.TryGetPropertyIC("Profiles", out JsonElement profilesElement))
        {
            if (profilesElement.TryGetPropertyIC(profile, out JsonElement profileDefinition))
            {
                KernelLog.InfoFormat("[Host] Loading Profile: {0}", profile);
                if (profileDefinition.ValueKind == JsonValueKind.Object &&
                    profileDefinition.TryGetPropertyIC("Packages", out JsonElement profilePackages))
                {
                    AddPackages(profilePackages);
                }
                else if (profileDefinition.ValueKind == JsonValueKind.Array)
                {
                    AddPackages(profileDefinition);
                }
            }
            else if (!string.Equals(profile, "Development", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(profile, "Production", StringComparison.OrdinalIgnoreCase))
            {
                KernelLog.WarningFormat(
                    "[Host] WARNING: Profile '{0}' not found in manifest.json.",
                    profile);
            }
        }

        string resolvedManifestPath;
        if (!string.IsNullOrWhiteSpace(resolvedManifestPathOverride))
        {
            resolvedManifestPath = Path.GetFullPath(resolvedManifestPathOverride);
            if (!File.Exists(resolvedManifestPath))
            {
                throw new FileNotFoundException(
                    $"Resolved manifest override was not found at '{resolvedManifestPath}'.",
                    resolvedManifestPath);
            }
        }
        else
        {
            resolvedManifestPath = Path.Combine(AppContext.BaseDirectory, "manifest.resolved.json");
            if (!File.Exists(resolvedManifestPath))
            {
                resolvedManifestPath = Path.Combine(
                    fullWorkspacePath,
                    $"manifest.resolved.{profile}.json");
            }
        }

        if (File.Exists(resolvedManifestPath))
        {
            if (!TryLoadResolvedPackageUrls(
                    resolvedManifestPath,
                    fullWorkspacePath,
                    profile,
                    packageUrls,
                    out string resolvedError))
            {
                if (!allowResolvedManifestFallback)
                {
                    throw new InvalidDataException(
                        $"Resolved manifest '{resolvedManifestPath}' is invalid: {resolvedError}");
                }

                KernelLog.WarningFormat(
                    "[Host] Failed to parse resolved manifest '{0}': {1}. Falling back to manifest.json order because --allow-manifest-fallback was specified.",
                    resolvedManifestPath,
                    resolvedError);
            }
        }
        else
        {
            KernelLog.WarningFormat(
                "[Host] No resolved manifest found for profile '{0}'. Runtime will use raw manifest package order.",
                profile);
        }

        if (packageUrls.Count == 0)
        {
            throw new InvalidDataException(
                $"Workspace '{fullWorkspacePath}' profile '{profile}' resolves no packages.");
        }

        return new EnginePackageGraphResolution(
            fullWorkspacePath,
            profile,
            manifestPath,
            resolvedManifestPath,
            packageUrls.ToArray());
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

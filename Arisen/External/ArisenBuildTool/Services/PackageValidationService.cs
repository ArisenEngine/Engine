using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public sealed class PackageValidationResult
{
    public Dictionary<string, PackageInfo> PackageMap { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PackageInfo> SortedPackages { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Success => Errors.Count == 0;
}

public static class PackageValidationService
{
    private static readonly HashSet<string> s_ValidPackageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "managed",
        "native",
        "hybrid"
    };

    private static readonly HashSet<string> s_ValidPackageLayers = new(StringComparer.OrdinalIgnoreCase)
    {
        "foundation",
        "domain",
        "driver",
        "tooling",
        "user",
        "test"
    };

    public static PackageValidationResult Validate(ProjectManifest manifest, string workspaceDir, string engineDir, string profile)
    {
        var result = new PackageValidationResult();
        var pendingPackages = new Queue<PackageRequirement>();
        var requestedPackages = new Dictionary<string, PackageRequirement>(StringComparer.OrdinalIgnoreCase);

        if (manifest.Packages == null || manifest.Packages.Count == 0)
        {
            result.Errors.Add("Workspace manifest does not list any base packages.");
        }
        else
        {
            foreach (var package in manifest.Packages)
            {
                EnqueueRequestedPackage(package, "base manifest", pendingPackages, requestedPackages, result);
            }
        }

        if (!string.IsNullOrWhiteSpace(profile))
        {
            if (manifest.Profiles != null && manifest.Profiles.TryGetValue(profile, out var profileDefinition))
            {
                foreach (var package in profileDefinition.Packages ?? new List<PackageRequirement>())
                {
                    EnqueueRequestedPackage(package, $"profile '{profile}'", pendingPackages, requestedPackages, result);
                }
            }
            else
            {
                result.Errors.Add($"Profile '{profile}' was not found in the workspace manifest.");
            }
        }

        while (pendingPackages.Count > 0)
        {
            var requirement = pendingPackages.Dequeue();
            if (string.IsNullOrWhiteSpace(requirement.Id))
            {
                result.Errors.Add("Encountered a package requirement with an empty Id.");
                continue;
            }

            if (result.PackageMap.ContainsKey(requirement.Id))
            {
                continue;
            }

            string packagePath = ResolvePackagePath(requirement, workspaceDir, engineDir);
            if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
            {
                result.Errors.Add($"Package '{requirement.Id}' could not be resolved. Searched workspace Local, workspace .Cache, engine Packages, and explicit URL '{requirement.Url ?? "<none>"}'.");
                continue;
            }

            string packageJsonPath = Path.Combine(packagePath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                result.Errors.Add($"Package '{requirement.Id}' at '{packagePath}' is missing package.json.");
                continue;
            }

            PackageManifest? packageManifest = ReadPackageManifest(packagePath, requirement.Id, result);
            if (packageManifest == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(packageManifest.Id) && !string.Equals(packageManifest.Id, requirement.Id, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Package ID mismatch at '{packageJsonPath}'. Requirement requested '{requirement.Id}', but package.json declares '{packageManifest.Id}'.");
                continue;
            }

            packageManifest.Id = requirement.Id;
            packageManifest.Type = NormalizePackageType(packageManifest, packagePath);
            if (!s_ValidPackageTypes.Contains(packageManifest.Type ?? string.Empty))
            {
                result.Errors.Add($"Package '{requirement.Id}' has invalid type '{packageManifest.Type}'. Valid types are: managed, native, hybrid.");
                continue;
            }

            ValidatePackageLayer(packageManifest, result);

            if (packageManifest.Entry != null)
            {
                ValidateEntryBlock(packageManifest, result);
            }

            ValidateSubsystemMetadata(packageManifest, result);
            ValidateNativeRuntimeMetadata(packageManifest, packagePath, result);
            ValidateNativeTestMetadata(packageManifest, packagePath, result);

            result.PackageMap[requirement.Id] = new PackageInfo
            {
                Manifest = packageManifest,
                DirectoryPath = packagePath
            };

            if (packageManifest.Dependencies != null)
            {
                foreach (var dependency in packageManifest.Dependencies.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(dependency.Key))
                    {
                        result.Errors.Add($"Package '{requirement.Id}' declares an empty dependency id.");
                        continue;
                    }

                    if (!result.PackageMap.ContainsKey(dependency.Key))
                    {
                        pendingPackages.Enqueue(new PackageRequirement { Id = dependency.Key, Version = dependency.Value });
                    }
                }
            }
        }

        if (result.Errors.Count == 0)
        {
            ValidatePackageLayerDependencies(result);
        }

        if (result.Errors.Count == 0)
        {
            ValidateServiceContracts(result);
        }

        if (result.Errors.Count == 0)
        {
            SortTopologically(result);
        }

        return result;
    }

    public static void LogSummary(PackageValidationResult result)
    {
        foreach (var warning in result.Warnings)
        {
            Logger.Warning(warning);
        }

        foreach (var error in result.Errors)
        {
            Logger.Error(error);
        }

        if (result.Success)
        {
            Logger.Info($"Package validation succeeded. {result.PackageMap.Count} packages resolved.");
            Logger.Info("Resolved package order:");
            for (int i = 0; i < result.SortedPackages.Count; i++)
            {
                var package = result.SortedPackages[i];
                Logger.Info($"  {i + 1}. {package.Manifest.Id} ({package.Manifest.Type}) -> {package.DirectoryPath}");
            }
        }
        else
        {
            Logger.Error($"Package validation failed with {result.Errors.Count} error(s) and {result.Warnings.Count} warning(s).");
        }
    }

    private static void EnqueueRequestedPackage(
        PackageRequirement package,
        string source,
        Queue<PackageRequirement> pendingPackages,
        Dictionary<string, PackageRequirement> requestedPackages,
        PackageValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(package.Id))
        {
            result.Errors.Add($"The {source} contains a package requirement with an empty Id.");
            return;
        }

        if (requestedPackages.TryGetValue(package.Id, out var existing))
        {
            if (!SameRequirement(existing, package))
            {
                result.Errors.Add($"Package '{package.Id}' is listed multiple times with conflicting URL/version metadata.");
            }
            else
            {
                result.Warnings.Add($"Package '{package.Id}' is listed more than once. The duplicate entry from {source} will be ignored.");
            }
            return;
        }

        requestedPackages[package.Id] = package;
        pendingPackages.Enqueue(package);
    }

    private static bool SameRequirement(PackageRequirement left, PackageRequirement right)
    {
        return string.Equals(left.Url ?? string.Empty, right.Url ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Version ?? string.Empty, right.Version ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static PackageManifest? ReadPackageManifest(string packagePath, string packageId, PackageValidationResult result)
    {
        try
        {
            var manifest = PackageManifestService.ReadEffectiveManifest(packagePath);
            if (manifest == null)
            {
                result.Errors.Add($"Package '{packageId}' package.json deserialized to null.");
                return null;
            }

            return manifest;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse package '{packageId}' manifest at '{packagePath}': {ex.Message}");
            return null;
        }
    }

    private static string NormalizePackageType(PackageManifest manifest, string packagePath)
    {
        if (string.Equals(manifest.Type, "hybrid", StringComparison.OrdinalIgnoreCase))
        {
            return "hybrid";
        }

        if (string.Equals(manifest.Type, "managed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(manifest.Type, "native", StringComparison.OrdinalIgnoreCase))
        {
            return manifest.Type!.ToLowerInvariant();
        }

        if (manifest.NativeRuntimes != null && manifest.NativeRuntimes.Count > 0 && manifest.Entry == null)
        {
            return "native";
        }

        if (Directory.Exists(Path.Combine(packagePath, "Managed")) && File.Exists(Path.Combine(packagePath, "CMakeLists.txt")))
        {
            return "hybrid";
        }

        return "managed";
    }

    private static void ValidatePackageLayer(PackageManifest manifest, PackageValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(manifest.Layer))
        {
            result.Errors.Add($"Package '{manifest.Id}' is missing required layer metadata. Valid layers are: foundation, domain, driver, tooling, user, test.");
            return;
        }

        manifest.Layer = manifest.Layer.ToLowerInvariant();
        if (!s_ValidPackageLayers.Contains(manifest.Layer))
        {
            result.Errors.Add($"Package '{manifest.Id}' has invalid layer '{manifest.Layer}'. Valid layers are: foundation, domain, driver, tooling, user, test.");
        }
    }

    private static void ValidatePackageLayerDependencies(PackageValidationResult result)
    {
        foreach (var package in result.PackageMap.Values)
        {
            if (package.Manifest.Dependencies == null) continue;

            foreach (var dependencyId in package.Manifest.Dependencies.Keys)
            {
                if (!result.PackageMap.TryGetValue(dependencyId, out var dependency)) continue;

                if (!CanDependOnLayer(package.Manifest.Layer, dependency.Manifest.Layer))
                {
                    result.Errors.Add($"Package '{package.Manifest.Id}' in layer '{package.Manifest.Layer}' cannot depend on package '{dependency.Manifest.Id}' in layer '{dependency.Manifest.Layer}'.");
                }
            }
        }
    }

    private static bool CanDependOnLayer(string? packageLayer, string? dependencyLayer)
    {
        if (string.IsNullOrWhiteSpace(packageLayer) || string.IsNullOrWhiteSpace(dependencyLayer)) return true;

        return packageLayer.ToLowerInvariant() switch
        {
            "foundation" => string.Equals(dependencyLayer, "foundation", StringComparison.OrdinalIgnoreCase),
            "domain" => IsOneOf(dependencyLayer, "foundation", "domain"),
            "driver" => string.Equals(dependencyLayer, "foundation", StringComparison.OrdinalIgnoreCase),
            "tooling" => IsOneOf(dependencyLayer, "foundation", "domain", "tooling"),
            "user" => IsOneOf(dependencyLayer, "foundation", "domain", "driver", "tooling", "user"),
            "test" => true,
            _ => true
        };
    }

    private static bool IsOneOf(string value, params string[] candidates)
    {
        return candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateEntryBlock(PackageManifest manifest, PackageValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(manifest.Entry?.Assembly) && !string.Equals(manifest.Type, "native", StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"Package '{manifest.Id}' has an entry block but no entry.assembly.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.Entry?.Class) && string.IsNullOrWhiteSpace(manifest.Entry?.Assembly))
        {
            result.Errors.Add($"Package '{manifest.Id}' declares entry.class but no entry.assembly.");
        }
    }

    private static void ValidateSubsystemMetadata(PackageManifest manifest, PackageValidationResult result)
    {
        if (manifest.Subsystems == null) return;

        var validPhases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PreInit",
            "Init",
            "PostInit",
            "Running",
            "PreShutdown",
            "Shutdown"
        };

        foreach (var subsystem in manifest.Subsystems)
        {
            if (string.IsNullOrWhiteSpace(subsystem.Class))
            {
                result.Errors.Add($"Package '{manifest.Id}' declares a subsystem with an empty class.");
            }

            if (!validPhases.Contains(subsystem.Phase))
            {
                result.Errors.Add($"Package '{manifest.Id}' subsystem '{subsystem.Class}' declares invalid phase '{subsystem.Phase}'.");
            }
        }
    }

    private static void ValidateNativeRuntimeMetadata(PackageManifest manifest, string packagePath, PackageValidationResult result)
    {
        if (manifest.NativeRuntimes == null || manifest.NativeRuntimes.Count == 0)
        {
            return;
        }

        var package = new PackageInfo
        {
            Manifest = manifest,
            DirectoryPath = packagePath
        };

        if (!NativeRuntimeManifestService.HasRuntime(manifest, NativeRuntimeManifestService.DefaultRuntimeIdentifier)
            && (string.Equals(manifest.Type, "native", StringComparison.OrdinalIgnoreCase)
                || string.Equals(manifest.Type, "hybrid", StringComparison.OrdinalIgnoreCase)))
        {
            result.Errors.Add($"Package '{manifest.Id}' declares native runtimes but has no '{NativeRuntimeManifestService.DefaultRuntimeIdentifier}' payloads for the current target runtime.");
        }

        _ = NativeRuntimeManifestService.EnumerateForRuntime(
            package,
            NativeRuntimeManifestService.DefaultRuntimeIdentifier,
            result.Errors,
            result.Warnings,
            validateFiles: true).ToList();
    }

    private static void ValidateNativeTestMetadata(PackageManifest manifest, string packagePath, PackageValidationResult result)
    {
        if (manifest.NativeTests == null || manifest.NativeTests.Count == 0)
        {
            return;
        }

        if (!string.Equals(manifest.Layer, "test", StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"Package '{manifest.Id}' declares nativeTests but is in layer '{manifest.Layer}'. Native tests are only valid in test packages.");
        }

        if (!NativeTestManifestService.HasRuntime(manifest, NativeRuntimeManifestService.DefaultRuntimeIdentifier))
        {
            result.Errors.Add($"Package '{manifest.Id}' declares nativeTests but has no '{NativeRuntimeManifestService.DefaultRuntimeIdentifier}' test entries for the current target runtime.");
        }

        var package = new PackageInfo
        {
            Manifest = manifest,
            DirectoryPath = packagePath
        };

        var runtimeLibraries = NativeRuntimeManifestService
            .EnumerateForRuntime(package, NativeRuntimeManifestService.DefaultRuntimeIdentifier, result.Errors, result.Warnings)
            .Select(runtime => Path.GetFileName(runtime.Path))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var nativeTest in NativeTestManifestService.EnumerateForRuntime(package, NativeRuntimeManifestService.DefaultRuntimeIdentifier, result.Errors))
        {
            if (!runtimeLibraries.Contains(nativeTest.Library))
            {
                result.Errors.Add($"Package '{manifest.Id}' native test library '{nativeTest.Library}' must also be declared in nativeRuntimes['{NativeRuntimeManifestService.DefaultRuntimeIdentifier}'].");
            }
        }
    }

    private static void ValidateServiceContracts(PackageValidationResult result)
    {
        var providersByContract = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var package in result.PackageMap.Values)
        {
            foreach (var providedContract in EnumerateServiceContracts(package.Manifest.Services?.Provides, package.Manifest.Id, "provides", result))
            {
                if (!providersByContract.TryGetValue(providedContract.Name, out var providers))
                {
                    providers = new List<string>();
                    providersByContract[providedContract.Name] = providers;
                }

                providers.Add(package.Manifest.Id);
            }
        }

        foreach (var provider in providersByContract.Where(x => x.Value.Count > 1))
        {
            result.Errors.Add($"Service contract '{provider.Key}' is provided by multiple selected packages: {string.Join(", ", provider.Value.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}. Select exactly one provider package for each service contract in the active workspace/profile.");
        }

        foreach (var package in result.PackageMap.Values)
        {
            foreach (var requiredContract in EnumerateServiceContracts(package.Manifest.Services?.Requires, package.Manifest.Id, "requires", result))
            {
                if (!providersByContract.ContainsKey(requiredContract.Name))
                {
                    if (requiredContract.Optional)
                    {
                        result.Warnings.Add($"Package '{package.Manifest.Id}' optionally requires service '{requiredContract.Name}', but no selected package provides it.");
                    }
                    else
                    {
                        result.Errors.Add($"Package '{package.Manifest.Id}' requires service '{requiredContract.Name}', but no selected package provides it.");
                    }
                }
            }
        }
    }

    private sealed record ServiceContractDescriptor(string Name, bool Optional, bool Deferred, int? Priority, IReadOnlyList<string> Capabilities);

    private static IEnumerable<ServiceContractDescriptor> EnumerateServiceContracts(List<JsonElement>? elements, string packageId, string sectionName, PackageValidationResult result)
    {
        if (elements == null) yield break;

        foreach (var element in elements)
        {
            string? contract = null;
            bool optional = false;
            bool deferred = false;
            int? priority = null;
            var capabilities = Array.Empty<string>();
            if (element.ValueKind == JsonValueKind.String)
            {
                contract = element.GetString();
            }
            else if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("interface", out var interfaceElement) && interfaceElement.ValueKind == JsonValueKind.String)
            {
                contract = interfaceElement.GetString();
                optional = IsTrue(element, "optional");
                deferred = IsTrue(element, "deferred");

                if (string.Equals(sectionName, "provides", StringComparison.OrdinalIgnoreCase) && optional)
                {
                    result.Errors.Add($"Package '{packageId}' has invalid services.{sectionName} optional flag for contract '{contract}'. The optional flag is only valid in services.requires.");
                }

                if (string.Equals(sectionName, "requires", StringComparison.OrdinalIgnoreCase) && element.TryGetProperty("priority", out _))
                {
                    result.Errors.Add($"Package '{packageId}' has invalid services.{sectionName} priority for contract '{contract}'. Priority is only valid in services.provides.");
                }

                if (element.TryGetProperty("priority", out var priorityElement))
                {
                    if (priorityElement.ValueKind == JsonValueKind.Number && priorityElement.TryGetInt32(out int parsedPriority))
                    {
                        priority = parsedPriority;
                    }
                    else
                    {
                        result.Errors.Add($"Package '{packageId}' has invalid services.{sectionName} priority for contract '{contract}'. Expected an integer.");
                    }
                }

                if (element.TryGetProperty("capabilities", out var capabilitiesElement))
                {
                    if (capabilitiesElement.ValueKind == JsonValueKind.Array)
                    {
                        var parsedCapabilities = new List<string>();
                        foreach (var capabilityElement in capabilitiesElement.EnumerateArray())
                        {
                            if (capabilityElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(capabilityElement.GetString()))
                            {
                                result.Errors.Add($"Package '{packageId}' has invalid services.{sectionName} capability for contract '{contract}'. Expected non-empty strings.");
                                continue;
                            }

                            parsedCapabilities.Add(capabilityElement.GetString()!);
                        }

                        capabilities = parsedCapabilities.ToArray();
                    }
                    else
                    {
                        result.Errors.Add($"Package '{packageId}' has invalid services.{sectionName} capabilities for contract '{contract}'. Expected an array of strings.");
                    }
                }
            }
            else
            {
                result.Errors.Add($"Package '{packageId}' has invalid services.{sectionName} entry. Expected a string or an object with an 'interface' string property.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(contract))
            {
                result.Errors.Add($"Package '{packageId}' has an empty services.{sectionName} contract.");
                continue;
            }

            if (!contract.Contains('.', StringComparison.Ordinal))
            {
                result.Errors.Add($"Package '{packageId}' has unqualified services.{sectionName} contract '{contract}'. Service contracts must use fully qualified type names such as 'ArisenKernel.Contracts.IApplicationHost'.");
                continue;
            }

            yield return new ServiceContractDescriptor(contract, optional, deferred, priority, capabilities);
        }
    }

    private static bool IsTrue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.True;
    }

    private static void SortTopologically(PackageValidationResult result)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!result.PackageMap.TryGetValue(id, out var package))
            {
                result.Errors.Add($"Package '{id}' is referenced but was not discovered.");
                return;
            }

            if (visiting.Contains(id))
            {
                var cycle = stack.Reverse().SkipWhile(x => !string.Equals(x, id, StringComparison.OrdinalIgnoreCase)).Concat(new[] { id });
                result.Errors.Add($"Package dependency cycle detected: {string.Join(" -> ", cycle)}");
                return;
            }

            visiting.Add(id);
            stack.Push(id);

            if (package.Manifest.Dependencies != null)
            {
                foreach (var dependencyId in package.Manifest.Dependencies.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    if (!result.PackageMap.ContainsKey(dependencyId))
                    {
                        result.Errors.Add($"Package '{id}' depends on missing package '{dependencyId}'.");
                    }
                    else
                    {
                        Visit(dependencyId);
                    }
                }
            }

            stack.Pop();
            visiting.Remove(id);
            visited.Add(id);
            result.SortedPackages.Add(package);
        }

        foreach (var id in result.PackageMap.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            Visit(id);
        }

        if (result.Errors.Count > 0)
        {
            result.SortedPackages.Clear();
        }
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

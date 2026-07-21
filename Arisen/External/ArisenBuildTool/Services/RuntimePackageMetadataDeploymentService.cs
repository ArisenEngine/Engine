using System.Text.Json;
using System.Text.Json.Serialization;
using ArisenBuildTool.Models;

namespace ArisenBuildTool.Services;

public sealed record RuntimePackageMetadataDeploymentResult(
    string OutputRoot,
    string ProjectManifestPath,
    string ResolvedManifestPath,
    string LaunchConfigPath,
    string PackagesRoot,
    int PackageCount);

/// <summary>
/// Publishes the source-independent package and project metadata consumed by a deployed player.
/// Package assemblies and native payloads remain in the output root; package directories contain
/// only effective runtime descriptors.
/// </summary>
public static class RuntimePackageMetadataDeploymentService
{
    public const int SchemaVersion = 1;
    public const string PackagesDirectoryName = "Packages";
    public const string ProjectManifestFileName = "manifest.json";
    public const string ResolvedManifestFileName = "manifest.resolved.json";
    public const string LaunchConfigFileName = "launch.config.json";
    public const string DeployedLaunchMode = "Deployed";

    private static readonly JsonSerializerOptions s_JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static RuntimePackageMetadataDeploymentResult Deploy(
        ProjectManifest project,
        string profile,
        IReadOnlyList<PackageInfo> sortedPackages,
        string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sortedPackages);
        if (string.IsNullOrWhiteSpace(profile))
        {
            throw new ArgumentException("Runtime metadata deployment requires a profile.", nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            throw new ArgumentException("Runtime metadata deployment requires an output root.", nameof(outputRoot));
        }

        if (sortedPackages.Count == 0)
        {
            throw new InvalidOperationException("Runtime metadata deployment requires at least one package.");
        }

        ValidatePackages(sortedPackages);
        string fullOutputRoot = Path.GetFullPath(outputRoot);
        Directory.CreateDirectory(fullOutputRoot);
        string transactionId = Guid.NewGuid().ToString("N");
        string stagingRoot = Path.Combine(fullOutputRoot, $".runtime-metadata-stage-{transactionId}");
        string backupRoot = Path.Combine(fullOutputRoot, $".runtime-metadata-backup-{transactionId}");

        try
        {
            WriteStagedMetadata(project, profile.Trim(), sortedPackages, stagingRoot);
            ValidateStagedMetadata(profile.Trim(), sortedPackages, stagingRoot);
            Commit(fullOutputRoot, stagingRoot, backupRoot);

            return new RuntimePackageMetadataDeploymentResult(
                fullOutputRoot,
                Path.Combine(fullOutputRoot, ProjectManifestFileName),
                Path.Combine(fullOutputRoot, ResolvedManifestFileName),
                Path.Combine(fullOutputRoot, LaunchConfigFileName),
                Path.Combine(fullOutputRoot, PackagesDirectoryName),
                sortedPackages.Count);
        }
        finally
        {
            DeleteOwnedDirectory(fullOutputRoot, stagingRoot);
            DeleteOwnedDirectory(fullOutputRoot, backupRoot);
        }
    }

    private static void ValidatePackages(IReadOnlyList<PackageInfo> packages)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PackageInfo package in packages)
        {
            string id = package.Manifest.Id;
            if (string.IsNullOrWhiteSpace(id) ||
                !string.Equals(id, Path.GetFileName(id), StringComparison.Ordinal) ||
                id.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            {
                throw new InvalidOperationException(
                    $"Runtime package id '{id}' is not a safe deployment directory name.");
            }

            if (!seen.Add(id))
            {
                throw new InvalidOperationException(
                    $"Runtime package metadata contains duplicate package id '{id}'.");
            }
        }
    }

    private static void WriteStagedMetadata(
        ProjectManifest project,
        string profile,
        IReadOnlyList<PackageInfo> packages,
        string stagingRoot)
    {
        Directory.CreateDirectory(stagingRoot);
        string packagesRoot = Path.Combine(stagingRoot, PackagesDirectoryName);
        Directory.CreateDirectory(packagesRoot);
        foreach (PackageInfo package in packages)
        {
            string packageRoot = Path.Combine(packagesRoot, package.Manifest.Id);
            Directory.CreateDirectory(packageRoot);
            WriteJson(Path.Combine(packageRoot, "package.json"), package.Manifest);
        }

        var runtimePackages = packages
            .Select(package => new PackageRequirement
            {
                Id = package.Manifest.Id,
                Version = package.Manifest.Version,
                Url = GetDeployedPackageUrl(package.Manifest.Id)
            })
            .ToArray();
        var runtimeProject = new
        {
            project.Name,
            project.EngineVersion,
            project.StartupScene,
            project.StartupWorld,
            project.RenderPipeline,
            Packages = runtimePackages
        };
        WriteJson(Path.Combine(stagingRoot, ProjectManifestFileName), runtimeProject);

        var resolvedManifest = new
        {
            SchemaVersion,
            Profile = profile,
            ResolvedPackages = packages.Select(package => new
            {
                package.Manifest.Id,
                package.Manifest.Name,
                package.Manifest.Version,
                package.Manifest.EngineVersion,
                package.Manifest.Type,
                Dependencies = package.Manifest.Dependencies ?? new Dictionary<string, string>(),
                package.Manifest.Services,
                package.Manifest.Subsystems,
                package.Manifest.NativeRuntimes,
                package.Manifest.NativeTests,
                package.Manifest.Entry,
                Url = GetDeployedPackageUrl(package.Manifest.Id)
            }).ToArray()
        };
        WriteJson(Path.Combine(stagingRoot, ResolvedManifestFileName), resolvedManifest);

        var launchConfig = new
        {
            SchemaVersion,
            Mode = DeployedLaunchMode,
            Profile = profile
        };
        WriteJson(Path.Combine(stagingRoot, LaunchConfigFileName), launchConfig);
    }

    private static void ValidateStagedMetadata(
        string expectedProfile,
        IReadOnlyList<PackageInfo> packages,
        string stagingRoot)
    {
        using JsonDocument launchConfig = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(stagingRoot, LaunchConfigFileName)));
        JsonElement launchRoot = launchConfig.RootElement;
        if (!launchRoot.TryGetProperty("Mode", out JsonElement modeElement) ||
            !string.Equals(modeElement.GetString(), DeployedLaunchMode, StringComparison.Ordinal) ||
            !launchRoot.TryGetProperty("Profile", out JsonElement profileElement) ||
            !string.Equals(profileElement.GetString(), expectedProfile, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Staged launch configuration does not describe the requested deployed profile.");
        }

        using JsonDocument resolved = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(stagingRoot, ResolvedManifestFileName)));
        JsonElement resolvedRoot = resolved.RootElement;
        if (!resolvedRoot.TryGetProperty("ResolvedPackages", out JsonElement resolvedPackages) ||
            resolvedPackages.ValueKind != JsonValueKind.Array ||
            resolvedPackages.GetArrayLength() != packages.Count)
        {
            throw new InvalidDataException("Staged resolved manifest has an invalid package collection.");
        }

        int index = 0;
        foreach (JsonElement resolvedPackage in resolvedPackages.EnumerateArray())
        {
            PackageInfo expected = packages[index++];
            string expectedUrl = GetDeployedPackageUrl(expected.Manifest.Id);
            if (!resolvedPackage.TryGetProperty("Id", out JsonElement idElement) ||
                !string.Equals(idElement.GetString(), expected.Manifest.Id, StringComparison.Ordinal) ||
                !resolvedPackage.TryGetProperty("Url", out JsonElement urlElement) ||
                !string.Equals(urlElement.GetString(), expectedUrl, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Staged resolved package {index - 1} does not match '{expected.Manifest.Id}'.");
            }

            string descriptorPath = Path.Combine(
                stagingRoot,
                PackagesDirectoryName,
                expected.Manifest.Id,
                "package.json");
            PackageManifest descriptor = PackageManifestService.ReadManifestFile(descriptorPath)
                ?? throw new InvalidDataException(
                    $"Staged package descriptor '{descriptorPath}' is invalid.");
            if (!string.Equals(descriptor.Id, expected.Manifest.Id, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Staged package descriptor '{descriptorPath}' has an unexpected id '{descriptor.Id}'.");
            }
        }

        using JsonDocument project = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(stagingRoot, ProjectManifestFileName)));
        if (!project.RootElement.TryGetProperty("Packages", out JsonElement projectPackages) ||
            projectPackages.ValueKind != JsonValueKind.Array ||
            projectPackages.GetArrayLength() != packages.Count)
        {
            throw new InvalidDataException("Staged runtime project manifest has an invalid package collection.");
        }
    }

    private static void Commit(string outputRoot, string stagingRoot, string backupRoot)
    {
        var entries = new[]
        {
            new DeploymentEntry(PackagesDirectoryName, IsDirectory: true),
            new DeploymentEntry(ProjectManifestFileName, IsDirectory: false),
            new DeploymentEntry(ResolvedManifestFileName, IsDirectory: false),
            new DeploymentEntry(LaunchConfigFileName, IsDirectory: false)
        };
        Directory.CreateDirectory(backupRoot);
        var backedUp = new List<DeploymentEntry>();
        var installed = new List<DeploymentEntry>();
        try
        {
            foreach (DeploymentEntry entry in entries)
            {
                string target = Path.Combine(outputRoot, entry.Name);
                if (!Exists(target, entry.IsDirectory))
                {
                    continue;
                }

                RejectReparsePoint(target);
                Move(target, Path.Combine(backupRoot, entry.Name), entry.IsDirectory);
                backedUp.Add(entry);
            }

            foreach (DeploymentEntry entry in entries)
            {
                Move(
                    Path.Combine(stagingRoot, entry.Name),
                    Path.Combine(outputRoot, entry.Name),
                    entry.IsDirectory);
                installed.Add(entry);
            }
        }
        catch
        {
            for (int index = installed.Count - 1; index >= 0; index--)
            {
                DeleteOwnedEntry(outputRoot, Path.Combine(outputRoot, installed[index].Name), installed[index].IsDirectory);
            }

            for (int index = backedUp.Count - 1; index >= 0; index--)
            {
                DeploymentEntry entry = backedUp[index];
                Move(
                    Path.Combine(backupRoot, entry.Name),
                    Path.Combine(outputRoot, entry.Name),
                    entry.IsDirectory);
            }

            throw;
        }
    }

    private static string GetDeployedPackageUrl(string packageId)
    {
        return $"file://{PackagesDirectoryName}/{packageId}/";
    }

    private static void WriteJson<T>(string path, T value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, s_JsonOptions) + "\n");
    }

    private static bool Exists(string path, bool isDirectory)
    {
        return isDirectory ? Directory.Exists(path) : File.Exists(path);
    }

    private static void Move(string source, string destination, bool isDirectory)
    {
        if (isDirectory)
        {
            Directory.Move(source, destination);
        }
        else
        {
            File.Move(source, destination);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                $"Runtime metadata target '{path}' is a symbolic link or reparse point.");
        }

        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (string child in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
        {
            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    $"Runtime metadata target contains symbolic link or reparse point '{child}'.");
            }
        }
    }

    private static void DeleteOwnedEntry(string outputRoot, string path, bool isDirectory)
    {
        EnsureOwnedPath(outputRoot, path);
        if (isDirectory)
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteOwnedDirectory(string outputRoot, string path)
    {
        EnsureOwnedPath(outputRoot, path);
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private static void EnsureOwnedPath(string outputRoot, string path)
    {
        string fullRoot = Path.GetFullPath(outputRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Runtime metadata path '{fullPath}' escapes output root '{outputRoot}'.");
        }
    }

    private readonly record struct DeploymentEntry(string Name, bool IsDirectory);
}

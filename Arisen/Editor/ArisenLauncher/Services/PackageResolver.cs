using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ArisenLauncher.Models;

namespace ArisenLauncher.Services;

public class PackageResolver
{
    private readonly ILogService? _logService;
    private static readonly HttpClient s_HttpClient = new();
    private const string LockFileName = "package-lock.json";

    public PackageResolver(ILogService? logService)
    {
        _logService = logService;
    }

    public async Task RestoreManifestPackagesAsync(ProjectManifest manifest, string profile, string workspaceDir)
    {
        string cacheDir = Path.Combine(workspaceDir, ".Cache");
        string lockFilePath = Path.Combine(workspaceDir, ".arisen", LockFileName);
        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);

        var lockDocument = PackageLockDocument.Load(lockFilePath);
        bool lockChanged = false;

        foreach (var requirement in EnumerateRequirements(manifest, profile))
        {
            if (string.IsNullOrWhiteSpace(requirement.Id) || string.IsNullOrWhiteSpace(requirement.Url))
                continue;

            if (!requirement.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !requirement.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                continue;

            var restore = await ResolveRequirementAsync(requirement, cacheDir);
            string contentHash = ComputeDirectoryHash(restore.PackagePath);
            string relativeCachePath = Path.GetRelativePath(workspaceDir, restore.PackagePath).Replace('\\', '/');
            lockChanged |= lockDocument.UpdateOrValidate(requirement, relativeCachePath, contentHash, restore.ArchiveUrl, restore.ArchiveHash, restore.ResolvedVersion);
        }

        if (lockChanged)
        {
            lockDocument.Save(lockFilePath);
        }
    }

    public async Task<string> ResolveAsync(string id, string url, string destinationDir)
    {
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveRemoteArchiveAsync(id, url, destinationDir, expectedArchiveHash: null);
        }

        throw new NotSupportedException($"URL scheme not supported by PackageResolver: {url}");
    }

    private async Task<PackageRestoreResult> ResolveRequirementAsync(PackageRequirement requirement, string destinationDir)
    {
        string url = requirement.Url ?? string.Empty;
        if (IsRegistryIndexUrl(url))
        {
            return await ResolveRegistryPackageAsync(requirement, destinationDir);
        }

        string packagePath = await ResolveRemoteArchiveAsync(requirement.Id, url, destinationDir, expectedArchiveHash: null);
        return new PackageRestoreResult(packagePath, ArchiveUrl: null, ArchiveHash: null);
    }

    private async Task<PackageRestoreResult> ResolveRegistryPackageAsync(PackageRequirement requirement, string destinationDir)
    {
        if (string.IsNullOrWhiteSpace(requirement.Version))
        {
            throw new InvalidDataException($"Registry package '{requirement.Id}' requires a Version or semantic version range in the workspace manifest.");
        }

        string registryUrl = requirement.Url ?? string.Empty;
        Info($"[PackageResolver] Downloading package registry index from {registryUrl}...");
        string json = await s_HttpClient.GetStringAsync(registryUrl);
        var index = JsonSerializer.Deserialize<PackageRegistryIndex>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Registry index '{registryUrl}' could not be parsed.");

        if (index.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Registry index '{registryUrl}' has unsupported schemaVersion {index.SchemaVersion}.");
        }

        var registryPackages = index.Packages
            .Where(package => !string.IsNullOrWhiteSpace(package.Id) && !string.IsNullOrWhiteSpace(package.Version))
            .Select(package => new PackageRegistryPackageVersion
            {
                Id = package.Id,
                Version = package.Version,
                ArchiveUrl = package.Archive?.Url ?? string.Empty,
                Sha256 = package.Archive?.Sha256 ?? string.Empty,
                SizeBytes = package.Archive?.SizeBytes ?? 0
            })
            .ToList();

        var selectedPackage = PackageRegistryClient.SelectPackageVersion(registryPackages, requirement.Id, requirement.Version, out string rangeError);
        if (!string.IsNullOrWhiteSpace(rangeError))
        {
            throw new InvalidDataException($"Registry package '{requirement.Id}' has invalid version range '{requirement.Version}': {rangeError}");
        }

        if (selectedPackage == null)
        {
            string availableVersions = string.Join(", ", index.Packages
                .Where(package => string.Equals(package.Id, requirement.Id, StringComparison.OrdinalIgnoreCase))
                .Select(package => package.Version)
                .OrderBy(version => version, StringComparer.OrdinalIgnoreCase));
            string suffix = string.IsNullOrWhiteSpace(availableVersions) ? " No versions are available for this package." : $" Available versions: {availableVersions}.";
            throw new InvalidDataException($"Registry index '{registryUrl}' does not contain package '{requirement.Id}' matching version range '{requirement.Version}'.{suffix}");
        }

        if (string.IsNullOrWhiteSpace(selectedPackage.ArchiveUrl))
        {
            throw new InvalidDataException($"Registry package '{requirement.Id}' version '{selectedPackage.Version}' does not declare archive.url.");
        }

        if (string.IsNullOrWhiteSpace(selectedPackage.Sha256))
        {
            throw new InvalidDataException($"Registry package '{requirement.Id}' version '{selectedPackage.Version}' does not declare archive.sha256.");
        }

        string archiveUrl = ResolveRegistryArchiveUrl(registryUrl, selectedPackage.ArchiveUrl);
        string archiveHash = NormalizeSha256Hash(selectedPackage.Sha256);
        string packagePath = await ResolveRemoteArchiveAsync(requirement.Id, archiveUrl, destinationDir, archiveHash);
        return new PackageRestoreResult(packagePath, archiveUrl, archiveHash, selectedPackage.Version);
    }

    private async Task<string> ResolveRemoteArchiveAsync(string id, string url, string destinationDir, string? expectedArchiveHash)
    {
        Directory.CreateDirectory(destinationDir);

        string fileName = Path.GetFileName(new Uri(url).LocalPath);
        if (string.IsNullOrEmpty(fileName)) fileName = $"{id}.zip";

        string extractDir = Path.Combine(destinationDir, id);

        if (Directory.Exists(extractDir))
        {
            Info($"[PackageResolver] Package {id} already exists in {extractDir}. Skipping download.");
            return extractDir;
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string tempFile = Path.Combine(tempRoot, fileName);
        string tempExtractDir = Path.Combine(tempRoot, "extract");

        Info($"[PackageResolver] Downloading package from {url}...");

        try
        {
            using (var response = await s_HttpClient.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();
                using (var fs = new FileStream(tempFile, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }

            if (!string.IsNullOrWhiteSpace(expectedArchiveHash))
            {
                string actualArchiveHash = ComputeFileHash(tempFile);
                if (!string.Equals(actualArchiveHash, expectedArchiveHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Downloaded archive for package '{id}' failed integrity validation. Expected {expectedArchiveHash}, actual {actualArchiveHash}.");
                }
            }

            Info("[PackageResolver] Extracting to temporary directory...");
            ZipFile.ExtractToDirectory(tempFile, tempExtractDir);

            string packageRoot = FindExtractedPackageRoot(tempExtractDir);
            if (!File.Exists(Path.Combine(packageRoot, "package.json")))
            {
                throw new InvalidDataException($"Downloaded package '{id}' does not contain package.json at the archive root or inside a single top-level folder.");
            }

            Info($"[PackageResolver] Moving safely to {extractDir}...");
            Directory.Move(packageRoot, extractDir);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }

        return extractDir;
    }

    private static bool IsRegistryIndexUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.LocalPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRegistryArchiveUrl(string registryUrl, string archiveUrl)
    {
        if (Uri.TryCreate(archiveUrl, UriKind.Absolute, out var absoluteArchiveUri))
            return absoluteArchiveUri.ToString();

        var registryUri = new Uri(registryUrl, UriKind.Absolute);
        return new Uri(registryUri, archiveUrl).ToString();
    }

    private static IEnumerable<PackageRequirement> EnumerateRequirements(ProjectManifest manifest, string profile)
    {
        foreach (var requirement in manifest.Packages ?? new List<PackageRequirement>())
        {
            yield return requirement;
        }

        if (!string.IsNullOrWhiteSpace(profile)
            && manifest.Profiles != null
            && manifest.Profiles.TryGetValue(profile, out var profileDefinition))
        {
            foreach (var requirement in profileDefinition.Packages ?? new List<PackageRequirement>())
            {
                yield return requirement;
            }
        }
    }

    private static string FindExtractedPackageRoot(string extractDir)
    {
        if (File.Exists(Path.Combine(extractDir, "package.json")))
            return extractDir;

        var childDirectories = Directory.GetDirectories(extractDir);
        var childFiles = Directory.GetFiles(extractDir);
        if (childDirectories.Length == 1 && childFiles.Length == 0)
            return childDirectories[0];

        return extractDir;
    }

    private void Info(string message)
    {
        _logService?.Info(message);
    }

    private static string ComputeDirectoryHash(string directory)
    {
        using var sha256 = SHA256.Create();
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(directory, path).Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);

        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(directory, file).Replace('\\', '/');
            byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
            sha256.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
            sha256.TransformBlock(new byte[] { 0 }, 0, 1, null, 0);

            byte[] fileBytes = File.ReadAllBytes(file);
            sha256.TransformBlock(fileBytes, 0, fileBytes.Length, null, 0);
            sha256.TransformBlock(new byte[] { 0 }, 0, 1, null, 0);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return $"sha256:{Convert.ToHexString(sha256.Hash!).ToLowerInvariant()}";
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string NormalizeSha256Hash(string hash)
    {
        string trimmed = hash.Trim();
        if (trimmed.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            return $"sha256:{trimmed.Substring("sha256:".Length).ToLowerInvariant()}";

        return $"sha256:{trimmed.ToLowerInvariant()}";
    }

    private sealed record PackageRestoreResult(string PackagePath, string? ArchiveUrl, string? ArchiveHash, string? ResolvedVersion = null);

    private sealed class PackageRegistryIndex
    {
        public int SchemaVersion { get; set; }
        public List<PackageRegistryEntry> Packages { get; set; } = new();
    }

    private sealed class PackageRegistryEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public PackageRegistryArchive? Archive { get; set; }
    }

    private sealed class PackageRegistryArchive
    {
        public string Url { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    private sealed class PackageLockDocument
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, PackageLockEntry> Packages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public static PackageLockDocument Load(string path)
        {
            if (!File.Exists(path))
                return new PackageLockDocument();

            string json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<PackageLockDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new PackageLockDocument();
            document.Packages = new Dictionary<string, PackageLockEntry>(document.Packages, StringComparer.OrdinalIgnoreCase);
            return document;
        }

        public bool UpdateOrValidate(PackageRequirement requirement, string relativeCachePath, string contentHash, string? archiveUrl, string? archiveHash, string? resolvedVersion = null)
        {
            if (Packages.TryGetValue(requirement.Id, out var existing))
            {
                if (!string.Equals(existing.SourceUrl, requirement.Url, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Package lock source mismatch for '{requirement.Id}'. Locked source is '{existing.SourceUrl}', manifest requests '{requirement.Url}'. Remove the cached package and lock entry before changing package source.");
                }

                if (!string.Equals(existing.Version ?? string.Empty, requirement.Version ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Package lock version mismatch for '{requirement.Id}'. Locked version is '{existing.Version ?? "<none>"}', manifest requests '{requirement.Version ?? "<none>"}'.");
                }

                if (!string.Equals(existing.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Cached package '{requirement.Id}' does not match {LockFileName}. Expected {existing.ContentHash}, actual {contentHash}.");
                }

                if (!string.IsNullOrWhiteSpace(existing.ResolvedVersion)
                    && !string.IsNullOrWhiteSpace(resolvedVersion)
                    && !string.Equals(existing.ResolvedVersion, resolvedVersion, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Package lock resolved version mismatch for '{requirement.Id}'. Locked resolved version is '{existing.ResolvedVersion}', registry resolved '{resolvedVersion}'. Remove the cached package and lock entry to upgrade this range.");
                }

                if (!string.IsNullOrWhiteSpace(existing.ArchiveUrl)
                    && !string.IsNullOrWhiteSpace(archiveUrl)
                    && !string.Equals(existing.ArchiveUrl, archiveUrl, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Package lock archive URL mismatch for '{requirement.Id}'. Locked archive is '{existing.ArchiveUrl}', registry resolved '{archiveUrl}'.");
                }

                if (!string.IsNullOrWhiteSpace(existing.ArchiveHash)
                    && !string.IsNullOrWhiteSpace(archiveHash)
                    && !string.Equals(existing.ArchiveHash, archiveHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Package lock archive hash mismatch for '{requirement.Id}'. Locked archive hash is '{existing.ArchiveHash}', registry resolved '{archiveHash}'.");
                }

                if (!string.Equals(existing.CachePath, relativeCachePath, StringComparison.OrdinalIgnoreCase))
                {
                    existing.CachePath = relativeCachePath;
                    return true;
                }

                bool changed = false;
                if (string.IsNullOrWhiteSpace(existing.ArchiveUrl) && !string.IsNullOrWhiteSpace(archiveUrl))
                {
                    existing.ArchiveUrl = archiveUrl;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existing.ArchiveHash) && !string.IsNullOrWhiteSpace(archiveHash))
                {
                    existing.ArchiveHash = archiveHash;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existing.ResolvedVersion) && !string.IsNullOrWhiteSpace(resolvedVersion))
                {
                    existing.ResolvedVersion = resolvedVersion;
                    changed = true;
                }

                return changed;
            }

            Packages[requirement.Id] = new PackageLockEntry
            {
                Id = requirement.Id,
                Version = requirement.Version,
                SourceUrl = requirement.Url ?? string.Empty,
                CachePath = relativeCachePath,
                ContentHash = contentHash,
                ArchiveUrl = archiveUrl,
                ArchiveHash = archiveHash,
                ResolvedVersion = resolvedVersion,
                AcquiredAtUtc = DateTimeOffset.UtcNow
            };
            return true;
        }

        public void Save(string path)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            Packages = Packages
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            File.WriteAllText(path, JsonSerializer.Serialize(this, options));
        }
    }

    private sealed class PackageLockEntry
    {
        public string Id { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string SourceUrl { get; set; } = string.Empty;
        public string CachePath { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string? ArchiveUrl { get; set; }
        public string? ArchiveHash { get; set; }
        public string? ResolvedVersion { get; set; }
        public DateTimeOffset AcquiredAtUtc { get; set; }
    }
}

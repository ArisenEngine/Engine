using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ArisenLauncher.Services;

public sealed class PackageRegistryClient
{
    private static readonly HttpClient s_HttpClient = new();

    public async Task<IReadOnlyList<PackageRegistryPackageVersion>> GetPackagesAsync(string registryUrl)
    {
        string json = await s_HttpClient.GetStringAsync(registryUrl);
        return ParseIndex(json, registryUrl);
    }

    public static IReadOnlyList<PackageRegistryPackageVersion> ParseIndex(string json, string registryUrl)
    {
        var index = JsonSerializer.Deserialize<PackageRegistryIndex>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Registry index '{registryUrl}' could not be parsed.");

        if (index.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Registry index '{registryUrl}' has unsupported schemaVersion {index.SchemaVersion}.");
        }

        return index.Packages
            .Where(package => !string.IsNullOrWhiteSpace(package.Id) && !string.IsNullOrWhiteSpace(package.Version))
            .Select(package => new PackageRegistryPackageVersion
            {
                Id = package.Id,
                Version = package.Version,
                Name = package.Name ?? package.Id,
                Description = package.Description ?? string.Empty,
                Type = package.Type ?? "managed",
                Layer = package.Layer ?? string.Empty,
                ArchiveUrl = package.Archive == null ? string.Empty : ResolveArchiveUrl(registryUrl, package.Archive.Url),
                Sha256 = package.Archive?.Sha256 ?? string.Empty,
                SizeBytes = package.Archive?.SizeBytes ?? 0
            })
            .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static PackageRegistryPackageVersion? SelectPackageVersion(
        IEnumerable<PackageRegistryPackageVersion> packages,
        string packageId,
        string? versionRange,
        out string error)
    {
        error = string.Empty;
        if (!SemanticVersionRange.TryParse(versionRange, out var range, out error))
            return null;

        return range.SelectHighestMatch(packages
            .Where(package => string.Equals(package.Id, packageId, StringComparison.OrdinalIgnoreCase)));
    }

    private static string ResolveArchiveUrl(string registryUrl, string archiveUrl)
    {
        if (string.IsNullOrWhiteSpace(archiveUrl))
            return string.Empty;

        if (Uri.TryCreate(archiveUrl, UriKind.Absolute, out var absoluteArchiveUri))
            return absoluteArchiveUri.ToString();

        return new Uri(new Uri(registryUrl, UriKind.Absolute), archiveUrl).ToString();
    }

    private sealed class PackageRegistryIndex
    {
        public int SchemaVersion { get; set; }
        public List<PackageRegistryEntry> Packages { get; set; } = new();
    }

    private sealed class PackageRegistryEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? Layer { get; set; }
        public PackageRegistryArchive? Archive { get; set; }
    }

    private sealed class PackageRegistryArchive
    {
        public string Url { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }
}

public sealed class PackageRegistryPackageVersion
{
    public string Id { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Type { get; init; } = "managed";
    public string Layer { get; init; } = string.Empty;
    public string ArchiveUrl { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

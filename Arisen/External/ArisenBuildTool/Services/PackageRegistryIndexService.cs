using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArisenBuildTool.Models;

namespace ArisenBuildTool.Services;

public static class PackageRegistryIndexService
{
    private static readonly JsonSerializerOptions s_JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static PackageRegistryIndex Build(string sourceDirectory, string baseUrl = "")
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Registry package source directory not found: {sourceDirectory}");

        var entries = new List<PackageRegistryEntry>();
        var seenVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string archivePath in Directory.EnumerateFiles(sourceDirectory, "*.zip", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var manifest = ReadRootManifest(archivePath);
            if (string.IsNullOrWhiteSpace(manifest.Id))
                throw new InvalidDataException($"Package archive '{archivePath}' has no package id.");

            if (string.IsNullOrWhiteSpace(manifest.Version))
                throw new InvalidDataException($"Package archive '{archivePath}' has no package version.");

            string versionKey = $"{manifest.Id}@{manifest.Version}";
            if (!seenVersions.Add(versionKey))
                throw new InvalidDataException($"Duplicate package version in registry source: {versionKey}");

            var info = new FileInfo(archivePath);
            entries.Add(new PackageRegistryEntry
            {
                Id = manifest.Id,
                Version = manifest.Version,
                Name = string.IsNullOrWhiteSpace(manifest.Name) ? null : manifest.Name,
                Description = string.IsNullOrWhiteSpace(manifest.Description) ? null : manifest.Description,
                Type = string.IsNullOrWhiteSpace(manifest.Type) ? null : manifest.Type,
                Layer = string.IsNullOrWhiteSpace(manifest.Layer) ? null : manifest.Layer,
                Archive = new PackageRegistryArchive
                {
                    Url = BuildArchiveUrl(baseUrl, Path.GetFileName(archivePath)),
                    Sha256 = ComputeSha256(archivePath),
                    SizeBytes = info.Length
                }
            });
        }

        return new PackageRegistryIndex
        {
            Packages = entries
                .OrderBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Version, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public static void Write(string sourceDirectory, string outputPath, string baseUrl = "", bool overwrite = false)
    {
        if (File.Exists(outputPath) && !overwrite)
            throw new IOException($"Registry index already exists: {outputPath}");

        var index = Build(sourceDirectory, baseUrl);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.WriteAllText(outputPath, JsonSerializer.Serialize(index, s_JsonOptions));
    }

    private static PackageManifest ReadRootManifest(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entry = archive.GetEntry("package.json");
        if (entry == null)
            throw new InvalidDataException($"Package archive '{archivePath}' does not contain package.json at the zip root.");

        using var stream = entry.Open();
        var manifest = Utils.ManifestJson.Deserialize<PackageManifest>(stream);
        if (manifest == null)
            throw new InvalidDataException($"Package archive '{archivePath}' has an invalid package.json.");

        return manifest;
    }

    private static string ComputeSha256(string archivePath)
    {
        using var stream = File.OpenRead(archivePath);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildArchiveUrl(string baseUrl, string archiveName)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return archiveName.Replace('\\', '/');

        return $"{baseUrl.TrimEnd('/', '\\')}/{archiveName.Replace('\\', '/')}";
    }
}

public sealed class PackageRegistryIndex
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("packages")]
    public List<PackageRegistryEntry> Packages { get; set; } = new();
}

public sealed class PackageRegistryEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("layer")]
    public string? Layer { get; set; }

    [JsonPropertyName("archive")]
    public PackageRegistryArchive Archive { get; set; } = new();
}

public sealed class PackageRegistryArchive
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
}

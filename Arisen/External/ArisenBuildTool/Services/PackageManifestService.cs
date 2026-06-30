using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArisenBuildTool.Models;

namespace ArisenBuildTool.Services;

public static class PackageManifestService
{
    private static readonly JsonSerializerOptions s_JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static PackageManifest? ReadEffectiveManifest(string packageDir)
    {
        string packageJsonPath = Path.Combine(packageDir, "package.json");
        if (!File.Exists(packageJsonPath)) return null;

        var manifest = ReadManifestFile(packageJsonPath);
        if (manifest == null) return null;

        string generatedJsonPath = Path.Combine(packageDir, "package.generated.json");
        if (!File.Exists(generatedJsonPath)) return manifest;

        var generated = ReadManifestFile(generatedJsonPath);
        if (generated == null) return manifest;

        MergeGenerated(manifest, generated);
        return manifest;
    }

    public static PackageManifest? ReadManifestFile(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<PackageManifest>(json, s_JsonOptions);
    }

    private static void MergeGenerated(PackageManifest manifest, PackageManifest generated)
    {
        if (generated.Entry != null)
        {
            manifest.Entry = generated.Entry;
        }

        if (generated.Subsystems != null && generated.Subsystems.Count > 0)
        {
            manifest.Subsystems = MergeSubsystems(manifest.Subsystems, generated.Subsystems);
        }

        if (generated.Services != null)
        {
            manifest.Services ??= new PackageServices();

            if (generated.Services.Provides != null && generated.Services.Provides.Count > 0)
            {
                manifest.Services.Provides = MergeJsonElements(manifest.Services.Provides, generated.Services.Provides);
            }

            if (generated.Services.Requires != null && generated.Services.Requires.Count > 0)
            {
                manifest.Services.Requires = MergeJsonElements(manifest.Services.Requires, generated.Services.Requires);
            }
        }

        if (generated.NativeRuntimes != null && generated.NativeRuntimes.Count > 0)
        {
            manifest.NativeRuntimes = generated.NativeRuntimes;
        }

        if (generated.NativeTests != null && generated.NativeTests.Count > 0)
        {
            manifest.NativeTests = generated.NativeTests;
        }
    }

    private static List<PackageSubsystem> MergeSubsystems(List<PackageSubsystem>? authored, List<PackageSubsystem> generated)
    {
        var result = new List<PackageSubsystem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var subsystem in authored ?? Enumerable.Empty<PackageSubsystem>())
        {
            if (seen.Add(subsystem.Class)) result.Add(subsystem);
        }

        foreach (var subsystem in generated)
        {
            int existingIndex = result.FindIndex(x => string.Equals(x.Class, subsystem.Class, StringComparison.Ordinal));
            if (existingIndex >= 0)
            {
                result[existingIndex] = subsystem;
            }
            else
            {
                result.Add(subsystem);
            }
        }

        return result;
    }

    private static List<JsonElement> MergeJsonElements(List<JsonElement>? authored, List<JsonElement> generated)
    {
        var result = new List<JsonElement>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in authored ?? Enumerable.Empty<JsonElement>())
        {
            string key = GetServiceKey(element);
            if (seen.Add(key)) result.Add(element.Clone());
        }

        foreach (var element in generated)
        {
            string key = GetServiceKey(element);
            int existingIndex = result.FindIndex(x => string.Equals(GetServiceKey(x), key, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                result[existingIndex] = element.Clone();
            }
            else
            {
                result.Add(element.Clone());
            }
        }

        return result;
    }

    private static string GetServiceKey(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? string.Empty;
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("interface", out var interfaceElement)
            && interfaceElement.ValueKind == JsonValueKind.String)
        {
            return interfaceElement.GetString() ?? element.GetRawText();
        }

        return element.GetRawText();
    }
}

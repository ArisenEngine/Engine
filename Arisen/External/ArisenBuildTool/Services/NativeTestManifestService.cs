using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArisenBuildTool.Models;

namespace ArisenBuildTool.Services;

public sealed record NativeTestDescriptor(
    string RuntimeIdentifier,
    string Library,
    string RegisterExport);

public static class NativeTestManifestService
{
    public const string DefaultRegisterExport = "RegisterNativeTests";

    public static IEnumerable<NativeTestDescriptor> EnumerateForRuntime(
        PackageInfo package,
        string runtimeIdentifier,
        IList<string>? errors = null)
    {
        if (package.Manifest.NativeTests == null) yield break;

        foreach (var ridEntry in package.Manifest.NativeTests)
        {
            string rid = ridEntry.Key;
            if (string.IsNullOrWhiteSpace(rid))
            {
                errors?.Add($"Package '{package.Manifest.Id}' declares nativeTests with an empty runtime identifier.");
                continue;
            }

            if (ridEntry.Value == null)
            {
                errors?.Add($"Package '{package.Manifest.Id}' nativeTests['{rid}'] must be an array.");
                continue;
            }

            bool isTargetRid = string.Equals(rid, runtimeIdentifier, StringComparison.OrdinalIgnoreCase);
            for (int i = 0; i < ridEntry.Value.Count; i++)
            {
                if (!TryParse(package.Manifest.Id, rid, i, ridEntry.Value[i], errors, out var descriptor))
                {
                    continue;
                }

                if (isTargetRid)
                {
                    yield return descriptor;
                }
            }
        }
    }

    public static bool HasRuntime(PackageManifest manifest, string runtimeIdentifier)
    {
        return manifest.NativeTests != null
            && manifest.NativeTests.Keys.Any(rid => string.Equals(rid, runtimeIdentifier, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParse(
        string packageId,
        string runtimeIdentifier,
        int index,
        JsonElement element,
        IList<string>? errors,
        out NativeTestDescriptor descriptor)
    {
        descriptor = new NativeTestDescriptor(runtimeIdentifier, string.Empty, DefaultRegisterExport);

        string? library = null;
        string registerExport = DefaultRegisterExport;

        if (element.ValueKind == JsonValueKind.String)
        {
            library = element.GetString();
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            library = ReadStringProperty(element, "library") ?? ReadStringProperty(element, "name");
            registerExport = ReadStringProperty(element, "registerExport")
                ?? ReadStringProperty(element, "export")
                ?? DefaultRegisterExport;
        }
        else
        {
            errors?.Add($"Package '{packageId}' nativeTests['{runtimeIdentifier}'][{index}] must be a string or object.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(library))
        {
            errors?.Add($"Package '{packageId}' nativeTests['{runtimeIdentifier}'][{index}] has an empty library.");
            return false;
        }

        if (Path.IsPathRooted(library) || library.Contains('/') || library.Contains('\\'))
        {
            errors?.Add($"Package '{packageId}' native test library '{library}' must be a deployed file name, not a path.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(registerExport))
        {
            errors?.Add($"Package '{packageId}' nativeTests['{runtimeIdentifier}'][{index}] has an empty registerExport.");
            return false;
        }

        descriptor = new NativeTestDescriptor(runtimeIdentifier, library, registerExport);
        return true;
    }

    private static string? ReadStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}

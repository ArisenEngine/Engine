using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public sealed class NativeOutputValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Success => Errors.Count == 0;
}

public static class NativeOutputValidationService
{
    public static NativeOutputValidationResult Validate(string resolvedManifestPath, string outputDir, string? configuration = null)
    {
        var result = new NativeOutputValidationResult();
        resolvedManifestPath = Path.GetFullPath(resolvedManifestPath);
        outputDir = Path.GetFullPath(outputDir);

        if (!File.Exists(resolvedManifestPath))
        {
            result.Errors.Add($"Resolved manifest not found: {resolvedManifestPath}");
            return result;
        }

        if (!Directory.Exists(outputDir))
        {
            result.Errors.Add($"Native output directory not found: {outputDir}");
            return result;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(resolvedManifestPath), ManifestJson.DocumentOptions);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse resolved manifest '{resolvedManifestPath}': {ex.Message}");
            return result;
        }

        using (document)
        {
            if (!TryGetProperty(document.RootElement, "ResolvedPackages", out var packagesElement)
                || packagesElement.ValueKind != JsonValueKind.Array)
            {
                result.Errors.Add($"Resolved manifest '{resolvedManifestPath}' is missing a ResolvedPackages array.");
                return result;
            }

            foreach (var packageElement in packagesElement.EnumerateArray())
            {
                ValidatePackage(packageElement, outputDir, configuration, result);
            }
        }

        return result;
    }

    public static void LogSummary(NativeOutputValidationResult result)
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
            Logger.Info("Native output validation succeeded.");
        }
        else
        {
            Logger.Error($"Native output validation failed with {result.Errors.Count} error(s) and {result.Warnings.Count} warning(s).");
        }
    }

    private static void ValidatePackage(JsonElement packageElement, string outputDir, string? configuration, NativeOutputValidationResult result)
    {
        string packageId = TryGetProperty(packageElement, "Id", out var idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString() ?? "<unknown>"
            : "<unknown>";

        if (!TryGetProperty(packageElement, "NativeRuntimes", out var nativeRuntimesElement)
            || nativeRuntimesElement.ValueKind == JsonValueKind.Null
            || nativeRuntimesElement.ValueKind == JsonValueKind.Undefined)
        {
            return;
        }

        Dictionary<string, List<JsonElement>>? nativeRuntimes;
        try
        {
            nativeRuntimes = JsonSerializer.Deserialize<Dictionary<string, List<JsonElement>>>(
                nativeRuntimesElement.GetRawText(),
                ManifestJson.SerializerOptions);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Package '{packageId}' NativeRuntimes could not be parsed from resolved manifest: {ex.Message}");
            return;
        }

        var package = new PackageInfo
        {
            DirectoryPath = outputDir,
            Manifest = new PackageManifest
            {
                Id = packageId,
                NativeRuntimes = nativeRuntimes
            }
        };

        foreach (var descriptor in NativeRuntimeManifestService.EnumerateForRuntime(
                     package,
                     NativeRuntimeManifestService.DefaultRuntimeIdentifier,
                     result.Errors,
                     result.Warnings,
                     configuration: configuration))
        {
            ValidateDescriptorOutput(packageId, descriptor, outputDir, result);
        }
    }

    private static void ValidateDescriptorOutput(
        string packageId,
        NativeRuntimeDescriptor descriptor,
        string outputDir,
        NativeOutputValidationResult result)
    {
        string fileName = Path.GetFileName(descriptor.Path.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            result.Errors.Add($"Package '{packageId}' native runtime '{descriptor.Path}' does not resolve to an output file name.");
            return;
        }

        string outputPath = Path.Combine(outputDir, fileName);
        if (!File.Exists(outputPath))
        {
            string message = $"Package '{packageId}' deployed native runtime '{fileName}' was not found at '{outputPath}'.";
            if (descriptor.Required)
            {
                result.Errors.Add(message);
            }
            else
            {
                result.Warnings.Add(message);
            }

            return;
        }

        if (NativeRuntimeManifestService.GetExpectedExports(descriptor).Count > 0)
        {
            NativeRuntimeManifestService.ValidateExports(packageId, descriptor, outputPath, result.Errors);
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

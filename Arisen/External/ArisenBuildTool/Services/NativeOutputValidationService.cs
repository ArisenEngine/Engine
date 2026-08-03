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
            ValidateResolvedConfiguration(document.RootElement, configuration, result);
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

            ValidatePayloadInventory(
                document.RootElement,
                packagesElement,
                outputDir,
                configuration,
                result);
        }

        return result;
    }

    private static void ValidateResolvedConfiguration(
        JsonElement root,
        string? expectedConfiguration,
        NativeOutputValidationResult result)
    {
        if (!TryGetProperty(root, "NativePayloadsFinalized", out JsonElement finalizedElement) ||
            finalizedElement.ValueKind != JsonValueKind.True)
        {
            return;
        }

        if (!TryGetProperty(root, "Configuration", out JsonElement configurationElement) ||
            configurationElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(configurationElement.GetString()))
        {
            result.Errors.Add(
                "Finalized resolved manifest is missing a valid Configuration identity.");
            return;
        }

        string declaredConfiguration = configurationElement.GetString()!.Trim();
        if (!string.IsNullOrWhiteSpace(expectedConfiguration) &&
            !string.Equals(
                declaredConfiguration,
                expectedConfiguration.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add(
                $"Resolved manifest Configuration '{declaredConfiguration}' does not match requested configuration '{expectedConfiguration.Trim()}'.");
        }
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

    private static void ValidatePayloadInventory(
        JsonElement rootElement,
        JsonElement packagesElement,
        string outputDir,
        string? configuration,
        NativeOutputValidationResult result)
    {
        List<ResolvedNativeDeclaration> declarations = ReadResolvedNativeDeclarations(
            packagesElement,
            configuration,
            result);
        if (declarations.Count == 0) return;

        if (!TryGetProperty(rootElement, "NativePayloadsFinalized", out JsonElement finalizedElement) ||
            finalizedElement.ValueKind != JsonValueKind.True)
        {
            result.Errors.Add(
                "Resolved manifest native payload metadata is not finalized. Rebuild the entry project before boot.");
            return;
        }

        if (!TryGetProperty(rootElement, "NativePayloads", out JsonElement payloadsElement) ||
            payloadsElement.ValueKind != JsonValueKind.Array)
        {
            result.Errors.Add("Resolved manifest is missing the finalized NativePayloads array.");
            return;
        }

        var inventory = new Dictionary<string, ResolvedNativeInventoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement payloadElement in payloadsElement.EnumerateArray())
        {
            if (!TryReadInventoryEntry(payloadElement, result, out ResolvedNativeInventoryEntry? entry))
            {
                continue;
            }

            ResolvedNativeInventoryEntry inventoryEntry = entry!;
            if (!inventory.TryAdd(inventoryEntry.FileName, inventoryEntry))
            {
                result.Errors.Add(
                    $"Resolved manifest contains duplicate native payload inventory entry '{inventoryEntry.FileName}'.");
                continue;
            }

            string outputPath = Path.Combine(outputDir, inventoryEntry.FileName);
            if (!File.Exists(outputPath))
            {
                result.Errors.Add(
                    $"Finalized native payload '{inventoryEntry.FileName}' was not found at '{outputPath}'.");
                continue;
            }

            long actualSize = new FileInfo(outputPath).Length;
            if (actualSize != inventoryEntry.Size)
            {
                result.Errors.Add(
                    $"Native payload '{inventoryEntry.FileName}' size mismatch. Expected {inventoryEntry.Size} bytes, found {actualSize} bytes.");
                continue;
            }

            string actualHash = NativePayloadIntegrityService.ComputeSha256(outputPath);
            if (!string.Equals(actualHash, inventoryEntry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add(
                    $"Native payload '{inventoryEntry.FileName}' SHA-256 mismatch. Expected {inventoryEntry.Sha256}, found {actualHash}.");
            }
        }

        foreach (IGrouping<string, ResolvedNativeDeclaration> declarationGroup in declarations
                     .GroupBy(declaration => declaration.FileName, StringComparer.OrdinalIgnoreCase))
        {
            if (!inventory.TryGetValue(declarationGroup.Key, out ResolvedNativeInventoryEntry? entry))
            {
                if (declarationGroup.Any(declaration => declaration.Required))
                {
                    result.Errors.Add(
                        $"Required native payload '{declarationGroup.Key}' has no finalized hash inventory entry.");
                }

                continue;
            }

            string[] expectedOwners = declarationGroup
                .Select(declaration => declaration.PackageId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(owner => owner, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] actualOwners = entry.Owners
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(owner => owner, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (!expectedOwners.SequenceEqual(actualOwners, StringComparer.OrdinalIgnoreCase))
            {
                result.Errors.Add(
                    $"Native payload '{declarationGroup.Key}' owner mismatch. Expected [{string.Join(", ", expectedOwners)}], " +
                    $"inventory contains [{string.Join(", ", actualOwners)}].");
            }
        }

        var declaredFiles = declarations
            .Select(declaration => declaration.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string inventoryFile in inventory.Keys.Where(file => !declaredFiles.Contains(file)))
        {
            result.Errors.Add(
                $"Native payload inventory entry '{inventoryFile}' is not declared by any resolved package.");
        }
    }

    private static List<ResolvedNativeDeclaration> ReadResolvedNativeDeclarations(
        JsonElement packagesElement,
        string? configuration,
        NativeOutputValidationResult result)
    {
        var declarations = new List<ResolvedNativeDeclaration>();
        foreach (JsonElement packageElement in packagesElement.EnumerateArray())
        {
            string packageId = TryGetProperty(packageElement, "Id", out JsonElement idElement) &&
                               idElement.ValueKind == JsonValueKind.String
                ? idElement.GetString() ?? "<unknown>"
                : "<unknown>";
            if (!TryGetProperty(packageElement, "NativeRuntimes", out JsonElement runtimesElement) ||
                runtimesElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            Dictionary<string, List<JsonElement>>? nativeRuntimes;
            try
            {
                nativeRuntimes = JsonSerializer.Deserialize<Dictionary<string, List<JsonElement>>>(
                    runtimesElement.GetRawText(),
                    ManifestJson.SerializerOptions);
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    $"Package '{packageId}' NativeRuntimes could not be parsed for hash validation: {exception.Message}");
                continue;
            }

            var package = new PackageInfo
            {
                DirectoryPath = string.Empty,
                Manifest = new PackageManifest
                {
                    Id = packageId,
                    NativeRuntimes = nativeRuntimes
                }
            };
            foreach (NativeRuntimeDescriptor descriptor in NativeRuntimeManifestService.EnumerateForRuntime(
                         package,
                         NativeRuntimeManifestService.DefaultRuntimeIdentifier,
                         result.Errors,
                         result.Warnings,
                         configuration: configuration))
            {
                string fileName = Path.GetFileName(
                    descriptor.Path.Replace('/', Path.DirectorySeparatorChar));
                declarations.Add(new ResolvedNativeDeclaration(
                    packageId,
                    fileName,
                    descriptor.Required));
            }
        }

        return declarations;
    }

    private static bool TryReadInventoryEntry(
        JsonElement element,
        NativeOutputValidationResult result,
        out ResolvedNativeInventoryEntry? entry)
    {
        entry = null;
        if (element.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(element, "FileName", out JsonElement fileNameElement) ||
            fileNameElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(fileNameElement.GetString()))
        {
            result.Errors.Add("Resolved manifest contains a native payload entry without a valid FileName.");
            return false;
        }

        string fileName = fileNameElement.GetString()!;
        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            result.Errors.Add(
                $"Resolved native payload FileName '{fileName}' must be a destination basename.");
            return false;
        }

        if (!TryGetProperty(element, "Size", out JsonElement sizeElement) ||
            sizeElement.ValueKind != JsonValueKind.Number ||
            !sizeElement.TryGetInt64(out long size) ||
            size < 0)
        {
            result.Errors.Add(
                $"Resolved native payload '{fileName}' has an invalid Size.");
            return false;
        }

        if (!TryGetProperty(element, "Sha256", out JsonElement hashElement) ||
            hashElement.ValueKind != JsonValueKind.String ||
            !IsSha256(hashElement.GetString()))
        {
            result.Errors.Add(
                $"Resolved native payload '{fileName}' has an invalid SHA-256 value.");
            return false;
        }

        if (!TryGetProperty(element, "Owners", out JsonElement ownersElement) ||
            ownersElement.ValueKind != JsonValueKind.Array)
        {
            result.Errors.Add(
                $"Resolved native payload '{fileName}' has no Owners array.");
            return false;
        }

        string[] owners = ownersElement.EnumerateArray()
            .Where(owner => owner.ValueKind == JsonValueKind.String)
            .Select(owner => owner.GetString() ?? string.Empty)
            .Where(owner => !string.IsNullOrWhiteSpace(owner))
            .ToArray();
        if (owners.Length != ownersElement.GetArrayLength() || owners.Length == 0)
        {
            result.Errors.Add(
                $"Resolved native payload '{fileName}' contains an invalid owner identity.");
            return false;
        }

        entry = new ResolvedNativeInventoryEntry(
            fileName,
            size,
            hashElement.GetString()!,
            owners);
        return true;
    }

    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }

    private sealed record ResolvedNativeDeclaration(
        string PackageId,
        string FileName,
        bool Required);

    private sealed record ResolvedNativeInventoryEntry(
        string FileName,
        long Size,
        string Sha256,
        IReadOnlyList<string> Owners);

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

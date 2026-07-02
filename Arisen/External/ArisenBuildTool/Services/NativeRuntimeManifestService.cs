using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using ArisenBuildTool.Models;

namespace ArisenBuildTool.Services;

public enum NativeRuntimeSource
{
    Static,
    BuildOutput
}

public sealed record NativeRuntimeDescriptor(
    string RuntimeIdentifier,
    string Path,
    NativeRuntimeSource Source,
    bool Required,
    IReadOnlyList<string> Configurations,
    IReadOnlyList<string> Exports,
    string? InitExport,
    string? ShutdownExport);

public static class NativeRuntimeManifestService
{
    public const string DefaultRuntimeIdentifier = "win-x64";

    public static IEnumerable<NativeRuntimeDescriptor> EnumerateForRuntime(
        PackageInfo package,
        string runtimeIdentifier,
        IList<string>? errors = null,
        IList<string>? warnings = null,
        bool validateFiles = false)
    {
        if (package.Manifest.NativeRuntimes == null) yield break;

        foreach (var ridEntry in package.Manifest.NativeRuntimes)
        {
            string rid = ridEntry.Key;
            if (string.IsNullOrWhiteSpace(rid))
            {
                errors?.Add($"Package '{package.Manifest.Id}' declares nativeRuntimes with an empty runtime identifier.");
                continue;
            }

            if (ridEntry.Value == null)
            {
                errors?.Add($"Package '{package.Manifest.Id}' nativeRuntimes['{rid}'] must be an array.");
                continue;
            }

            bool isTargetRid = string.Equals(rid, runtimeIdentifier, StringComparison.OrdinalIgnoreCase);
            for (int i = 0; i < ridEntry.Value.Count; i++)
            {
                if (!TryParse(package.Manifest.Id, rid, i, ridEntry.Value[i], errors, out var descriptor))
                {
                    continue;
                }

                if (isTargetRid && validateFiles)
                {
                    ValidateDescriptor(package, descriptor, errors, warnings);
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
        return manifest.NativeRuntimes != null
            && manifest.NativeRuntimes.Keys.Any(rid => string.Equals(rid, runtimeIdentifier, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParse(
        string packageId,
        string runtimeIdentifier,
        int index,
        JsonElement element,
        IList<string>? errors,
        out NativeRuntimeDescriptor descriptor)
    {
        descriptor = new NativeRuntimeDescriptor(
            runtimeIdentifier,
            string.Empty,
            NativeRuntimeSource.BuildOutput,
            Required: true,
            Configurations: Array.Empty<string>(),
            Exports: Array.Empty<string>(),
            InitExport: null,
            ShutdownExport: null);

        string? path = null;
        NativeRuntimeSource? source = null;
        bool required = true;
        var configurations = Array.Empty<string>();
        var exports = Array.Empty<string>();
        string? initExport = null;
        string? shutdownExport = null;

        if (element.ValueKind == JsonValueKind.String)
        {
            path = element.GetString();
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            path = ReadStringProperty(element, "path") ?? ReadStringProperty(element, "name");
            if (!TryParseSource(
                    ReadStringProperty(element, "source") ?? ReadStringProperty(element, "kind"),
                    packageId,
                    runtimeIdentifier,
                    index,
                    errors,
                    out source))
            {
                return false;
            }

            if (element.TryGetProperty("required", out var requiredElement))
            {
                if (requiredElement.ValueKind == JsonValueKind.True) required = true;
                else if (requiredElement.ValueKind == JsonValueKind.False) required = false;
                else errors?.Add($"Package '{packageId}' nativeRuntimes['{runtimeIdentifier}'][{index}] has invalid 'required'. Expected boolean.");
            }

            configurations = ReadStringArray(element, "configurations", packageId, runtimeIdentifier, index, errors);
            exports = ReadStringArray(element, "exports", packageId, runtimeIdentifier, index, errors);
            initExport = ReadOptionalExportName(element, "initExport", packageId, runtimeIdentifier, index, errors);
            shutdownExport = ReadOptionalExportName(element, "shutdownExport", packageId, runtimeIdentifier, index, errors);
        }
        else
        {
            errors?.Add($"Package '{packageId}' nativeRuntimes['{runtimeIdentifier}'][{index}] must be a string or object.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            errors?.Add($"Package '{packageId}' nativeRuntimes['{runtimeIdentifier}'][{index}] has an empty runtime path.");
            return false;
        }

        if (Path.IsPathRooted(path))
        {
            errors?.Add($"Package '{packageId}' native runtime '{path}' must be package-relative, not absolute.");
            return false;
        }

        source ??= InferSource(path);
        descriptor = new NativeRuntimeDescriptor(
            runtimeIdentifier,
            path.Replace('\\', '/'),
            source.Value,
            required,
            configurations,
            exports,
            initExport,
            shutdownExport);
        return true;
    }

    private static void ValidateDescriptor(
        PackageInfo package,
        NativeRuntimeDescriptor descriptor,
        IList<string>? errors,
        IList<string>? warnings)
    {
        if (descriptor.Source == NativeRuntimeSource.BuildOutput)
        {
            if (descriptor.Path.Contains('/') || descriptor.Path.Contains('\\'))
            {
                errors?.Add($"Package '{package.Manifest.Id}' build-output native runtime '{descriptor.Path}' must be a file name, not a relative path.");
            }

            return;
        }

        string sourcePath = Path.GetFullPath(Path.Combine(package.DirectoryPath, descriptor.Path));
        if (!IsInsideDirectory(sourcePath, package.DirectoryPath))
        {
            errors?.Add($"Package '{package.Manifest.Id}' native runtime '{descriptor.Path}' escapes the package directory.");
            return;
        }

        if (!File.Exists(sourcePath))
        {
            string message = $"Package '{package.Manifest.Id}' required static native runtime '{descriptor.Path}' was not found at '{sourcePath}'.";
            if (descriptor.Required) errors?.Add(message);
            else warnings?.Add(message);
            return;
        }

        if (GetExpectedExports(descriptor).Count > 0)
        {
            ValidateExports(package.Manifest.Id, descriptor, sourcePath, errors);
        }
    }

    private static void ValidateExports(string packageId, NativeRuntimeDescriptor descriptor, string sourcePath, IList<string>? errors)
    {
        if (!string.Equals(Path.GetExtension(sourcePath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            errors?.Add($"Package '{packageId}' native runtime '{descriptor.Path}' declares exports but is not a DLL.");
            return;
        }

        try
        {
            var exports = ReadExportNames(sourcePath);
            foreach (var expectedExport in GetExpectedExports(descriptor))
            {
                if (!exports.Contains(expectedExport))
                {
                    errors?.Add($"Package '{packageId}' native runtime '{descriptor.Path}' is missing declared export '{expectedExport}'.");
                }
            }
        }
        catch (Exception ex)
        {
            errors?.Add($"Package '{packageId}' native runtime '{descriptor.Path}' export validation failed: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> GetExpectedExports(NativeRuntimeDescriptor descriptor)
    {
        var expectedExports = new HashSet<string>(descriptor.Exports, StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(descriptor.InitExport)) expectedExports.Add(descriptor.InitExport);
        if (!string.IsNullOrWhiteSpace(descriptor.ShutdownExport)) expectedExports.Add(descriptor.ShutdownExport);
        return expectedExports.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static HashSet<string> ReadExportNames(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        var headers = reader.PEHeaders;
        var directory = headers.PEHeader?.ExportTableDirectory;
        if (directory == null || directory.Value.RelativeVirtualAddress == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var block = reader.GetSectionData(directory.Value.RelativeVirtualAddress);
        var data = block.GetContent(0, Math.Min(directory.Value.Size, block.Length)).ToArray();
        if (data.Length < 40)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        uint numberOfNames = ReadUInt32(data, 24);
        uint addressOfNames = ReadUInt32(data, 32);
        var names = new HashSet<string>(StringComparer.Ordinal);

        for (uint i = 0; i < numberOfNames; i++)
        {
            int nameRvaOffset = RvaToOffset(headers, checked((int)(addressOfNames + i * 4)));
            stream.Position = nameRvaOffset;
            int nameRva = ReadInt32(stream);
            int nameOffset = RvaToOffset(headers, nameRva);
            stream.Position = nameOffset;
            names.Add(ReadNullTerminatedAscii(stream));
        }

        return names;
    }

    private static int RvaToOffset(PEHeaders headers, int rva)
    {
        foreach (var section in headers.SectionHeaders)
        {
            int sectionStart = section.VirtualAddress;
            int sectionEnd = sectionStart + Math.Max(section.VirtualSize, section.SizeOfRawData);
            if (rva >= sectionStart && rva < sectionEnd)
            {
                return section.PointerToRawData + (rva - sectionStart);
            }
        }

        throw new InvalidDataException($"RVA 0x{rva:X} does not map to any PE section.");
    }

    private static uint ReadUInt32(byte[] data, int offset)
    {
        return BitConverter.ToUInt32(data, offset);
    }

    private static int ReadInt32(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        if (stream.Read(buffer) != buffer.Length)
        {
            throw new EndOfStreamException();
        }

        return BitConverter.ToInt32(buffer);
    }

    private static string ReadNullTerminatedAscii(Stream stream)
    {
        var bytes = new List<byte>(64);
        int value;
        while ((value = stream.ReadByte()) > 0)
        {
            bytes.Add((byte)value);
        }

        if (value < 0)
        {
            throw new EndOfStreamException();
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static NativeRuntimeSource InferSource(string path)
    {
        return path.Contains('/') || path.Contains('\\')
            ? NativeRuntimeSource.Static
            : NativeRuntimeSource.BuildOutput;
    }

    public static bool IsInsideDirectory(string path, string directory)
    {
        string fullPath = Path.GetFullPath(path);
        string fullDirectory = Path.GetFullPath(directory);
        string normalizedDirectory = fullDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? fullDirectory
            : fullDirectory + Path.DirectorySeparatorChar;

        return fullPath.Equals(fullDirectory, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseSource(
        string? value,
        string packageId,
        string runtimeIdentifier,
        int index,
        IList<string>? errors,
        out NativeRuntimeSource? source)
    {
        source = null;
        if (string.IsNullOrWhiteSpace(value)) return true;

        if (string.Equals(value, "static", StringComparison.OrdinalIgnoreCase))
        {
            source = NativeRuntimeSource.Static;
            return true;
        }

        if (string.Equals(value, "buildOutput", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "generated", StringComparison.OrdinalIgnoreCase))
        {
            source = NativeRuntimeSource.BuildOutput;
            return true;
        }

        errors?.Add($"Package '{packageId}' nativeRuntimes['{runtimeIdentifier}'][{index}] has invalid source '{value}'. Valid values are static and buildOutput.");
        return false;
    }

    private static string? ReadStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string? ReadOptionalExportName(JsonElement element, string propertyName, string packageId, string runtimeIdentifier, int index, IList<string>? errors)
    {
        if (!element.TryGetProperty(propertyName, out var property)) return null;
        if (property.ValueKind != JsonValueKind.String)
        {
            errors?.Add($"Package '{packageId}' nativeRuntimes['{runtimeIdentifier}'][{index}] has invalid '{propertyName}'. Expected string.");
            return null;
        }

        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            errors?.Add($"Package '{packageId}' nativeRuntimes['{runtimeIdentifier}'][{index}] has an empty {propertyName}.");
            return null;
        }

        return value;
    }

    private static string[] ReadStringArray(JsonElement element, string propertyName, string packageId, string runtimeIdentifier, int index, IList<string>? errors)
    {
        if (!element.TryGetProperty(propertyName, out var property)) return Array.Empty<string>();
        if (property.ValueKind != JsonValueKind.Array)
        {
            errors?.Add($"Package '{packageId}' nativeRuntimes['{runtimeIdentifier}'][{index}] has invalid '{propertyName}'. Expected string array.");
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                errors?.Add($"Package '{packageId}' nativeRuntimes['{runtimeIdentifier}'][{index}] has invalid '{propertyName}' item. Expected non-empty string.");
                continue;
            }

            values.Add(item.GetString()!);
        }

        return values.ToArray();
    }
}

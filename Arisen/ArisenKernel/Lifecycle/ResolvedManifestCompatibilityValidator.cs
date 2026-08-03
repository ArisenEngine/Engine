using System.Security.Cryptography;
using System.Text.Json;
using Arisen.Versioning;

namespace ArisenKernel.Lifecycle;

internal static class ResolvedManifestCompatibilityValidator
{
    private const string RuntimeIdentifier = "win-x64";

    public static bool Validate(
        JsonElement root,
        JsonElement packagesElement,
        string outputDirectory,
        bool validateFinalizedNativePayloads,
        out string error)
    {
        var errors = new List<string>();
        List<ResolvedPackage> packages = ReadPackages(packagesElement, errors);
        ValidatePackageCompatibility(packages, errors);
        string fullOutputDirectory = Path.GetFullPath(outputDirectory);
        bool requiresDeclaredConfiguration = validateFinalizedNativePayloads &&
            TryGetProperty(root, "NativePayloadsFinalized", out JsonElement finalizedElement) &&
            finalizedElement.ValueKind == JsonValueKind.True;
        string? configuration = ResolveConfiguration(
            root,
            fullOutputDirectory,
            requiresDeclaredConfiguration,
            errors);
        if (validateFinalizedNativePayloads)
        {
            ValidateNativePayloads(
                root,
                packages,
                fullOutputDirectory,
                configuration,
                errors);
        }
        else
        {
            List<NativeDeclaration> declarations = ReadNativeDeclarations(
                packages,
                configuration,
                errors);
            ValidateNativeOwnership(declarations, errors);
        }

        error = string.Join(Environment.NewLine, errors.Select(value => $"- {value}"));
        return errors.Count == 0;
    }

    private static List<ResolvedPackage> ReadPackages(
        JsonElement packagesElement,
        ICollection<string> errors)
    {
        var packages = new List<ResolvedPackage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int index = 0;
        foreach (JsonElement packageElement in packagesElement.EnumerateArray())
        {
            string? id = ReadString(packageElement, "Id");
            string? version = ReadString(packageElement, "Version");
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"Resolved package entry {index} has no valid Id.");
                index++;
                continue;
            }

            if (!seen.Add(id))
            {
                errors.Add($"Resolved manifest contains duplicate package id '{id}'.");
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                errors.Add($"Resolved package '{id}' has no version.");
                version = string.Empty;
            }

            packages.Add(new ResolvedPackage(id, version, packageElement));
            index++;
        }

        return packages;
    }

    private static void ValidatePackageCompatibility(
        IReadOnlyCollection<ResolvedPackage> packages,
        ICollection<string> errors)
    {
        var versions = new Dictionary<string, SemanticVersion>(StringComparer.OrdinalIgnoreCase);
        foreach (ResolvedPackage package in packages)
        {
            if (!SemanticVersion.TryParseExact(package.Version, out SemanticVersion version))
            {
                errors.Add(
                    $"Resolved package '{package.Id}' declares invalid semantic version '{package.Version}'.");
            }
            else
            {
                versions[package.Id] = version;
            }

            ValidateEngineCompatibility(package, errors);
        }

        foreach (ResolvedPackage package in packages)
        {
            if (!TryGetProperty(package.Element, "Dependencies", out JsonElement dependencies) ||
                dependencies.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            if (dependencies.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"Resolved package '{package.Id}' Dependencies must be an object.");
                continue;
            }

            foreach (JsonProperty dependency in dependencies.EnumerateObject())
            {
                if (dependency.Value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(dependency.Value.GetString()))
                {
                    errors.Add(
                        $"Resolved dependency edge '{package.Id} -> {dependency.Name}' has no version constraint.");
                    continue;
                }

                if (!versions.TryGetValue(dependency.Name, out SemanticVersion dependencyVersion))
                {
                    errors.Add(
                        $"Resolved dependency edge '{package.Id} -> {dependency.Name}' targets a missing or invalid package version.");
                    continue;
                }

                string expression = dependency.Value.GetString()!;
                if (!SemanticVersionRange.TryParse(
                        expression,
                        out SemanticVersionRange range,
                        out string rangeError))
                {
                    errors.Add(
                        $"Resolved dependency edge '{package.Id} -> {dependency.Name}' has invalid constraint '{expression}': {rangeError}");
                }
                else if (!range.Matches(dependencyVersion))
                {
                    errors.Add(
                        $"Resolved dependency edge '{package.Id} -> {dependency.Name}' requires '{expression}', " +
                        $"but selected version is '{dependencyVersion}'.");
                }
            }
        }
    }

    private static void ValidateEngineCompatibility(
        ResolvedPackage package,
        ICollection<string> errors)
    {
        string? canonicalMinimum = null;
        if (TryGetProperty(package.Element, "Engine", out JsonElement engineElement) &&
            engineElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            if (engineElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"Resolved package '{package.Id}' Engine must be an object.");
                return;
            }

            canonicalMinimum = ReadString(engineElement, "minVersion");
            if (string.IsNullOrWhiteSpace(canonicalMinimum))
            {
                errors.Add(
                    $"Resolved package '{package.Id}' declares Engine without engine.minVersion.");
                return;
            }
        }

        string? legacyMinimum = ReadString(package.Element, "EngineVersion");
        if (!string.IsNullOrWhiteSpace(canonicalMinimum) &&
            !string.IsNullOrWhiteSpace(legacyMinimum) &&
            !string.Equals(canonicalMinimum, legacyMinimum, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"Resolved package '{package.Id}' has conflicting engine.minVersion '{canonicalMinimum}' and legacy EngineVersion '{legacyMinimum}'.");
            return;
        }

        string? effectiveMinimum = !string.IsNullOrWhiteSpace(canonicalMinimum)
            ? canonicalMinimum
            : legacyMinimum;
        if (string.IsNullOrWhiteSpace(effectiveMinimum)) return;

        if (!SemanticVersion.TryParseExact(effectiveMinimum, out SemanticVersion minimumVersion))
        {
            errors.Add(
                $"Resolved package '{package.Id}' has invalid engine minimum '{effectiveMinimum}'.");
        }
        else if (EngineCompatibility.CurrentVersion.CompareTo(minimumVersion) < 0)
        {
            errors.Add(
                $"Resolved package '{package.Id}' requires engine '{effectiveMinimum}' or newer, " +
                $"but the running engine is '{EngineCompatibility.CurrentVersionText}'.");
        }
    }

    private static void ValidateNativePayloads(
        JsonElement root,
        IReadOnlyCollection<ResolvedPackage> packages,
        string outputDirectory,
        string? configuration,
        ICollection<string> errors)
    {
        List<NativeDeclaration> declarations = ReadNativeDeclarations(
            packages,
            configuration,
            errors);
        if (declarations.Count == 0) return;

        ValidateNativeOwnership(declarations, errors);
        if (!TryGetProperty(root, "NativePayloadsFinalized", out JsonElement finalized) ||
            finalized.ValueKind != JsonValueKind.True)
        {
            errors.Add(
                "Resolved native payload metadata is not finalized. Rebuild the selected profile before boot.");
            return;
        }

        if (!TryGetProperty(root, "NativePayloads", out JsonElement payloadsElement) ||
            payloadsElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add("Resolved manifest is missing the finalized NativePayloads array.");
            return;
        }

        var inventory = new Dictionary<string, NativeInventoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement payloadElement in payloadsElement.EnumerateArray())
        {
            if (!TryReadInventoryEntry(payloadElement, errors, out NativeInventoryEntry? parsed))
            {
                continue;
            }

            NativeInventoryEntry entry = parsed!;
            if (!inventory.TryAdd(entry.FileName, entry))
            {
                errors.Add($"Native payload inventory contains duplicate entry '{entry.FileName}'.");
                continue;
            }

            string outputPath = Path.Combine(outputDirectory, entry.FileName);
            if (!File.Exists(outputPath))
            {
                errors.Add(
                    $"Finalized native payload '{entry.FileName}' is missing at '{outputPath}'.");
                continue;
            }

            long actualSize = new FileInfo(outputPath).Length;
            if (actualSize != entry.Size)
            {
                errors.Add(
                    $"Native payload '{entry.FileName}' size mismatch. Expected {entry.Size}, found {actualSize}.");
                continue;
            }

            string actualHash;
            try
            {
                using FileStream stream = File.OpenRead(outputPath);
                actualHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            }
            catch (Exception exception)
            {
                errors.Add(
                    $"Native payload '{entry.FileName}' could not be hashed: {exception.Message}");
                continue;
            }

            if (!string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Native payload '{entry.FileName}' SHA-256 mismatch. Expected {entry.Sha256}, found {actualHash}.");
            }
        }

        foreach (IGrouping<string, NativeDeclaration> group in declarations
                     .GroupBy(declaration => declaration.FileName, StringComparer.OrdinalIgnoreCase))
        {
            if (!inventory.TryGetValue(group.Key, out NativeInventoryEntry? entry))
            {
                if (group.Any(declaration => declaration.Required))
                {
                    errors.Add(
                        $"Required native payload '{group.Key}' has no finalized hash inventory entry.");
                }

                continue;
            }

            string[] expectedOwners = group
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
                errors.Add(
                    $"Native payload '{group.Key}' owner mismatch. Expected [{string.Join(", ", expectedOwners)}], " +
                    $"inventory contains [{string.Join(", ", actualOwners)}].");
            }

            string? expectedSharedPayload = group
                .Select(declaration => declaration.SharedPayload)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.Equals(
                    expectedSharedPayload ?? string.Empty,
                    entry.SharedPayload ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Native payload '{group.Key}' shared identity does not match its declarations.");
            }
        }

        var declaredFiles = declarations
            .Select(declaration => declaration.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string inventoryFile in inventory.Keys.Where(file => !declaredFiles.Contains(file)))
        {
            errors.Add(
                $"Native payload inventory entry '{inventoryFile}' is not declared by any resolved package.");
        }
    }

    private static List<NativeDeclaration> ReadNativeDeclarations(
        IReadOnlyCollection<ResolvedPackage> packages,
        string? configuration,
        ICollection<string> errors)
    {
        var declarations = new List<NativeDeclaration>();
        foreach (ResolvedPackage package in packages)
        {
            if (!TryGetProperty(package.Element, "NativeRuntimes", out JsonElement runtimes) ||
                runtimes.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            if (runtimes.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(runtimes, RuntimeIdentifier, out JsonElement entries))
            {
                continue;
            }

            if (entries.ValueKind != JsonValueKind.Array)
            {
                errors.Add(
                    $"Resolved package '{package.Id}' native runtime '{RuntimeIdentifier}' must be an array.");
                continue;
            }

            int index = 0;
            foreach (JsonElement element in entries.EnumerateArray())
            {
                string? path = element.ValueKind == JsonValueKind.String
                    ? element.GetString()
                    : element.ValueKind == JsonValueKind.Object
                        ? ReadString(element, "path") ?? ReadString(element, "name")
                        : null;
                if (string.IsNullOrWhiteSpace(path))
                {
                    errors.Add(
                        $"Resolved package '{package.Id}' native runtime entry {index} has no path.");
                    index++;
                    continue;
                }

                if (element.ValueKind == JsonValueKind.Object &&
                    !MatchesConfiguration(element, configuration, errors, package.Id, index))
                {
                    index++;
                    continue;
                }

                string fileName = Path.GetFileName(path.Replace('/', Path.DirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(fileName) ||
                    !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Resolved package '{package.Id}' native runtime '{path}' has no valid destination basename.");
                    index++;
                    continue;
                }

                bool required = element.ValueKind != JsonValueKind.Object ||
                    !TryGetProperty(element, "required", out JsonElement requiredElement) ||
                    requiredElement.ValueKind != JsonValueKind.False;
                string? sharedPayload = element.ValueKind == JsonValueKind.Object
                    ? ReadString(element, "sharedPayload")
                    : null;
                declarations.Add(new NativeDeclaration(
                    package.Id,
                    fileName,
                    required,
                    sharedPayload));
                index++;
            }
        }

        return declarations;
    }

    private static bool MatchesConfiguration(
        JsonElement element,
        string? configuration,
        ICollection<string> errors,
        string packageId,
        int index)
    {
        if (!TryGetProperty(element, "configurations", out JsonElement configurations)) return true;
        if (configurations.ValueKind != JsonValueKind.Array)
        {
            errors.Add(
                $"Resolved package '{packageId}' native runtime entry {index} has invalid configurations.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(configuration)) return true;
        return configurations.EnumerateArray().Any(value =>
            value.ValueKind == JsonValueKind.String &&
            string.Equals(value.GetString(), configuration, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateNativeOwnership(
        IReadOnlyCollection<NativeDeclaration> declarations,
        ICollection<string> errors)
    {
        foreach (IGrouping<string, NativeDeclaration> group in declarations
                     .GroupBy(declaration => declaration.FileName, StringComparer.OrdinalIgnoreCase))
        {
            string[] owners = group
                .Select(declaration => declaration.PackageId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (owners.Length <= 1) continue;

            string[] identities = group
                .Select(declaration => declaration.SharedPayload)
                .Where(identity => !string.IsNullOrWhiteSpace(identity))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(identity => identity!)
                .ToArray();
            if (identities.Length != 1 || group.Any(declaration =>
                    string.IsNullOrWhiteSpace(declaration.SharedPayload)))
            {
                errors.Add(
                    $"Native output basename '{group.Key}' has ambiguous owners [{string.Join(", ", owners)}] without one sharedPayload identity.");
            }
        }
    }

    private static bool TryReadInventoryEntry(
        JsonElement element,
        ICollection<string> errors,
        out NativeInventoryEntry? entry)
    {
        entry = null;
        string? runtimeIdentifier = ReadString(element, "RuntimeIdentifier");
        string? fileName = ReadString(element, "FileName");
        string? sha256 = ReadString(element, "Sha256");
        if (!string.Equals(runtimeIdentifier, RuntimeIdentifier, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal) ||
            sha256 is not { Length: 64 } ||
            !sha256.All(Uri.IsHexDigit))
        {
            errors.Add("Resolved manifest contains an invalid native payload inventory identity.");
            return false;
        }

        if (!TryGetProperty(element, "Size", out JsonElement sizeElement) ||
            sizeElement.ValueKind != JsonValueKind.Number ||
            !sizeElement.TryGetInt64(out long size) ||
            size < 0)
        {
            errors.Add($"Native payload inventory entry '{fileName}' has invalid Size.");
            return false;
        }

        if (!TryGetProperty(element, "Owners", out JsonElement ownersElement) ||
            ownersElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"Native payload inventory entry '{fileName}' has no Owners array.");
            return false;
        }

        string[] owners = ownersElement.EnumerateArray()
            .Where(owner => owner.ValueKind == JsonValueKind.String)
            .Select(owner => owner.GetString() ?? string.Empty)
            .Where(owner => !string.IsNullOrWhiteSpace(owner))
            .ToArray();
        if (owners.Length == 0 || owners.Length != ownersElement.GetArrayLength())
        {
            errors.Add($"Native payload inventory entry '{fileName}' has invalid owner identity.");
            return false;
        }

        entry = new NativeInventoryEntry(
            fileName,
            size,
            sha256,
            owners,
            ReadString(element, "SharedPayload"));
        return true;
    }

    private static string? GetConfiguration(string outputDirectory)
    {
        string name = new DirectoryInfo(Path.GetFullPath(outputDirectory)).Name;
        return name.Equals("Debug", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Release", StringComparison.OrdinalIgnoreCase)
            ? name
            : null;
    }

    private static string? ResolveConfiguration(
        JsonElement root,
        string outputDirectory,
        bool requireDeclaredConfiguration,
        ICollection<string> errors)
    {
        string? inferredConfiguration = GetConfiguration(outputDirectory);
        bool hasDeclaredConfiguration = TryGetProperty(
            root,
            "Configuration",
            out JsonElement configurationElement);
        string? declaredConfiguration = null;
        if (hasDeclaredConfiguration)
        {
            if (configurationElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(configurationElement.GetString()))
            {
                errors.Add("Resolved manifest Configuration must be a non-empty string.");
            }
            else
            {
                declaredConfiguration = configurationElement.GetString()!.Trim();
            }
        }
        else if (requireDeclaredConfiguration)
        {
            errors.Add(
                "Finalized resolved manifest is missing Configuration. Rebuild the selected profile before boot.");
        }

        if (!string.IsNullOrWhiteSpace(declaredConfiguration) &&
            !string.IsNullOrWhiteSpace(inferredConfiguration) &&
            !string.Equals(
                declaredConfiguration,
                inferredConfiguration,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"Resolved manifest Configuration '{declaredConfiguration}' does not match output directory configuration '{inferredConfiguration}'.");
        }

        return declaredConfiguration ?? inferredConfiguration;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out JsonElement value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private sealed record ResolvedPackage(
        string Id,
        string Version,
        JsonElement Element);

    private sealed record NativeDeclaration(
        string PackageId,
        string FileName,
        bool Required,
        string? SharedPayload);

    private sealed record NativeInventoryEntry(
        string FileName,
        long Size,
        string Sha256,
        IReadOnlyList<string> Owners,
        string? SharedPayload);
}

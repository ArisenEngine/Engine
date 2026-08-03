using System.Security.Cryptography;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public sealed record ResolvedNativePayload(
    string RuntimeIdentifier,
    string FileName,
    long Size,
    string Sha256,
    IReadOnlyList<string> Owners,
    string? SharedPayload);

public sealed class NativePayloadInventoryResult
{
    public List<ResolvedNativePayload> Payloads { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Success => Errors.Count == 0;
}

public static class NativePayloadIntegrityService
{
    public static void DeployStaticPayloads(
        IReadOnlyCollection<PackageInfo> packages,
        IReadOnlyCollection<string> outputDirectories,
        bool? enableProfiler = null)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(outputDirectories);

        var errors = new List<string>();
        var plans = new List<StaticDeploymentPlan>();
        var removals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string outputDirectory in outputDirectories)
        {
            string fullOutputDirectory = Path.GetFullPath(outputDirectory);
            string configuration = new DirectoryInfo(fullOutputDirectory).Name;
            var warnings = new List<string>();
            List<NativePayloadDeclaration> allDeclarations = CollectDeclarations(
                packages,
                NativeRuntimeManifestService.DefaultRuntimeIdentifier,
                configuration,
                errors,
                warnings);
            List<NativePayloadDeclaration> declarations = SelectForProfiler(
                allDeclarations,
                enableProfiler);
            ValidateOwnershipGroups(declarations, errors, validateStaticIdentity: true);
            foreach (string warning in warnings) Logger.Warning(warning);

            if (enableProfiler.HasValue)
            {
                var activeFiles = declarations
                    .Select(declaration => declaration.FileName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (string inactiveFile in allDeclarations
                             .Select(declaration => declaration.FileName)
                             .Where(fileName => !activeFiles.Contains(fileName))
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    removals.Add(Path.Combine(fullOutputDirectory, inactiveFile));
                }
            }

            foreach (IGrouping<string, NativePayloadDeclaration> group in declarations
                         .GroupBy(declaration => declaration.FileName, StringComparer.OrdinalIgnoreCase))
            {
                NativePayloadDeclaration[] staticDeclarations = group
                    .Where(declaration =>
                        declaration.Descriptor.Source == NativeRuntimeSource.Static)
                    .ToArray();
                if (staticDeclarations.Length == 0) continue;

                NativePayloadDeclaration? source = staticDeclarations.FirstOrDefault(declaration =>
                    File.Exists(declaration.StaticSourcePath));
                if (source == null)
                {
                    if (staticDeclarations.Any(declaration => declaration.Descriptor.Required))
                    {
                        errors.Add(
                            $"Required static native payload '{group.Key}' has no readable source for output '{fullOutputDirectory}'.");
                    }
                    else if (!group.Any(declaration =>
                                 declaration.Descriptor.Source == NativeRuntimeSource.BuildOutput))
                    {
                        removals.Add(Path.Combine(fullOutputDirectory, group.Key));
                    }

                    continue;
                }

                plans.Add(new StaticDeploymentPlan(
                    source.StaticSourcePath!,
                    Path.Combine(fullOutputDirectory, group.Key),
                    ReadIdentity(source.StaticSourcePath!)));
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Native payload deployment preflight failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => $"- {error}")));
        }

        foreach (string removal in removals)
        {
            RemoveInactiveNativePayload(removal);
        }

        foreach (StaticDeploymentPlan plan in plans)
        {
            AtomicCopyIfChanged(plan);
        }
    }

    public static void ValidateOwnership(
        IReadOnlyCollection<PackageInfo> packages,
        IList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(errors);

        var parseErrors = new List<string>();
        List<NativePayloadDeclaration> declarations = CollectDeclarations(
            packages,
            NativeRuntimeManifestService.DefaultRuntimeIdentifier,
            configuration: null,
            parseErrors,
            warnings: null);
        foreach (string parseError in parseErrors)
        {
            if (!errors.Contains(parseError, StringComparer.Ordinal)) errors.Add(parseError);
        }

        ValidateOwnershipGroups(declarations, errors, validateStaticIdentity: true);
    }

    public static NativePayloadInventoryResult BuildInventory(
        IReadOnlyCollection<PackageInfo> packages,
        string outputDirectory,
        string? configuration,
        bool? enableProfiler = null)
    {
        ArgumentNullException.ThrowIfNull(packages);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Native payload inventory requires an output directory.", nameof(outputDirectory));
        }

        var result = new NativePayloadInventoryResult();
        string fullOutputDirectory = Path.GetFullPath(outputDirectory);
        List<NativePayloadDeclaration> allDeclarations = CollectDeclarations(
            packages,
            NativeRuntimeManifestService.DefaultRuntimeIdentifier,
            configuration,
            result.Errors,
            result.Warnings);
        List<NativePayloadDeclaration> declarations = SelectForProfiler(
            allDeclarations,
            enableProfiler);
        ValidateOwnershipGroups(declarations, result.Errors, validateStaticIdentity: true);
        if (enableProfiler.HasValue)
        {
            var activeFiles = declarations
                .Select(declaration => declaration.FileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string inactiveFile in allDeclarations
                         .Select(declaration => declaration.FileName)
                         .Where(fileName => !activeFiles.Contains(fileName))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string inactivePath = Path.Combine(fullOutputDirectory, inactiveFile);
                if (File.Exists(inactivePath))
                {
                    result.Errors.Add(
                        $"Profile-disabled native payload '{inactiveFile}' remains at '{inactivePath}'.");
                }
            }
        }

        if (!result.Success) return result;

        foreach (IGrouping<string, NativePayloadDeclaration> group in declarations
                     .GroupBy(declaration => declaration.FileName, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            NativePayloadDeclaration[] groupDeclarations = group.ToArray();
            string outputPath = Path.Combine(fullOutputDirectory, group.Key);
            bool required = groupDeclarations.Any(declaration => declaration.Descriptor.Required);
            NativePayloadDeclaration[] staticDeclarations = groupDeclarations
                .Where(declaration =>
                    declaration.Descriptor.Source == NativeRuntimeSource.Static)
                .ToArray();
            bool hasReadableStaticSource = staticDeclarations.Any(declaration =>
                File.Exists(declaration.StaticSourcePath));
            bool hasBuildOutputOwner = groupDeclarations.Any(declaration =>
                declaration.Descriptor.Source == NativeRuntimeSource.BuildOutput);
            if (staticDeclarations.Length > 0 && !hasReadableStaticSource)
            {
                if (staticDeclarations.Any(declaration => declaration.Descriptor.Required))
                {
                    result.Errors.Add(
                        $"Required static native payload '{group.Key}' has no readable source and cannot be finalized.");
                    continue;
                }

                if (!hasBuildOutputOwner && File.Exists(outputPath))
                {
                    result.Errors.Add(
                        $"Optional static native payload '{group.Key}' has no readable source, but stale output remains at '{outputPath}'.");
                    continue;
                }
            }

            if (!File.Exists(outputPath))
            {
                string ownerDescription = FormatOwners(groupDeclarations);
                string message =
                    $"Native payload '{group.Key}' declared by {ownerDescription} was not found at '{outputPath}'.";
                if (required) result.Errors.Add(message);
                else result.Warnings.Add(message);
                continue;
            }

            FileIdentity outputIdentity = ReadIdentity(outputPath);
            foreach (NativePayloadDeclaration declaration in groupDeclarations
                         .Where(declaration => declaration.Descriptor.Source == NativeRuntimeSource.Static))
            {
                string sourcePath = declaration.StaticSourcePath!;
                if (!File.Exists(sourcePath)) continue;

                FileIdentity sourceIdentity = ReadIdentity(sourcePath);
                if (sourceIdentity != outputIdentity)
                {
                    result.Errors.Add(
                        $"Native payload '{group.Key}' in '{fullOutputDirectory}' is stale for package '{declaration.Package.Manifest.Id}'. " +
                        $"Expected static source SHA-256 {sourceIdentity.Sha256} ({sourceIdentity.Size} bytes), " +
                        $"but deployed output is {outputIdentity.Sha256} ({outputIdentity.Size} bytes).");
                }
            }

            string[] owners = groupDeclarations
                .Select(declaration => declaration.Package.Manifest.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(owner => owner, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string? sharedPayload = groupDeclarations
                .Select(declaration => declaration.Descriptor.SharedPayload)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            result.Payloads.Add(new ResolvedNativePayload(
                NativeRuntimeManifestService.DefaultRuntimeIdentifier,
                group.Key,
                outputIdentity.Size,
                outputIdentity.Sha256,
                owners,
                sharedPayload));
        }

        if (!result.Success) result.Payloads.Clear();
        return result;
    }

    public static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void AtomicCopyIfChanged(StaticDeploymentPlan plan)
    {
        if (File.Exists(plan.DestinationPath) &&
            ReadIdentity(plan.DestinationPath) == plan.SourceIdentity)
        {
            return;
        }

        string destinationDirectory = Path.GetDirectoryName(plan.DestinationPath)!;
        Directory.CreateDirectory(destinationDirectory);
        string stagingPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(plan.DestinationPath)}.arisen-stage-{Guid.NewGuid():N}");
        try
        {
            using (FileStream source = new(
                       plan.SourcePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            using (FileStream staging = new(
                       stagingPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                source.CopyTo(staging);
                staging.Flush(flushToDisk: true);
            }

            FileIdentity stagedIdentity = ReadIdentity(stagingPath);
            if (stagedIdentity != plan.SourceIdentity)
            {
                throw new IOException(
                    $"Staged native payload '{stagingPath}' does not match source '{plan.SourcePath}'.");
            }

            Logger.Info(
                $"Deploying native payload: {Path.GetFileName(plan.DestinationPath)} -> {destinationDirectory}");
            if (File.Exists(plan.DestinationPath))
            {
                File.Replace(stagingPath, plan.DestinationPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(stagingPath, plan.DestinationPath);
            }
        }
        finally
        {
            if (File.Exists(stagingPath)) File.Delete(stagingPath);
        }
    }

    private static void RemoveInactiveNativePayload(string path)
    {
        if (!File.Exists(path)) return;

        Logger.Info(
            $"Removing inactive native payload: {Path.GetFileName(path)} -> {Path.GetDirectoryName(path)}");
        File.Delete(path);
    }

    private static List<NativePayloadDeclaration> CollectDeclarations(
        IReadOnlyCollection<PackageInfo> packages,
        string runtimeIdentifier,
        string? configuration,
        IList<string> errors,
        IList<string>? warnings)
    {
        var declarations = new List<NativePayloadDeclaration>();
        foreach (PackageInfo package in packages.OrderBy(
                     package => package.Manifest.Id,
                     StringComparer.OrdinalIgnoreCase))
        {
            foreach (NativeRuntimeDescriptor descriptor in NativeRuntimeManifestService.EnumerateForRuntime(
                         package,
                         runtimeIdentifier,
                         errors,
                         warnings,
                         configuration: configuration))
            {
                string fileName = Path.GetFileName(
                    descriptor.Path.Replace('/', Path.DirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(fileName) ||
                    !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Package '{package.Manifest.Id}' native runtime '{descriptor.Path}' does not resolve to a destination basename.");
                    continue;
                }

                string? staticSourcePath = descriptor.Source == NativeRuntimeSource.Static
                    ? Path.GetFullPath(Path.Combine(package.DirectoryPath, descriptor.Path))
                    : null;
                declarations.Add(new NativePayloadDeclaration(
                    package,
                    descriptor,
                    fileName,
                    staticSourcePath));
            }
        }

        return declarations;
    }

    private static List<NativePayloadDeclaration> SelectForProfiler(
        IEnumerable<NativePayloadDeclaration> declarations,
        bool? enableProfiler)
    {
        return enableProfiler is false
            ? declarations.Where(declaration => !declaration.Descriptor.RequiresProfiler).ToList()
            : declarations.ToList();
    }

    private static void ValidateOwnershipGroups(
        IReadOnlyCollection<NativePayloadDeclaration> declarations,
        IList<string> errors,
        bool validateStaticIdentity)
    {
        var sharedPayloadFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (NativePayloadDeclaration declaration in declarations)
        {
            string? sharedPayload = declaration.Descriptor.SharedPayload;
            if (string.IsNullOrWhiteSpace(sharedPayload)) continue;

            if (sharedPayloadFiles.TryGetValue(sharedPayload, out string? existingFile) &&
                !string.Equals(existingFile, declaration.FileName, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Shared native payload identity '{sharedPayload}' is assigned to both '{existingFile}' and '{declaration.FileName}'. A shared identity must name exactly one destination basename.");
            }
            else
            {
                sharedPayloadFiles[sharedPayload] = declaration.FileName;
            }
        }

        foreach (IGrouping<string, NativePayloadDeclaration> group in declarations
                     .GroupBy(declaration => declaration.FileName, StringComparer.OrdinalIgnoreCase))
        {
            NativePayloadDeclaration[] values = group.ToArray();
            for (int leftIndex = 0; leftIndex < values.Length; leftIndex++)
            {
                for (int rightIndex = leftIndex + 1; rightIndex < values.Length; rightIndex++)
                {
                    NativePayloadDeclaration left = values[leftIndex];
                    NativePayloadDeclaration right = values[rightIndex];
                    if (!ConfigurationsOverlap(left.Descriptor, right.Descriptor)) continue;

                    bool crossPackage = !string.Equals(
                        left.Package.Manifest.Id,
                        right.Package.Manifest.Id,
                        StringComparison.OrdinalIgnoreCase);
                    if (crossPackage &&
                        (string.IsNullOrWhiteSpace(left.Descriptor.SharedPayload) ||
                         string.IsNullOrWhiteSpace(right.Descriptor.SharedPayload) ||
                         !string.Equals(
                             left.Descriptor.SharedPayload,
                             right.Descriptor.SharedPayload,
                             StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add(
                            $"Native output basename collision for '{group.Key}': package '{left.Package.Manifest.Id}' " +
                            $"declares '{left.Descriptor.Path}' and package '{right.Package.Manifest.Id}' declares " +
                            $"'{right.Descriptor.Path}' for overlapping configurations. Cross-package basename sharing " +
                            "requires the same non-empty sharedPayload identity on every declaration.");
                    }

                    if (!validateStaticIdentity ||
                        left.Descriptor.Source != NativeRuntimeSource.Static ||
                        right.Descriptor.Source != NativeRuntimeSource.Static ||
                        !File.Exists(left.StaticSourcePath) ||
                        !File.Exists(right.StaticSourcePath))
                    {
                        continue;
                    }

                    FileIdentity leftIdentity = ReadIdentity(left.StaticSourcePath!);
                    FileIdentity rightIdentity = ReadIdentity(right.StaticSourcePath!);
                    if (leftIdentity != rightIdentity)
                    {
                        errors.Add(
                            $"Native output basename '{group.Key}' resolves to different static content: " +
                            $"package '{left.Package.Manifest.Id}' has SHA-256 {leftIdentity.Sha256}, while " +
                            $"package '{right.Package.Manifest.Id}' has SHA-256 {rightIdentity.Sha256}.");
                    }
                }
            }
        }
    }

    private static bool ConfigurationsOverlap(
        NativeRuntimeDescriptor left,
        NativeRuntimeDescriptor right)
    {
        if (left.Configurations.Count == 0 || right.Configurations.Count == 0) return true;

        return left.Configurations.Any(leftConfiguration =>
            right.Configurations.Any(rightConfiguration => string.Equals(
                leftConfiguration,
                rightConfiguration,
                StringComparison.OrdinalIgnoreCase)));
    }

    private static FileIdentity ReadIdentity(string path)
    {
        var info = new FileInfo(path);
        return new FileIdentity(info.Length, ComputeSha256(path));
    }

    private static string FormatOwners(IEnumerable<NativePayloadDeclaration> declarations)
    {
        return string.Join(
            ", ",
            declarations
                .Select(declaration => $"package '{declaration.Package.Manifest.Id}'")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
    }

    private sealed record NativePayloadDeclaration(
        PackageInfo Package,
        NativeRuntimeDescriptor Descriptor,
        string FileName,
        string? StaticSourcePath);

    private sealed record StaticDeploymentPlan(
        string SourcePath,
        string DestinationPath,
        FileIdentity SourceIdentity);

    private readonly record struct FileIdentity(long Size, string Sha256);
}

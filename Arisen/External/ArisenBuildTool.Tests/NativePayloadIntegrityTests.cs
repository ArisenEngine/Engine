using System.Text.Json;
using ArisenBuildTool.Models;
using ArisenBuildTool.Services;
using Xunit;

namespace ArisenBuildTool.Tests;

public sealed class NativePayloadIntegrityTests
{
    [Fact]
    public void DeploymentReplacesSameSizePayloadByContentIdentity()
    {
        using var temp = new TempDirectory();
        PackageInfo package = CreateStaticPackage(
            temp.Path,
            "com.test.native",
            "payload/Native.Runtime.dll",
            "BBBB");
        string output = Path.Combine(temp.Path, "bin", "Debug");
        Directory.CreateDirectory(output);
        string destination = Path.Combine(output, "Native.Runtime.dll");
        File.WriteAllText(destination, "AAAA");
        File.SetLastWriteTimeUtc(
            Path.Combine(package.DirectoryPath, "payload", "Native.Runtime.dll"),
            new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(
            destination,
            new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        NativeDeploymentService.Deploy([package], [output], "Development");

        Assert.Equal("BBBB", File.ReadAllText(destination));
        Assert.Empty(Directory.EnumerateFiles(output, "*.arisen-stage-*"));
    }

    [Fact]
    public void DeploymentCollisionFailsBeforeExistingOutputChanges()
    {
        using var temp = new TempDirectory();
        PackageInfo left = CreateStaticPackage(
            temp.Path,
            "com.test.left",
            "left/Shared.dll",
            "LEFT");
        PackageInfo right = CreateStaticPackage(
            temp.Path,
            "com.test.right",
            "right/Shared.dll",
            "RGHT");
        string output = Path.Combine(temp.Path, "bin", "Debug");
        Directory.CreateDirectory(output);
        string destination = Path.Combine(output, "Shared.dll");
        File.WriteAllText(destination, "KEEP");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            NativeDeploymentService.Deploy([left, right], [output], "Development"));

        Assert.Contains("basename collision", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("KEEP", File.ReadAllText(destination));
    }

    [Fact]
    public void InventoryRejectsSameSizeStaleStaticOutput()
    {
        using var temp = new TempDirectory();
        PackageInfo package = CreateStaticPackage(
            temp.Path,
            "com.test.native",
            "payload/Native.Runtime.dll",
            "BBBB");
        string output = Path.Combine(temp.Path, "bin", "Debug");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "Native.Runtime.dll"), "AAAA");

        NativePayloadInventoryResult result = NativePayloadIntegrityService.BuildInventory(
            [package],
            output,
            "Debug");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error =>
            error.Contains("stale", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("SHA-256", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeploymentRemovesOrphanedOptionalStaticPayload()
    {
        using var temp = new TempDirectory();
        PackageInfo package = CreateStaticPackage(
            temp.Path,
            "com.test.native",
            "payload/Native.Optional.dll",
            "CURRENT",
            required: false);
        File.Delete(Path.Combine(
            package.DirectoryPath,
            "payload",
            "Native.Optional.dll"));
        string output = Path.Combine(temp.Path, "bin", "Debug");
        Directory.CreateDirectory(output);
        string destination = Path.Combine(output, "Native.Optional.dll");
        File.WriteAllText(destination, "STALE");

        NativeDeploymentService.Deploy([package], [output], "Development");

        Assert.False(File.Exists(destination));
    }

    [Fact]
    public void ProfilerDisabledDeploymentRemovesConditionalBuildOutput()
    {
        using var temp = new TempDirectory();
        PackageInfo package = CreateBuildOutputPackage(
            temp.Path,
            "com.test.profiler",
            "TracyClient.dll",
            requiresProfiler: true);
        string output = Path.Combine(temp.Path, "bin", "Production", "Debug");
        Directory.CreateDirectory(output);
        string payload = Path.Combine(output, "TracyClient.dll");
        File.WriteAllText(payload, "STALE");

        NativeDeploymentService.Deploy(
            [package],
            [output],
            "Production",
            enableProfiler: false);
        NativePayloadInventoryResult inventory = NativePayloadIntegrityService.BuildInventory(
            [package],
            output,
            "Debug",
            enableProfiler: false);

        Assert.False(File.Exists(payload));
        Assert.True(inventory.Success, string.Join(Environment.NewLine, inventory.Errors));
        Assert.Empty(inventory.Payloads);
    }

    [Fact]
    public void ProfilerDisabledInventoryRejectsConditionalBuildOutputWhenCleanupWasSkipped()
    {
        using var temp = new TempDirectory();
        PackageInfo package = CreateBuildOutputPackage(
            temp.Path,
            "com.test.profiler",
            "TracyClient.dll",
            requiresProfiler: true);
        string output = Path.Combine(temp.Path, "bin", "Production", "Debug");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "TracyClient.dll"), "STALE");

        NativePayloadInventoryResult inventory = NativePayloadIntegrityService.BuildInventory(
            [package],
            output,
            "Debug",
            enableProfiler: false);

        Assert.False(inventory.Success);
        Assert.Contains(inventory.Errors, error =>
            error.Contains("Profile-disabled", StringComparison.Ordinal) &&
            error.Contains("TracyClient.dll", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvedManifestProjectsProfilerConditionalPayloads()
    {
        using var temp = new TempDirectory();
        PackageInfo package = CreateBuildOutputPackage(
            temp.Path,
            "com.test.profiler",
            "TracyClient.dll",
            requiresProfiler: true);
        string output = Path.Combine(temp.Path, "resolved");

        PackageResolutionService.SaveResolvedManifests(
            "Production",
            [output],
            [package],
            enableProfiler: false);
        PackageResolutionService.SaveResolvedManifests(
            "Development",
            [output],
            [package],
            "manifest.profiler.resolved.json",
            enableProfiler: true);

        using JsonDocument production = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(output, "manifest.resolved.json")));
        using JsonDocument development = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(output, "manifest.profiler.resolved.json")));
        Assert.False(production.RootElement.GetProperty("EnableProfiler").GetBoolean());
        Assert.Empty(production.RootElement
            .GetProperty("ResolvedPackages")[0]
            .GetProperty("NativeRuntimes")
            .GetProperty("win-x64")
            .EnumerateArray());
        Assert.True(development.RootElement.GetProperty("EnableProfiler").GetBoolean());
        Assert.Equal(
            "TracyClient.dll",
            development.RootElement
                .GetProperty("ResolvedPackages")[0]
                .GetProperty("NativeRuntimes")
                .GetProperty("win-x64")[0]
                .GetProperty("path")
                .GetString());
    }

    [Fact]
    public void InventoryRejectsOrphanedOptionalStaticPayloadWhenDeploymentWasSkipped()
    {
        using var temp = new TempDirectory();
        PackageInfo package = CreateStaticPackage(
            temp.Path,
            "com.test.native",
            "payload/Native.Optional.dll",
            "CURRENT",
            required: false);
        File.Delete(Path.Combine(
            package.DirectoryPath,
            "payload",
            "Native.Optional.dll"));
        string output = Path.Combine(temp.Path, "bin", "Debug");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "Native.Optional.dll"), "STALE");

        NativePayloadInventoryResult result = NativePayloadIntegrityService.BuildInventory(
            [package],
            output,
            "Debug");

        Assert.False(result.Success);
        Assert.Empty(result.Payloads);
        Assert.Contains(result.Errors, error =>
            error.Contains("stale output remains", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingOptionalStaticSourcePreservesDeclaredBuildOutputOwner()
    {
        using var temp = new TempDirectory();
        PackageInfo staticOwner = CreateStaticPackage(
            temp.Path,
            "com.test.static",
            "payload/Shared.dll",
            "STATIC",
            sharedPayload: "vendor.shared",
            required: false);
        File.Delete(Path.Combine(staticOwner.DirectoryPath, "payload", "Shared.dll"));
        PackageInfo buildOwner = CreateBuildOutputPackage(
            temp.Path,
            "com.test.build",
            "Shared.dll",
            sharedPayload: "vendor.shared");
        string output = Path.Combine(temp.Path, "bin", "Debug");
        Directory.CreateDirectory(output);
        string destination = Path.Combine(output, "Shared.dll");
        File.WriteAllText(destination, "BUILD");

        NativeDeploymentService.Deploy(
            [staticOwner, buildOwner],
            [output],
            "Development");
        NativePayloadInventoryResult inventory = NativePayloadIntegrityService.BuildInventory(
            [staticOwner, buildOwner],
            output,
            "Debug");

        Assert.Equal("BUILD", File.ReadAllText(destination));
        Assert.True(inventory.Success, string.Join(Environment.NewLine, inventory.Errors));
        Assert.Single(inventory.Payloads);
    }

    [Fact]
    public void FinalizedManifestDetectsSameSizePayloadTampering()
    {
        using var temp = new TempDirectory();
        string output = Path.Combine(temp.Path, "bin", "Debug");
        Directory.CreateDirectory(output);
        string payloadPath = Path.Combine(output, "Native.Runtime.dll");
        File.WriteAllText(payloadPath, "GOOD");
        PackageInfo package = CreateBuildOutputPackage(
            temp.Path,
            "com.test.native",
            "Native.Runtime.dll");
        NativePayloadInventoryResult inventory = NativePayloadIntegrityService.BuildInventory(
            [package],
            output,
            "Debug");
        Assert.True(inventory.Success, string.Join(Environment.NewLine, inventory.Errors));
        PackageResolutionService.SaveResolvedManifests(
            "Development",
            [output],
            [package],
            nativePayloads: inventory.Payloads,
            nativePayloadsFinalized: true,
            configuration: "Debug");
        string resolvedManifest = Path.Combine(output, "manifest.resolved.json");
        Assert.True(
            NativeOutputValidationService.Validate(resolvedManifest, output, "Debug").Success);

        File.WriteAllText(payloadPath, "EVIL");
        NativeOutputValidationResult result = NativeOutputValidationService.Validate(
            resolvedManifest,
            output,
            "Debug");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error =>
            error.Contains("SHA-256 mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SharedStaticIdentityStillRequiresByteIdenticalSources()
    {
        using var temp = new TempDirectory();
        PackageInfo left = CreateStaticPackage(
            temp.Path,
            "com.test.left",
            "left/Shared.dll",
            "LEFT",
            sharedPayload: "vendor.shared");
        PackageInfo right = CreateStaticPackage(
            temp.Path,
            "com.test.right",
            "right/Shared.dll",
            "RGHT",
            sharedPayload: "vendor.shared");
        var errors = new List<string>();

        NativePayloadIntegrityService.ValidateOwnership([left, right], errors);

        Assert.Contains(errors, error =>
            error.Contains("different static content", StringComparison.OrdinalIgnoreCase));
    }

    private static PackageInfo CreateStaticPackage(
        string root,
        string packageId,
        string relativePath,
        string content,
        string? sharedPayload = null,
        bool required = true)
    {
        string packageRoot = Path.Combine(root, packageId);
        string sourcePath = Path.Combine(
            packageRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, content);
        var descriptor = new Dictionary<string, object?>
        {
            ["path"] = relativePath,
            ["source"] = "static",
            ["required"] = required
        };
        if (sharedPayload != null) descriptor["sharedPayload"] = sharedPayload;
        return CreatePackage(packageRoot, packageId, JsonSerializer.SerializeToElement(descriptor));
    }

    private static PackageInfo CreateBuildOutputPackage(
        string root,
        string packageId,
        string fileName,
        string? sharedPayload = null,
        bool requiresProfiler = false)
    {
        string packageRoot = Path.Combine(root, packageId);
        Directory.CreateDirectory(packageRoot);
        JsonElement descriptor;
        if (sharedPayload == null && !requiresProfiler)
        {
            descriptor = JsonSerializer.SerializeToElement(fileName);
        }
        else
        {
            var descriptorData = new Dictionary<string, object>
            {
                ["path"] = fileName,
                ["source"] = "buildOutput",
                ["requiresProfiler"] = requiresProfiler
            };
            if (sharedPayload != null) descriptorData["sharedPayload"] = sharedPayload;
            descriptor = JsonSerializer.SerializeToElement(descriptorData);
        }
        return CreatePackage(packageRoot, packageId, descriptor);
    }

    private static PackageInfo CreatePackage(
        string packageRoot,
        string packageId,
        JsonElement descriptor)
    {
        return new PackageInfo
        {
            DirectoryPath = packageRoot,
            Manifest = new PackageManifest
            {
                Id = packageId,
                Name = packageId,
                Version = "1.0.0",
                Type = "native",
                Layer = "driver",
                NativeRuntimes = new Dictionary<string, List<JsonElement>>
                {
                    ["win-x64"] = [descriptor]
                }
            }
        };
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ArisenNativePayloadTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort fixture cleanup.
            }
        }
    }
}

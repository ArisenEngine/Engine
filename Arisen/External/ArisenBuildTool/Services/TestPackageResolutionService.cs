using System;
using System.IO;
using System.Text.Json;

namespace ArisenBuildTool.Services;

/// <summary>
/// Decides which packages an isolated `--package X` test run mounts.
///
/// A package may own its own gate: when its `package.json` declares `nativeTests` and no companion
/// `X.test` package exists, the run mounts the package itself, so a capability does not need a second
/// repository just to be testable. A companion package stays the right shape when the tests need a
/// package graph of their own, and it wins whenever it exists.
/// </summary>
public static class TestPackageResolutionService
{
    public readonly record struct Resolution(
        bool Succeeded,
        string TestPackageId,
        bool UsesOwnTests,
        string RuntimeIdentifier,
        string Diagnostic);

    public static Resolution Resolve(string workspaceDir, string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        string companionId = packageId.EndsWith(".test", StringComparison.OrdinalIgnoreCase)
            ? packageId
            : $"{packageId}.test";
        if (!LocalPackageExists(workspaceDir, companionId))
        {
            if (PackageDeclaresNativeTests(workspaceDir, packageId, out string runtimeIdentifier))
            {
                return new Resolution(
                    Succeeded: true,
                    TestPackageId: packageId,
                    UsesOwnTests: true,
                    RuntimeIdentifier: runtimeIdentifier,
                    Diagnostic: string.Empty);
            }

            return new Resolution(
                Succeeded: false,
                TestPackageId: companionId,
                UsesOwnTests: false,
                RuntimeIdentifier: string.Empty,
                Diagnostic:
                    $"Expected companion test package '{companionId}' at " +
                    $"'{Path.Combine(workspaceDir, "Local", companionId)}'. Declare 'nativeTests' on " +
                    $"'{packageId}' itself, or create the companion .test package.");
        }

        return new Resolution(
            Succeeded: true,
            TestPackageId: companionId,
            UsesOwnTests: string.Equals(companionId, packageId, StringComparison.OrdinalIgnoreCase),
            RuntimeIdentifier: string.Empty,
            Diagnostic: string.Empty);
    }

    public static bool LocalPackageExists(string workspaceDir, string packageId)
    {
        string packageDir = Path.Combine(workspaceDir, "Local", packageId);
        return Directory.Exists(packageDir) &&
            File.Exists(Path.Combine(packageDir, "package.json"));
    }

    /// <summary>
    /// True when the package's own `package.json` declares native tests. The first declared runtime
    /// identifier is reported so the log names the payload the tests come from.
    /// </summary>
    public static bool PackageDeclaresNativeTests(
        string workspaceDir,
        string packageId,
        out string runtimeIdentifier)
    {
        runtimeIdentifier = string.Empty;
        string packageJsonPath = Path.Combine(workspaceDir, "Local", packageId, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
            if (!document.RootElement.TryGetProperty("nativeTests", out JsonElement nativeTests) ||
                nativeTests.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (JsonProperty runtime in nativeTests.EnumerateObject())
            {
                if (runtime.Value.ValueKind == JsonValueKind.Array && runtime.Value.GetArrayLength() > 0)
                {
                    runtimeIdentifier = runtime.Name;
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }
}

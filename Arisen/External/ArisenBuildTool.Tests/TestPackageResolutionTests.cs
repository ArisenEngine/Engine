using ArisenBuildTool.Services;
using Xunit;

namespace ArisenBuildTool.Tests;

public sealed class TestPackageResolutionTests : IDisposable
{
    private readonly string m_Root = Path.Combine(
        Path.GetTempPath(),
        "ArisenTestPackageResolution",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void PackageWithItsOwnNativeTests_ResolvesToThePackageItself()
    {
        WritePackage("com.arisen.physics", """
            {
              "id": "com.arisen.physics",
              "nativeTests": { "win-x64": [ { "library": "Physics.Test.dll", "registerExport": "RegisterNativeTests" } ] }
            }
            """);

        TestPackageResolutionService.Resolution resolution =
            TestPackageResolutionService.Resolve(m_Root, "com.arisen.physics");

        Assert.True(resolution.Succeeded);
        Assert.True(resolution.UsesOwnTests);
        Assert.Equal("com.arisen.physics", resolution.TestPackageId);
        Assert.Equal("win-x64", resolution.RuntimeIdentifier);
        Assert.Empty(resolution.Diagnostic);
    }

    [Fact]
    public void CompanionTestPackage_WinsOverThePackageItself()
    {
        WritePackage("com.arisen.rhi.vulkan.native", """
            {
              "id": "com.arisen.rhi.vulkan.native",
              "nativeTests": { "win-x64": [ { "library": "unused.dll", "registerExport": "RegisterNativeTests" } ] }
            }
            """);
        WritePackage("com.arisen.rhi.vulkan.native.test", """{ "id": "com.arisen.rhi.vulkan.native.test" }""");

        TestPackageResolutionService.Resolution resolution =
            TestPackageResolutionService.Resolve(m_Root, "com.arisen.rhi.vulkan.native");

        Assert.True(resolution.Succeeded);
        Assert.False(resolution.UsesOwnTests);
        Assert.Equal("com.arisen.rhi.vulkan.native.test", resolution.TestPackageId);
    }

    [Fact]
    public void PackageWithoutNativeTestsAndWithoutCompanion_ReportsWhatToDo()
    {
        WritePackage("com.arisen.bare", """{ "id": "com.arisen.bare" }""");

        TestPackageResolutionService.Resolution resolution =
            TestPackageResolutionService.Resolve(m_Root, "com.arisen.bare");

        Assert.False(resolution.Succeeded);
        Assert.Contains("nativeTests", resolution.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("com.arisen.bare.test", resolution.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIdThatAlreadyNamesTheTestPackage_ResolvesToItself()
    {
        WritePackage("com.arisen.rhi.vulkan.native.test", """{ "id": "com.arisen.rhi.vulkan.native.test" }""");

        TestPackageResolutionService.Resolution resolution =
            TestPackageResolutionService.Resolve(m_Root, "com.arisen.rhi.vulkan.native.test");

        Assert.True(resolution.Succeeded);
        Assert.Equal("com.arisen.rhi.vulkan.native.test", resolution.TestPackageId);
    }

    private void WritePackage(string packageId, string packageJson)
    {
        string directory = Path.Combine(m_Root, "Local", packageId);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "package.json"), packageJson);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, recursive: true);
            }
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }
}

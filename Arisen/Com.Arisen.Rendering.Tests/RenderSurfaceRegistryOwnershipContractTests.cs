using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderSurfaceRegistryOwnershipContractTests
{
    [Fact]
    public void RenderingUsesKernelOwnerWithoutPublishingManagedPointers()
    {
        string subsystemSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderSubsystem.cs");
        string queueSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RHICommandQueue.cs");
        string viewportSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/Core/Views/ArisenViewportControl.cs");

        Assert.Contains("EngineKernel.Instance.RenderSurfaces", subsystemSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ARISEN_SURFACE_REGISTRY_ADDR", subsystemSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GCHandle", subsystemSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.SetEnvironmentVariable", subsystemSource, StringComparison.Ordinal);
        Assert.Contains("RenderSurfaceRegistration", queueSource, StringComparison.Ordinal);
        Assert.Contains("RenderSurfaceRegistration", viewportSource, StringComparison.Ordinal);
        Assert.Contains("registration != _renderSurfaceRegistration", viewportSource, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Arisen")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}

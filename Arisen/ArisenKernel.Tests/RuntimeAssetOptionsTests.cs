using ArisenKernel.Lifecycle;
using Xunit;

namespace ArisenKernel.Tests;

public sealed class RuntimeAssetOptionsTests
{
    [Fact]
    public void ParseDefaultsToCookedSelection()
    {
        RuntimeAssetOptions options = RuntimeAssetOptions.Parse([]);

        Assert.False(options.EnableSourceAssetDiagnostics);
        options.Validate("Development", deployedLaunch: false);
    }

    [Fact]
    public void ParseRecognizesExplicitDiagnosticSourceOption()
    {
        RuntimeAssetOptions options = RuntimeAssetOptions.Parse(
            ["--frames", "1", RuntimeAssetOptions.SourceDiagnosticsArgument]);

        Assert.True(options.EnableSourceAssetDiagnostics);
        options.Validate("Development", deployedLaunch: false);
        options.Validate("RHIVulkanTesting", deployedLaunch: false);
    }

    [Theory]
    [InlineData("Production", false, "Production")]
    [InlineData("Editor", false, "compile-owned")]
    [InlineData("Development", true, "deployed")]
    public void DiagnosticSourceOptionRejectsInvalidLaunches(
        string profile,
        bool deployedLaunch,
        string expectedDiagnostic)
    {
        var options = new RuntimeAssetOptions(EnableSourceAssetDiagnostics: true);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            options.Validate(profile, deployedLaunch));

        Assert.Contains(expectedDiagnostic, error.Message, StringComparison.OrdinalIgnoreCase);
    }
}

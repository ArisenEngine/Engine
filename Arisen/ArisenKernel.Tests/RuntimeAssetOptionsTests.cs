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
        options.Validate("Development", deployedLaunch: false, isEditorProfile: false, isDeployedProfile: false);
    }

    [Fact]
    public void ParseRecognizesExplicitDiagnosticSourceOption()
    {
        RuntimeAssetOptions options = RuntimeAssetOptions.Parse(
            ["--frames", "1", RuntimeAssetOptions.SourceDiagnosticsArgument]);

        Assert.True(options.EnableSourceAssetDiagnostics);
        options.Validate("Development", deployedLaunch: false, isEditorProfile: false, isDeployedProfile: false);
        options.Validate("RHIVulkanTesting", deployedLaunch: false, isEditorProfile: false, isDeployedProfile: false);
    }

    [Theory]
    // A profile that declares the deployed role is rejected whatever it is called, which is the
    // point of the role being declared rather than inferred from the name.
    [InlineData("Production", false, false, true, "deployed profile")]
    [InlineData("Gallery", false, false, true, "deployed profile")]
    [InlineData("Development", false, false, true, "deployed profile")]
    [InlineData("Editor", false, true, false, "compile-owned")]
    [InlineData("Development", true, false, false, "deployed launch")]
    public void DiagnosticSourceOptionRejectsInvalidLaunches(
        string profile,
        bool deployedLaunch,
        bool isEditorProfile,
        bool isDeployedProfile,
        string expectedDiagnostic)
    {
        var options = new RuntimeAssetOptions(EnableSourceAssetDiagnostics: true);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            options.Validate(profile, deployedLaunch, isEditorProfile, isDeployedProfile));

        Assert.Contains(expectedDiagnostic, error.Message, StringComparison.OrdinalIgnoreCase);
    }
}

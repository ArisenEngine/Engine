using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class OutdoorAtmospherePipelineContractTests
{
    [Fact]
    public void AtmospherePassUsesExplicitGraphDepthAndHdrAttachmentDependencies()
    {
        string pipeline = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        string pass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/OutdoorAtmospherePass.cs");

        Assert.Contains("DepthAttachmentSampled2D(", pipeline, StringComparison.Ordinal);
        Assert.Contains("if (atmosphereEnabled)", pipeline, StringComparison.Ordinal);
        Assert.Contains("m_OutdoorAtmospherePass", pipeline, StringComparison.Ordinal);
        Assert.Contains(".ReadShader(frameDepthResource)", pipeline, StringComparison.Ordinal);
        Assert.Contains(".ReadWriteColorAttachment(", pipeline, StringComparison.Ordinal);
        Assert.Contains("EAttachmentLoadOp.ATTACHMENT_LOAD_OP_LOAD", pass, StringComparison.Ordinal);
        Assert.Contains("BLEND_FACTOR_SRC_ALPHA", pass, StringComparison.Ordinal);
        Assert.Contains("BLEND_FACTOR_ONE_MINUS_SRC_ALPHA", pass, StringComparison.Ordinal);

        int transparentPass = pipeline.IndexOf(
            "m_TransparentStaticMeshPass.Prepare(context)",
            StringComparison.Ordinal);
        int atmospherePass = pipeline.IndexOf(
            "// 5. Optional atmosphere",
            StringComparison.Ordinal);
        int tonemapPass = pipeline.IndexOf(
            "// 6. Tonemap",
            StringComparison.Ordinal);
        Assert.True(transparentPass >= 0 && atmospherePass > transparentPass);
        Assert.True(tonemapPass > atmospherePass);
    }

    [Fact]
    public void EnvironmentFrameBufferIsSharedAndPreparedOutsideRecording()
    {
        string frameData = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/EnvironmentFrameData.cs");
        string skyPass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/EnvironmentSkyPass.cs");
        string atmospherePass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/OutdoorAtmospherePass.cs");

        Assert.Contains("public const int VectorCount = 16", frameData, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFrameBuffer", frameData, StringComparison.Ordinal);
        Assert.Contains("ERHIMemoryUsage.Upload", frameData, StringComparison.Ordinal);
        Assert.Contains("RegisterBindlessResourceBuffer", frameData, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFramePushConstants.From", skyPass, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFramePushConstants.From", atmospherePass, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateBuffer", skyPass, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateBuffer", atmospherePass, StringComparison.Ordinal);
    }

    [Fact]
    public void ProceduralSunUsesAcceptedDirectionalLightWhilePanoramaStillFeedsIbl()
    {
        string pipeline = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        string frameData = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/EnvironmentFrameData.cs");
        string skyShader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Assets/Shaders/EnvironmentSky.hlsl");

        Assert.Contains("GetPrimaryDirectionalLight(context)", pipeline, StringComparison.Ordinal);
        Assert.Contains("EnsureEnvironmentLighting(context, environmentTexture)", pipeline, StringComparison.Ordinal);
        Assert.Contains("environmentTexture?.OutdoorProfile", pipeline, StringComparison.Ordinal);
        Assert.Contains("directionalLight.Direction", frameData, StringComparison.Ordinal);
        Assert.Contains("m_SunDirectionIntensity", frameData, StringComparison.Ordinal);
        Assert.Contains("SKY_MODE_PANORAMA", skyShader, StringComparison.Ordinal);
        Assert.Contains("SKY_MODE_PROCEDURAL_OUTDOOR", skyShader, StringComparison.Ordinal);
        Assert.Contains("sunDirectionIntensity", skyShader, StringComparison.Ordinal);
        Assert.Contains("environmentImageIndex", skyShader, StringComparison.Ordinal);
    }

    [Fact]
    public void AtmosphereShaderOwnsNoBackendProjectionBranchOrSceneColorFeedback()
    {
        string shader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Assets/Shaders/OutdoorAtmosphere.hlsl");

        Assert.Contains("LinearizeDepth", shader, StringComparison.Ordinal);
        Assert.Contains("deviceDepth >= 0.999999", shader, StringComparison.Ordinal);
        Assert.Contains("deviceDepth <= 0.000001", shader, StringComparison.Ordinal);
        Assert.Contains("SV_Position", shader, StringComparison.Ordinal);
        Assert.Contains("heightFogProfile", shader, StringComparison.Ordinal);
        Assert.DoesNotContain("VULKAN", shader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sceneColor", shader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#if", shader, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualValidationCorrelatesAtmosphereStateWithNormalAndRelocatedRuns()
    {
        string pipeline = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        string runtimeValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_runtime.bat");
        string relocatedValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_relocated_production.ps1");

        const string marker = "[GenericRP.AtmosphereValidation]";
        const string validator = "validate_outdoor_atmosphere_visuals.ps1";
        Assert.Contains(marker, pipeline, StringComparison.Ordinal);
        Assert.Contains("if (IsVisualSummaryEnabled)", pipeline, StringComparison.Ordinal);
        Assert.Contains(validator, runtimeValidation, StringComparison.Ordinal);
        Assert.Contains(validator, relocatedValidation, StringComparison.Ordinal);
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

        throw new InvalidOperationException(
            "Could not locate repository root from test output directory.");
    }
}

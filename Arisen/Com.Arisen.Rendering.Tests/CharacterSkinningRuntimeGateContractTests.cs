using Xunit;

namespace Com.Arisen.Rendering.Tests;

/// <summary>
/// Pins the catalogue-to-runtime wiring of the character-skinning sample's own gate: the entry
/// declares it, validate_runtime launches the sample directly and recovers the evidence, and the
/// validator is what decides the sample's "correct" - the driver engaged on the cooked SKINNED
/// variant with playback not parked, and captures at different clip times that actually differ.
/// </summary>
public sealed class CharacterSkinningRuntimeGateContractTests
{
    [Fact]
    public void TheRuntimeGateLaunchesTheSampleAndProvesPlayback()
    {
        string runtimeValidation = ReadRepoFile("Arisen/Scripts/Windows/validate_runtime.bat");
        string validator = ReadRepoFile("Arisen/Scripts/Windows/validate_character_skinning_summary.ps1");
        string profileResult = ReadRepoFile("Arisen/Scripts/Windows/write_runtime_profile_result.ps1");
        string runtimeSummary = ReadRepoFile("Arisen/Scripts/Windows/write_runtime_validation_summary.ps1");
        string catalogue = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.samples/Assets/Catalogue/ShowcaseSamples.arisensamples");

        // The catalogue declares both the gate id and the artifact the runtime gate produces.
        Assert.Contains("Gate: character-skinning", catalogue, StringComparison.Ordinal);
        Assert.Contains(
            "Artifact: visual-summary-Samples-character-skinning-latest.json",
            catalogue,
            StringComparison.Ordinal);

        // The gate launches the sample directly (rather than the profile's default scene) and feeds
        // every artifact into the validator.
        Assert.Contains(":run_character_skinning_sample_gate", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("--startup-sample character-skinning", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("CURRENT_CHARACTER_SKINNING_REQUIRED", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("validate_character_skinning_summary.ps1", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains(
            "CURRENT_CHARACTER_SKINNING_SUMMARY_PATH=%LOG_DIR%\\visual-summary-%CURRENT_PROFILE%-character-skinning-latest.json",
            runtimeValidation,
            StringComparison.Ordinal);

        // The run is evidence: the profile result and the aggregate summary publish the gate's
        // artifacts beside every other gate's.
        Assert.Contains("characterSkinning = [ordered]@{", profileResult, StringComparison.Ordinal);
        Assert.Contains("CURRENT_CHARACTER_SKINNING_GATE_PATH", profileResult, StringComparison.Ordinal);
        Assert.Contains("characterSkinningSampleRuns", runtimeSummary, StringComparison.Ordinal);
        Assert.Contains("characterSkinningGateArtifactPaths", runtimeSummary, StringComparison.Ordinal);

        // The validator is the definition of "the sample is correct on screen": the driver binds
        // and samples, the SKINNED yet cooked variant is what runs, parked playback fails the gate,
        // and two of the three captures at different clip times must differ.
        Assert.Contains("Bound the sample character", validator, StringComparison.Ordinal);
        Assert.Contains("Clip 'Clip_0' | Elapsed", validator, StringComparison.Ordinal);
        Assert.Contains("playback is parked", validator, StringComparison.Ordinal);
        Assert.Contains(".kw-SKINNED.", validator, StringComparison.Ordinal);
        Assert.Contains("Prepared skinned pipeline batch", validator, StringComparison.Ordinal);
        Assert.Contains("the pose is not advancing", validator, StringComparison.Ordinal);
        Assert.Contains("distinctHashes.Count -ge 2", validator, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(CppSourceContractScanner.FindRepoRoot(), relativePath));
    }
}


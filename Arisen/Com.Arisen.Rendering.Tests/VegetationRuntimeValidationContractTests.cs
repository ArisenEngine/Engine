using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationRuntimeValidationContractTests
{
    [Fact]
    public void RelocatedProductionAuditsExactVegetationRuntimeClosure()
    {
        string validation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_relocated_production.ps1");

        Assert.Contains("vegetationClosureComplete = $false", validation, StringComparison.Ordinal);
        Assert.Contains("$checks.vegetationClosureComplete = $true", validation, StringComparison.Ordinal);
        Assert.Contains("schemaVersion = 6", validation, StringComparison.Ordinal);

        foreach (string identity in new[]
        {
            "c0a92f10-0eb9-4d24-b729-7d0f38313001",
            "2a536b1f-81cf-4d91-a84f-39bc6f7e15a2",
            "9d7a4c3e-f2b6-46a1-8c59-5e1087b34d20",
            "runtime.vegetation-cluster.v1",
            "runtime.vegetation-instance-page.v1",
            "runtime.vegetation-biome.v1",
            "runtime.vegetation-species.v1",
            "staticmesh.uint32",
            "material.runtime",
            "com.arisen.packagegame",
            "com.arisen.vegetation.generic-renderpipeline"
        })
        {
            Assert.Contains(identity, validation, StringComparison.Ordinal);
        }

        // Species, meshes, and materials are authored content, so the relocation gate pins them;
        // cluster and page identities are baked per cell, so the gate pins the regional scale and
        // derives the identities from the deployed catalog instead of a per-cell table.
        foreach (VegetationCanonicalSpecies species in VegetationCanonicalFixture.Species)
        {
            Assert.Contains(
                species.SpeciesGuid.ToString("D"),
                validation,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                species.MeshGuid.ToString("D"),
                validation,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                species.MaterialGuid.ToString("D"),
                validation,
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(
            $"$expectedVegetationClusterCount = {MistfallValleyRegionFixture.ClusterCount}",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            $"$expectedVegetationInstancePageCount = {MistfallValleyRegionFixture.InstancePageCount}",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            "$expectedVegetationRuntimeArtifactCount = " +
            MistfallValleyRegionFixture.VegetationRuntimeArtifactCount,
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            "$expectedVegetationDeploymentFileCount = " +
            MistfallValleyRegionFixture.VegetationDeploymentFileCount,
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            $"$terrainTileCount = {MistfallValleyRegionFixture.TileCount}",
            validation,
            StringComparison.Ordinal);

        // Derived closure: the shared biome closes over the shipped species, every species over
        // its mesh and material, every cluster over the biome, one species, and its pages, every
        // page over exactly its own species, no page in two clusters, and every cluster and page
        // required by exactly one deployed cell scene.
        Assert.Equal(
            3,
            CountOccurrences(validation, "Assert-ExactRequiredCatalogDependencies `"));
        Assert.Contains(
            "foreach ($species in $vegetationSpecies) {",
            validation,
            StringComparison.Ordinal);
        Assert.Contains("$vegetationPageOwners = @{}", validation, StringComparison.Ordinal);
        Assert.Contains(
            "$vegetationPageOwners.Count -ne $vegetationInstancePages.Count",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            "$catalogDependents[$dependencyKey] += $catalogOwner",
            validation,
            StringComparison.Ordinal);
        Assert.Contains("must be required by exactly one ", validation, StringComparison.Ordinal);
        Assert.Contains(
            "$deployedVegetationClusterGuids = @(",
            validation,
            StringComparison.Ordinal);
        Assert.Contains("$worldReachable.Contains($artifactKey)", validation, StringComparison.Ordinal);
        Assert.Contains("$pipelineReachable.Contains($shaderKey)", validation, StringComparison.Ordinal);
        Assert.Contains(
            $"exactly {MistfallValleyRegionFixture.VegetationRuntimeArtifactCount} regional cooked vegetation artifacts",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            "exactly four cooked vegetation shader stages",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            $"exactly the {MistfallValleyRegionFixture.VegetationDeploymentFileCount} catalog-referenced vegetation files",
            validation,
            StringComparison.Ordinal);
        Assert.DoesNotContain("vegetationExpectations", validation, StringComparison.Ordinal);
        Assert.Contains(
            "Relocated Content contains unreferenced vegetation file",
            validation,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RelocatedProductionRejectsVegetationAuthoringSourcesOnly()
    {
        string validation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_relocated_production.ps1");

        int sourcePatternStart = validation.IndexOf(
            "$workspaceSourcePattern",
            StringComparison.Ordinal);
        int sourcePatternEnd = validation.IndexOf(
            "Assert-SourceIndependentRun",
            sourcePatternStart + 1,
            StringComparison.Ordinal);
        string sourcePattern = validation[sourcePatternStart..sourcePatternEnd];
        Assert.Contains("\\.arivegetationspecies", sourcePattern, StringComparison.Ordinal);
        Assert.Contains("\\.arivegetationbiome", sourcePattern, StringComparison.Ordinal);
        Assert.Contains("\\.arivegetationscatter", sourcePattern, StringComparison.Ordinal);
        Assert.Contains("\\.arivegetationgenerated", sourcePattern, StringComparison.Ordinal);

        int forbiddenStart = validation.IndexOf("$forbiddenSourceFiles", StringComparison.Ordinal);
        int forbiddenEnd = validation.IndexOf(
            "$checks.sourceFilesAbsent = $true",
            forbiddenStart,
            StringComparison.Ordinal);
        string forbiddenBlock = validation[forbiddenStart..forbiddenEnd];
        Assert.Contains("\".arivegetationscatter\"", forbiddenBlock, StringComparison.Ordinal);
        Assert.Contains("\".arivegetationgenerated\"", forbiddenBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("\".arivegetationspecies\"", forbiddenBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("\".arivegetationbiome\"", forbiddenBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeGatesEnforceVegetationReleaseAndUnregisterOrdering()
    {
        string runtimeValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_runtime.bat");
        string relocatedValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_relocated_production.ps1");
        string normalizedRuntimeValidation = runtimeValidation.Replace(
            "''",
            "'",
            StringComparison.Ordinal);
        int vegetationBlockStart = runtimeValidation.IndexOf(
            "if ($vegetationSelected -and $vulkanSelected)",
            StringComparison.Ordinal);
        Assert.True(vegetationBlockStart >= 0);
        int vegetationBlockEnd = runtimeValidation.IndexOf(
            "Write-Host ('[Arisen] Runtime package shutdown log passed",
            vegetationBlockStart,
            StringComparison.Ordinal);
        Assert.True(vegetationBlockEnd > vegetationBlockStart);
        string runtimeVegetationBlock = runtimeValidation[vegetationBlockStart..vegetationBlockEnd];

        foreach (string marker in new[]
        {
            "[Vegetation.GenericRP] Feature device-resource release completed.",
            "[RHILoader::DestroyCurrentInstance] Destroying active RHI instance.",
            "[GenericRP.Features] Unregistering feature 'com.arisen.vegetation.generic-renderpipeline'.",
            "[VulkanRHIPackage] Unloaded Vulkan RHI backend."
        })
        {
            Assert.Contains(marker, normalizedRuntimeValidation, StringComparison.Ordinal);
            Assert.Contains(marker, relocatedValidation, StringComparison.Ordinal);
        }

        Assert.Contains("$releaseIndex -lt 0", runtimeVegetationBlock, StringComparison.Ordinal);
        Assert.Contains(
            "($destroyIndex -ge 0 -and $releaseIndex -ge $destroyIndex)",
            runtimeVegetationBlock,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "$destroyIndex -lt 0",
            runtimeVegetationBlock,
            StringComparison.Ordinal);
        Assert.Contains("$unregisterIndex -lt 0", runtimeVegetationBlock, StringComparison.Ordinal);
        Assert.Contains("$releaseIndex -ge $unregisterIndex", runtimeVegetationBlock, StringComparison.Ordinal);
        Assert.Contains("$vulkanUnloadIndex -lt 0", runtimeVegetationBlock, StringComparison.Ordinal);
        Assert.Contains("$unregisterIndex -ge $vulkanUnloadIndex", runtimeVegetationBlock, StringComparison.Ordinal);
        Assert.Contains("$releaseIndex -ge $destroyIndex", relocatedValidation, StringComparison.Ordinal);
        Assert.Contains("$releaseIndex -ge $unregisterIndex", relocatedValidation, StringComparison.Ordinal);
        Assert.Contains("$unregisterIndex -ge $vulkanUnloadIndex", relocatedValidation, StringComparison.Ordinal);
        Assert.Contains(
            "vegetationStreamingShutdownPassed = $false",
            relocatedValidation,
            StringComparison.Ordinal);
        Assert.Contains(
            "$checks.vegetationStreamingShutdownPassed = $true",
            relocatedValidation,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeGatesRequireCanonicalVegetationValidationRecords()
    {
        string runtimeValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_runtime.bat");
        string relocatedValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_relocated_production.ps1");
        string submissionValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_vegetation_submission_log.ps1");

        // One shared validator owns the submission-record contract for the runtime gate, the
        // relocated Production gate, and the editor viewport gate, so a record that does not
        // satisfy every half of it fails the gate instead of only being counted.
        Assert.Contains(
            "[Vegetation.GenericRP.Validation]",
            submissionValidation,
            StringComparison.Ordinal);
        foreach (VegetationCanonicalSpecies species in VegetationCanonicalFixture.Species)
        {
            Assert.Contains(
                species.SpeciesGuid.ToString("N"),
                submissionValidation,
                StringComparison.OrdinalIgnoreCase);
        }

        foreach (string contract in new[]
        {
            "Extracted=(?<extracted>[0-9]+)",
            "CullingInputs=(?<cullingInputs>[0-9]+)",
            "PreparedClusters=(?<preparedClusters>[0-9]+)",
            "ClustersOverflow=(?<clustersOverflow>[0-9]+)",
            "$cullingInputs -le $extracted",
            "$preparedClusters -le $cullingInputs",
            "$dropped -eq 0",
            "$cascades -eq $ExpectedCascadeCount",
            "$opaqueBatches -ge 1 -and $opaqueInstances -ge 1",
            "$record.Groups[\"recordedShadowBatches\"].Value -eq ($opaqueBatches * $cascades)",
            "$record.Groups[\"recordedShadowInstances\"].Value -eq",
            "$entries.Count -eq $preparedClusters",
            "$reportedInstances -eq $opaqueInstances",
            "$record.Groups[\"firstCluster\"]",
            "$allowedClusterGuidSet.Contains($entryClusterGuid)",
            "$surfaces.Count -ge $RequiredDistinctSurfaceCount",
            "Dropped=(?<dropped>[0-9]+) Ticket=(?<ticket>[1-9][0-9]*)"
        })
        {
            Assert.Contains(contract, submissionValidation, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(
            "[Vegetation.GenericRP] Submitted vegetation draw commands",
            submissionValidation,
            StringComparison.Ordinal);

        // Both gates delegate to that validator instead of carrying a private copy of one cell's
        // showcase counts, which the regional startup world can no longer satisfy.
        Assert.Contains("call :validate_veg_submission_log \"1\"", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("call :validate_veg_submission_log \"2\"", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains(
            "-File \"%SCRIPT_ROOT%validate_vegetation_submission_log.ps1\"",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.Contains(
            "-RequiredDistinctSurfaceCount %CURRENT_VEGETATION_REQUIRED_SURFACE_COUNT%",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.Contains(
            "-ResolvedManifestPath \"%BIN_DIR%\\manifest.resolved.json\"",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.Contains(
            "-RequiredPackageId \"com.arisen.vegetation.generic-renderpipeline\"",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PreparedClusters=4 Cluster=324397e1",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.DoesNotContain("OpaqueInstances=49835", runtimeValidation, StringComparison.Ordinal);

        Assert.Contains(
            "& (Join-Path $PSScriptRoot \"validate_vegetation_submission_log.ps1\")",
            relocatedValidation,
            StringComparison.Ordinal);
        Assert.Contains(
            "-AllowedClusterGuids @($deployedVegetationClusterGuids)",
            relocatedValidation,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VegetationVisualValidatorComparesCanonicalDuringStateWithRelativeMargins()
    {
        string validation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_vegetation_rendering_visuals.ps1");

        foreach (string contract in new[]
        {
            "$activeCellIds = @($checkpoint.activeCellIds | ForEach-Object { [string]$_ })",
            "$activeCellIds.Count -ge 1 -and",
            "$_ -notmatch \"^[0-9A-Fa-f]{8}-([0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}$\"",
            "$minimumWorldDepthShare = 0.25",
            "does not commit a settled set of active world cells",
            "does not render the streamed world",
            "$checkpoint.expectedComponents.meshRendererCount -ge 1",
            "[string]$_.name -ceq \"during\"",
            "[string]$_.capture.name -ceq \"during\"",
            "$candidate.StateSignature -ceq $disabled.StateSignature",
            "$candidate.RenderSignature -ceq $disabled.RenderSignature",
            "$minimumOpaqueAverageLuminanceDelta",
            "$minimumOpaqueSpatialLuminanceDelta",
            "$minimumOpaqueAverageDepthDelta",
            "$minimumOpaqueSpatialDepthDelta",
            "$minimumOpaqueWrittenDepthPixelDelta",
            "$minimumShadowAverageLuminanceDelta",
            "$minimumShadowSpatialLuminanceDelta",
            "$opaqueDepthMetrics -ceq $fullDepthMetrics",
            "$shadowAverageLuminanceDelta =",
            "Full vegetation rendering did not produce a meaningful darker shadow contribution"
        })
        {
            Assert.Contains(contract, validation, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(
            "867184E03ACD2E1CE5A7E366B8986A0B8F0B7DD5EE3C89916E6B72CD6F7879B1",
            validation,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "frameIndex -eq $disabled",
            validation,
            StringComparison.Ordinal);
        // The compared 'during' cell follows the authored pose, so the validator must not pin a
        // cell identity of the single-cell showcase world any more.
        Assert.DoesNotContain(
            "5d13eda6-606a-57a0-bae4-cd559ddad464",
            validation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeGatesOwnVegetationModesAndReportVisualArtifacts()
    {
        string runtimeValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_runtime.bat");
        string relocatedValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_relocated_production.ps1");
        string profileResult = ReadRepoFile(
            "Arisen/Scripts/Windows/write_runtime_profile_result.ps1");
        string runtimeSummary = ReadRepoFile(
            "Arisen/Scripts/Windows/write_runtime_validation_summary.ps1");

        Assert.Contains(
            "set \"ARISEN_VEGETATION_RENDER_VALIDATION_MODE=full\"",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.Contains(":run_vegetation_visual_mode_smoke", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains(
            "call :run_vegetation_visual_mode_smoke \"disabled\" \"disabled\"",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.Contains(
            "call :run_vegetation_visual_mode_smoke \"opaque-only\" \"opaque-only\"",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.Contains("setlocal EnableExtensions EnableDelayedExpansion", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("call :validate_vulkan_player_log", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("call :validate_runtime_asset_policy", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("call :validate_runtime_shutdown_log", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("validate_vegetation_rendering_visuals.ps1", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("-FullSummaryPath \"!CURRENT_WORLD_STREAMING_SUMMARY_PATH!\"", runtimeValidation, StringComparison.Ordinal);

        Assert.Contains(
            "$processInfo.EnvironmentVariables[",
            relocatedValidation,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"ARISEN_VEGETATION_RENDER_VALIDATION_MODE\"] = $VegetationRenderValidationMode",
            relocatedValidation,
            StringComparison.Ordinal);
        Assert.Contains("-VegetationRenderValidationMode \"full\"", relocatedValidation, StringComparison.Ordinal);
        Assert.Contains("Mode = \"disabled\"", relocatedValidation, StringComparison.Ordinal);
        Assert.Contains("Mode = \"opaque-only\"", relocatedValidation, StringComparison.Ordinal);
        Assert.Contains("Assert-SourceIndependentRun", relocatedValidation, StringComparison.Ordinal);
        Assert.Contains("Assert-VegetationShutdown", relocatedValidation, StringComparison.Ordinal);
        Assert.Contains("validate_vegetation_rendering_visuals.ps1", relocatedValidation, StringComparison.Ordinal);
        Assert.Contains("vegetationVisualComparisonArtifacts", relocatedValidation, StringComparison.Ordinal);
        Assert.Contains("schemaVersion = 6", relocatedValidation, StringComparison.Ordinal);

        Assert.Contains("vegetationVisualComparison = [ordered]@{", profileResult, StringComparison.Ordinal);
        Assert.Contains("disabledSummaryPath", profileResult, StringComparison.Ordinal);
        Assert.Contains("opaqueOnlySummaryPath", profileResult, StringComparison.Ordinal);
        Assert.Contains("fullSummaryPath", profileResult, StringComparison.Ordinal);
        Assert.Contains("schemaVersion = 9", runtimeSummary, StringComparison.Ordinal);
        Assert.Contains("vegetationVisualComparisonRuns", runtimeSummary, StringComparison.Ordinal);
        Assert.Contains("vegetationVisualSummaryArtifactPaths", runtimeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void StabilityStressConsumesAndArchivesVersionedVegetationEvidence()
    {
        string stabilityValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_stability_stress.ps1");

        foreach (string contract in new[]
        {
            "[int]$summary.schemaVersion -eq 9",
            "[int]$summary.vegetationVisualComparisonRuns -eq 2",
            "[int]$summary.vegetationVisualSummaryArtifactCount -eq 3",
            "$vegetationSummaryPaths.Count -eq 3",
            "[int]$artifact.schemaVersion -eq 6",
            "$RuntimeSummary.vegetationVisualSummaryArtifactPaths =",
            "$profile.vegetationVisualComparison.$summaryField = $pathMap[$source]",
            "$profile.vegetationVisualComparison.disabledLogPath = Archive-RuntimeLog",
            "$profile.vegetationVisualComparison.opaqueOnlyLogPath = Archive-RuntimeLog",
            "$artifact.vegetationVisualComparisonArtifacts.disabledSummary =",
            "$artifact.vegetationVisualComparisonArtifacts.disabledDuringVisual =",
            "$artifact.vegetationVisualComparisonArtifacts.opaqueOnlySummary =",
            "$artifact.vegetationVisualComparisonArtifacts.opaqueOnlyDuringVisual =",
            "$artifact.vegetationVisualComparisonArtifacts.fullSummary = $world.summaryPath",
            "$artifact.vegetationVisualComparisonArtifacts.fullDuringVisual =",
            "vegetationVisualComparisons = $vegetationResults.ToArray()",
            "schemaVersion = 2"
        })
        {
            Assert.Contains(contract, stabilityValidation, StringComparison.Ordinal);
        }

        Assert.Contains(
            "Development vegetation comparison path is absent from aggregate evidence",
            stabilityValidation,
            StringComparison.Ordinal);
        Assert.Contains(
            "Relocated Production artifact has no vegetation visual comparison evidence",
            stabilityValidation,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeGatesScanTheExactLaunchOwnedPlayerLogForVulkanMessages()
    {
        string runtimeValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_runtime.bat");
        string relocatedValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_relocated_production.ps1");

        Assert.Contains("length = [long]$_.Length", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("lastWriteTimeUtcTicks", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("creationTimeUtcTicks", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains(
            "$snapshot = Get-Content -LiteralPath $env:CURRENT_PLAYER_LOG_SNAPSHOT_PATH -Raw | ConvertFrom-Json",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "$snapshot = @(Get-Content -LiteralPath $env:CURRENT_PLAYER_LOG_SNAPSHOT_PATH -Raw | ConvertFrom-Json)",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.Contains("$candidates.Count -ne 1", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("CURRENT_PLAYER_LOG_PATH", runtimeValidation, StringComparison.Ordinal);
        Assert.DoesNotContain("CURRENT_LOG_SCAN_STARTED_UTC", runtimeValidation, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CURRENT_TERRAIN_SUBMISSION_STARTED_UTC",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CURRENT_VEGETATION_SUBMISSION_STARTED_UTC",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.Equal(5, CountOccurrences(runtimeValidation, "call :validate_vulkan_player_log"));

        foreach (string marker in new[] { "vk message warning:", "vk message error:" })
        {
            Assert.Contains(marker, runtimeValidation, StringComparison.Ordinal);
            Assert.Contains(marker, relocatedValidation, StringComparison.Ordinal);
        }
        Assert.Contains(
            "[StringComparison]::OrdinalIgnoreCase",
            runtimeValidation,
            StringComparison.Ordinal);
        Assert.Contains(
            "$playerLogs.Count -ne 1",
            relocatedValidation,
            StringComparison.Ordinal);
        Assert.Contains("PlayerLogPath = $launchPlayerLogPath", relocatedValidation, StringComparison.Ordinal);
        Assert.Contains("PlayerLogText = $launchPlayerLogText", relocatedValidation, StringComparison.Ordinal);
    }

    [Fact]
    public void StabilityStressScansTheLaunchOwnedNativeTestPlayerLogForVulkanMessages()
    {
        string stabilityValidation = ReadRepoFile(
            "Arisen/Scripts/Windows/validate_stability_stress.ps1");

        int cleanLogsStart = stabilityValidation.IndexOf(
            "function Assert-CleanPlayerLogs",
            StringComparison.Ordinal);
        int cleanLogsEnd = stabilityValidation.IndexOf(
            "function Get-PlayerLogSnapshot",
            cleanLogsStart + 1,
            StringComparison.Ordinal);
        Assert.True(cleanLogsStart >= 0 && cleanLogsEnd > cleanLogsStart);
        string cleanLogsContract = stabilityValidation[cleanLogsStart..cleanLogsEnd];

        foreach (string marker in new[] { "vk message warning:", "vk message error:" })
        {
            Assert.Contains(marker, cleanLogsContract, StringComparison.Ordinal);
        }

        foreach (string metadataField in new[]
        {
            "length = [long]$log.Length",
            "lastWriteTimeUtcTicks",
            "creationTimeUtcTicks"
        })
        {
            Assert.Contains(metadataField, stabilityValidation, StringComparison.Ordinal);
        }

        Assert.Contains("Join-Path $testingOutput \"logs\"", stabilityValidation, StringComparison.Ordinal);
        Assert.Contains("-Filter \"player_*.log\"", stabilityValidation, StringComparison.Ordinal);
        Assert.Contains("$nativePackagePlayerLogs.Count -eq 1", stabilityValidation, StringComparison.Ordinal);

        int snapshotIndex = stabilityValidation.IndexOf(
            "$nativePackagePlayerLogSnapshot =",
            StringComparison.Ordinal);
        int invocationIndex = stabilityValidation.IndexOf(
            "Invoke-Checked (Join-Path $scriptRoot \"build_workspace.bat\")",
            snapshotIndex + 1,
            StringComparison.Ordinal);
        int resolutionIndex = stabilityValidation.IndexOf(
            "$nativePackagePlayerLogs = @(",
            invocationIndex + 1,
            StringComparison.Ordinal);
        int validationIndex = stabilityValidation.IndexOf(
            "Assert-CleanPlayerLogs $nativePackagePlayerLogs",
            resolutionIndex + 1,
            StringComparison.Ordinal);

        Assert.True(snapshotIndex >= 0, "Native-test player-log snapshot is missing.");
        Assert.True(invocationIndex > snapshotIndex, "Native-test logs must be snapshotted before launch.");
        Assert.True(resolutionIndex > invocationIndex, "Native-test player log must be resolved after launch.");
        Assert.True(validationIndex > resolutionIndex, "Launch-owned native-test player log must be scanned.");
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
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

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateRange(2, 8)]
    [int]$Cycles = 2,

    [switch]$SkipFast
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$engineRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot "..\.."))
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $engineRoot ".."))
$workspaceRoot = Join-Path $engineRoot "Development\PackageGame"
$logDirectory = Join-Path $workspaceRoot ".arisen\Logs"
$testingOutput = Join-Path $workspaceRoot ".arisen\bin\Testing\$Configuration"
$editorOutput = Join-Path $workspaceRoot ".arisen\bin\Editor\$Configuration"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryPath = Join-Path $logDirectory "validate-stability-stress-$Configuration-$timestamp.json"
$latestSummaryPath = Join-Path $logDirectory "validate-stability-stress-$Configuration-latest.json"
$cycleResults = [Collections.Generic.List[object]]::new()
$failure = $null
$fastValidationPassed = $null
$nativePackageTestsPassed = $false
$nativePackageVulkanLogPath = $null
$shutdownBaselineMarker =
    "[EngineKernel] Shutdown baseline: packages=0, contexts=0, nativeRuntimes=0, " +
    "services=0, initializedSubsystems=0, renderSurfaces=0, surfaceRegistryDisposed=True."

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

function Assert-Condition([bool]$Condition, [string]$Message)
{
    if (-not $Condition)
    {
        throw $Message
    }
}

function Invoke-Checked([string]$FilePath, [string[]]$ArgumentList)
{
    Write-Host "[Arisen] Running: $FilePath $($ArgumentList -join ' ')"
    & $FilePath @ArgumentList 2>&1 | ForEach-Object { Write-Host $_ }
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0)
    {
        throw "Command failed with exit code $exitCode`: $FilePath $($ArgumentList -join ' ')"
    }
}

function Assert-EmptyVulkanLog([string]$Path)
{
    Assert-Condition (Test-Path -LiteralPath $Path -PathType Leaf) `
        "Vulkan validation log was not produced: $Path"
    $item = Get-Item -LiteralPath $Path
    Assert-Condition ($item.Length -eq 0) `
        "Vulkan validation log is not empty: $Path ($($item.Length) bytes)"
}

function Read-AndValidateRuntimeSummary([string]$Path)
{
    Assert-Condition (Test-Path -LiteralPath $Path -PathType Leaf) `
        "Runtime validation summary was not produced: $Path"
    $summary = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    Assert-Condition ($summary.succeeded -eq $true -and [int]$summary.exitCode -eq 0) `
        "Runtime validation summary reports failure: $Path"
    Assert-Condition ($summary.gpuAvailable -eq $true) `
        "Stability stress requires real GPU coverage: $Path"
    Assert-Condition ([int]$summary.smokeSkips -eq 0) `
        "Runtime validation skipped GPU smoke work: $Path"
    Assert-Condition ([int]$summary.smokeRuns -eq 4) `
        "Runtime validation did not execute all four profile smokes: $Path"
    Assert-Condition ([int]$summary.editorViewportSmokeRuns -eq 1) `
        "Runtime validation did not execute the dual-viewport Editor smoke: $Path"
    Assert-Condition ([int]$summary.worldStreamingSmokeRuns -eq 3) `
        "Runtime validation did not execute all world-streaming gates: $Path"
    Assert-Condition ([int]$summary.terrainStreamingSmokeRuns -eq 3) `
        "Runtime validation did not execute all terrain-streaming gates: $Path"
    Assert-Condition ([int]$summary.relocatedProductionSmokeRuns -eq 1) `
        "Runtime validation did not execute copied Production validation: $Path"
    $profiles = @($summary.profileResults)
    Assert-Condition ($profiles.Count -eq 4) `
        "Runtime validation did not report exactly four profile results: $Path"
    Assert-Condition (@($profiles | Where-Object { $_.status -cne "passed" }).Count -eq 0) `
        "At least one runtime profile did not pass: $Path"
    return $summary
}

function Get-FreshPlayerLogs([DateTime]$StartedUtc)
{
    return @(
        Get-ChildItem -LiteralPath (Join-Path $workspaceRoot ".arisen\bin") `
            -Filter "player_*.log" -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTimeUtc -ge $StartedUtc.AddSeconds(-1) } |
            Sort-Object LastWriteTimeUtc)
}

function Assert-CleanPlayerLogs([IO.FileInfo[]]$Logs)
{
    Assert-Condition ($Logs.Count -gt 0) "Stress cycle produced no fresh player logs."
    $forbidden = "\[FATAL\]|Fatal error|SEHException|0xC0000005|VK_ERROR_DEVICE_LOST|Error unloading package"
    foreach ($log in $Logs)
    {
        $text = Get-Content -LiteralPath $log.FullName -Raw
        if ([Text.RegularExpressions.Regex]::IsMatch(
                $text,
                $forbidden,
                [Text.RegularExpressions.RegexOptions]::IgnoreCase))
        {
            throw "Fresh player log contains a crash/device-loss/unload marker: $($log.FullName)"
        }
    }
}

function Read-JsonArtifact([string]$Path, [string]$Description)
{
    Assert-Condition (Test-Path -LiteralPath $Path -PathType Leaf) `
        "$Description was not produced: $Path"
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-CycleArtifactPath([int]$Cycle, [string]$Name, [string]$Extension)
{
    $cycleLabel = $Cycle.ToString("D2")
    $safeName = [Text.RegularExpressions.Regex]::Replace($Name, "[^A-Za-z0-9._-]", "-")
    return Join-Path $logDirectory `
        "stability-stress-$Configuration-$timestamp-cycle-$cycleLabel-$safeName$Extension"
}

function Copy-CycleArtifact([string]$SourcePath, [int]$Cycle, [string]$Name)
{
    Assert-Condition (Test-Path -LiteralPath $SourcePath -PathType Leaf) `
        "Stress artifact was not produced: $SourcePath"
    $destination = Get-CycleArtifactPath $Cycle $Name ([IO.Path]::GetExtension($SourcePath))
    Copy-Item -LiteralPath $SourcePath -Destination $destination -Force
    return $destination
}

function Write-JsonArtifact([object]$Artifact, [string]$Path)
{
    $json = $Artifact | ConvertTo-Json -Depth 100
    [IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false))
}

function Assert-AllChecksPassed([object]$Checks, [string]$Description)
{
    Assert-Condition ($null -ne $Checks) "$Description has no checks object."
    $failed = @($Checks.PSObject.Properties | Where-Object { $_.Value -ne $true })
    $failedNames = @($failed | ForEach-Object { $_.Name })
    Assert-Condition ($failed.Count -eq 0) `
        "$Description failed checks: $($failedNames -join ', ')"
}

function Assert-WorldOwnershipBaseline([object]$Artifact, [string]$Path)
{
    Assert-Condition ($Artifact.passed -eq $true) `
        "World-streaming ownership artifact reports failure: $Path"
    Assert-Condition (
        [int]$Artifact.completedSoakCycles -eq [int]$Artifact.requestedSoakCycles -and
        [int]$Artifact.completedSoakCycles -ge 4) `
        "World-streaming ownership cycles are incomplete: $Path"
    Assert-AllChecksPassed $Artifact.checks "World-streaming ownership artifact '$Path'"
    Assert-Condition ($Artifact.checks.memoryBoundsStable -eq $true) `
        "World-streaming memory did not remain within its steady-state baseline: $Path"
    Assert-Condition ($Artifact.checks.shutdownDrained -eq $true) `
        "World-streaming shutdown did not return to baseline: $Path"

    return [ordered]@{
        profile = [string]$Artifact.profile
        completedSoakCycles = [int]$Artifact.completedSoakCycles
        memoryBoundsStable = $true
        shutdownDrained = $true
        peaks = $Artifact.peaks
    }
}

function Assert-TerrainOwnershipBaseline([object]$Artifact, [string]$Path)
{
    Assert-Condition ($Artifact.passed -eq $true) `
        "Terrain-streaming ownership artifact reports failure: $Path"
    Assert-Condition (
        [int]$Artifact.completedSoakCycles -eq [int]$Artifact.requestedSoakCycles -and
        [int]$Artifact.completedSoakCycles -ge 4) `
        "Terrain-streaming ownership cycles are incomplete: $Path"
    Assert-Condition (
        $Artifact.shutdownDrained -eq $true -and
        [string]$Artifact.terminalStage -ceq "ReadyForShutdown" -and
        $Artifact.lastDrain.isDrained -eq $true) `
        "Terrain-streaming shutdown did not reach its drained terminal state: $Path"

    $zeroFields = @(
        "visibleTileCount",
        "runtimeRootCount",
        "runtimeTileCount",
        "diagnosticRootCount",
        "diagnosticTileCount",
        "diagnosticResourceCount",
        "terrainResidencyResourceCount",
        "terrainPendingDisposalCount",
        "totalPendingDisposalCount",
        "outstandingTaskCount")
    foreach ($field in $zeroFields)
    {
        Assert-Condition ([long]$Artifact.lastDrain.$field -eq 0) `
            "Terrain-streaming drain field '$field' is not zero: $Path"
    }

    $generationValues = @(
        $Artifact.checkpoints |
            ForEach-Object { $_.tiles } |
            ForEach-Object { [long]$_.generation })
    Assert-Condition (
        $generationValues.Count -gt 0 -and
        @($generationValues | Where-Object { $_ -le 0 }).Count -eq 0) `
        "Terrain-streaming cooked generations are missing or invalid: $Path"

    return [ordered]@{
        profile = [string]$Artifact.profile
        completedSoakCycles = [int]$Artifact.completedSoakCycles
        terminalStage = [string]$Artifact.terminalStage
        shutdownDrained = $true
        zeroDrainFields = $zeroFields
        minimumCookedGeneration = ($generationValues | Measure-Object -Minimum).Minimum
        maximumCookedGeneration = ($generationValues | Measure-Object -Maximum).Maximum
        peaks = $Artifact.peaks
    }
}

function Assert-EditorOwnershipBaseline(
    [object]$Artifact,
    [string]$Path,
    [bool]$ExpectRenderDocRestart)
{
    Assert-Condition ($Artifact.passed -eq $true) `
        "Editor viewport ownership artifact reports failure: $Path"
    Assert-AllChecksPassed $Artifact.checks "Editor viewport ownership artifact '$Path'"
    foreach ($frameName in @("sceneFirstFrame", "sceneResizedFrame", "gameFirstFrame"))
    {
        $frame = $Artifact.$frameName
        Assert-Condition (
            $null -ne $frame -and
            [uint64]$frame.ticket -gt 0 -and
            [uint64]$frame.surfaceOwnershipGeneration -gt 0 -and
            $frame.consumptionReported -eq $true) `
            "Editor viewport frame '$frameName' has incomplete ticket or ownership state: $Path"
    }
    Assert-Condition (
        [uint32]$Artifact.sceneResizedFrame.resizeGeneration -gt
        [uint32]$Artifact.sceneFirstFrame.resizeGeneration) `
        "Editor viewport resize generation did not advance: $Path"
    Assert-Condition (
        $Artifact.checks.interopResourceCachesBounded -eq $true -and
        [int]$Artifact.maxSceneImportedImageCount -eq 3 -and
        [int]$Artifact.maxSceneImportedSemaphoreCount -eq 4 -and
        [int]$Artifact.maxGameImportedImageCount -eq 3 -and
        [int]$Artifact.maxGameImportedSemaphoreCount -eq 4) `
        "Editor viewport native interop resources did not remain bounded: $Path"
    if ($ExpectRenderDocRestart)
    {
        Assert-Condition (
            $Artifact.renderDocRestartCompleted -eq $true -and
            [uint64]$Artifact.graphicsGenerationAfterRestart -gt
            [uint64]$Artifact.graphicsGenerationBeforeRestart) `
            "Editor viewport graphics generation did not advance during RenderDoc restart: $Path"
    }

    $capturePath = [string]$Artifact.renderDocCapturePath
    if ([bool]$Artifact.renderDocCaptureExpected)
    {
        Assert-Condition (
            $Artifact.renderDocCaptureRequested -eq $true -and
            $Artifact.renderDocCaptureSucceeded -eq $true -and
            -not [string]::IsNullOrWhiteSpace($capturePath)) `
            "Editor viewport RenderDoc capture ownership is incomplete: $Path"
        Assert-Condition (Test-Path -LiteralPath $capturePath -PathType Leaf) `
            "Editor viewport RenderDoc capture artifact does not exist: $capturePath"
        Assert-Condition ((Get-Item -LiteralPath $capturePath).Length -gt 0) `
            "Editor viewport RenderDoc capture artifact is empty: $capturePath"
    }
    else
    {
        Assert-Condition ([string]::IsNullOrEmpty($capturePath)) `
            "Editor viewport artifact recorded an unexpected RenderDoc capture path: $Path"
    }

    return [ordered]@{
        renderDocRestartExpected = $ExpectRenderDocRestart
        graphicsGenerationBeforeRestart = [uint64]$Artifact.graphicsGenerationBeforeRestart
        graphicsGenerationAfterRestart = [uint64]$Artifact.graphicsGenerationAfterRestart
        sceneFirstTicket = [uint64]$Artifact.sceneFirstFrame.ticket
        sceneResizedTicket = [uint64]$Artifact.sceneResizedFrame.ticket
        gameFirstTicket = [uint64]$Artifact.gameFirstFrame.ticket
        sceneResizeGeneration = [uint32]$Artifact.sceneResizedFrame.resizeGeneration
        sceneSurfaceOwnershipGeneration = [uint64]$Artifact.sceneResizedFrame.surfaceOwnershipGeneration
        gameSurfaceOwnershipGeneration = [uint64]$Artifact.gameFirstFrame.surfaceOwnershipGeneration
        importedResourceCachesBounded = $true
        maxSceneImportedImageCount = [int]$Artifact.maxSceneImportedImageCount
        maxSceneImportedSemaphoreCount = [int]$Artifact.maxSceneImportedSemaphoreCount
        maxGameImportedImageCount = [int]$Artifact.maxGameImportedImageCount
        maxGameImportedSemaphoreCount = [int]$Artifact.maxGameImportedSemaphoreCount
        renderDocCapturePath = $capturePath
    }
}

function Archive-WorldStreamingArtifact(
    [string]$SourcePath,
    [int]$Cycle,
    [string]$Name)
{
    $artifact = Read-JsonArtifact $SourcePath "World-streaming summary"
    $baseline = Assert-WorldOwnershipBaseline $artifact $SourcePath
    $visualPaths = [Collections.Generic.List[string]]::new()
    foreach ($capture in @($artifact.visualCaptures))
    {
        $captureName = [string]$capture.capture.name
        $visualPath = Copy-CycleArtifact `
            ([string]$capture.capture.outputPath) `
            $Cycle `
            "$Name-visual-$captureName"
        $capture.capture.outputPath = $visualPath
        $visualPaths.Add($visualPath)
    }

    $summaryPath = Get-CycleArtifactPath $Cycle $Name ".json"
    Write-JsonArtifact $artifact $summaryPath
    return [ordered]@{
        sourcePath = $SourcePath
        summaryPath = $summaryPath
        visualPaths = $visualPaths.ToArray()
        baseline = $baseline
    }
}

function Archive-TerrainStreamingArtifact(
    [string]$SourcePath,
    [int]$Cycle,
    [string]$Name)
{
    $artifact = Read-JsonArtifact $SourcePath "Terrain-streaming summary"
    $baseline = Assert-TerrainOwnershipBaseline $artifact $SourcePath
    $visualPaths = [Collections.Generic.List[string]]::new()
    foreach ($capture in @($artifact.visualCaptures))
    {
        $captureName = [string]$capture.capture.name
        $visualPath = Copy-CycleArtifact `
            ([string]$capture.capture.outputPath) `
            $Cycle `
            "$Name-visual-$captureName"
        $capture.capture.outputPath = $visualPath
        $visualPaths.Add($visualPath)
    }

    $summaryPath = Get-CycleArtifactPath $Cycle $Name ".json"
    Write-JsonArtifact $artifact $summaryPath
    return [ordered]@{
        sourcePath = $SourcePath
        summaryPath = $summaryPath
        visualPaths = $visualPaths.ToArray()
        baseline = $baseline
    }
}

function Archive-EditorViewportArtifact(
    [string]$SourcePath,
    [int]$Cycle,
    [string]$Name,
    [bool]$ExpectRenderDocRestart)
{
    $artifact = Read-JsonArtifact $SourcePath "Editor viewport summary"
    $baseline = Assert-EditorOwnershipBaseline $artifact $SourcePath $ExpectRenderDocRestart
    $capturePath = $null
    if ([bool]$artifact.renderDocCaptureExpected)
    {
        $capturePath = Copy-CycleArtifact `
            ([string]$artifact.renderDocCapturePath) `
            $Cycle `
            "$Name-capture"
        $artifact.renderDocCapturePath = $capturePath
        $baseline.renderDocCapturePath = $capturePath
    }
    $summaryPath = Get-CycleArtifactPath $Cycle $Name ".json"
    Write-JsonArtifact $artifact $summaryPath
    return [ordered]@{
        sourcePath = $SourcePath
        summaryPath = $summaryPath
        capturePath = $capturePath
        baseline = $baseline
    }
}

function Archive-RuntimeLog([string]$SourcePath, [int]$Cycle, [string]$Name)
{
    Assert-Condition (Test-Path -LiteralPath $SourcePath -PathType Leaf) `
        "Runtime stress log was not produced: $SourcePath"
    $text = Get-Content -LiteralPath $SourcePath -Raw
    Assert-Condition ($text.Contains($shutdownBaselineMarker)) `
        "Runtime stress log has no zero-ownership shutdown baseline: $SourcePath"
    Assert-Condition ($text.Contains("[EngineKernel] Shutdown complete.")) `
        "Runtime stress log has no clean shutdown marker: $SourcePath"
    return Copy-CycleArtifact $SourcePath $Cycle $Name
}

function Archive-RelocatedProductionArtifact(
    [string]$SourcePath,
    [int]$Cycle)
{
    $artifact = Read-JsonArtifact $SourcePath "Relocated Production summary"
    Assert-Condition ($artifact.passed -eq $true) `
        "Relocated Production ownership artifact reports failure: $SourcePath"
    Assert-AllChecksPassed $artifact.checks "Relocated Production artifact '$SourcePath'"

    $world = Archive-WorldStreamingArtifact `
        ([string]$artifact.worldStreamingSummaryArtifact) `
        $Cycle `
        "relocated-production-world"
    $terrain = Archive-TerrainStreamingArtifact `
        ([string]$artifact.terrainStreamingSummaryArtifact) `
        $Cycle `
        "relocated-production-terrain"
    $logPath = Archive-RuntimeLog `
        ([string]$artifact.logPath) `
        $Cycle `
        "relocated-production"

    $artifact.worldStreamingSummaryArtifact = $world.summaryPath
    $artifact.worldStreamingVisualArtifacts = $world.visualPaths
    $artifact.terrainStreamingSummaryArtifact = $terrain.summaryPath
    $artifact.terrainStreamingVisualArtifacts = $terrain.visualPaths
    $artifact.logPath = $logPath
    $summaryPath = Get-CycleArtifactPath $Cycle "relocated-production" ".json"
    Write-JsonArtifact $artifact $summaryPath

    return [ordered]@{
        sourcePath = $SourcePath
        summaryPath = $summaryPath
        logPath = $logPath
        world = $world
        terrain = $terrain
        baseline = [ordered]@{
            sourceIndependent = $artifact.checks.metadataIsSourceIndependent -eq $true
            worldShutdownDrained = $world.baseline.shutdownDrained
            terrainShutdownDrained = $terrain.baseline.shutdownDrained
            vulkanValidationLogsEmpty = $artifact.checks.vulkanValidationLogsEmpty -eq $true
            tamperRejected = $artifact.checks.tamperRejected -eq $true
            missingArtifactRejected = $artifact.checks.missingArtifactRejected -eq $true
        }
    }
}

function Archive-RuntimeCycleEvidence(
    [object]$RuntimeSummary,
    [string]$RuntimeArchivePath,
    [int]$Cycle)
{
    $pathMap = @{}
    $worldResults = [Collections.Generic.List[object]]::new()
    $terrainResults = [Collections.Generic.List[object]]::new()
    $editorResults = [Collections.Generic.List[object]]::new()
    $sceneVisualPaths = [Collections.Generic.List[string]]::new()
    $runtimeLogPaths = [Collections.Generic.List[string]]::new()
    $vulkanLogPaths = [Collections.Generic.List[string]]::new()

    $index = 0
    foreach ($path in @($RuntimeSummary.worldStreamingSummaryArtifactPaths))
    {
        $index++
        $result = Archive-WorldStreamingArtifact ([string]$path) $Cycle "world-$index"
        $worldResults.Add($result)
        $pathMap[[IO.Path]::GetFullPath([string]$path)] = $result.summaryPath
    }

    $index = 0
    foreach ($path in @($RuntimeSummary.terrainStreamingSummaryArtifactPaths))
    {
        $index++
        $result = Archive-TerrainStreamingArtifact ([string]$path) $Cycle "terrain-$index"
        $terrainResults.Add($result)
        $pathMap[[IO.Path]::GetFullPath([string]$path)] = $result.summaryPath
    }

    $index = 0
    foreach ($path in @($RuntimeSummary.editorViewportSmokeArtifactPaths))
    {
        $index++
        $result = Archive-EditorViewportArtifact `
            ([string]$path) `
            $Cycle `
            "editor-viewport-$index" `
            $false
        $editorResults.Add($result)
        $pathMap[[IO.Path]::GetFullPath([string]$path)] = $result.summaryPath
    }

    $index = 0
    foreach ($path in @($RuntimeSummary.visualSummaryArtifactPaths))
    {
        $index++
        $artifact = Read-JsonArtifact ([string]$path) "Runtime visual summary"
        Assert-Condition ($artifact.passed -eq $true) `
            "Runtime visual summary reports failure: $path"
        $archivedPath = Copy-CycleArtifact ([string]$path) $Cycle "scene-visual-$index"
        $sceneVisualPaths.Add($archivedPath)
        $pathMap[[IO.Path]::GetFullPath([string]$path)] = $archivedPath
    }

    $relocatedPath = [string]@($RuntimeSummary.relocatedProductionSummaryArtifactPaths)[0]
    $relocated = Archive-RelocatedProductionArtifact $relocatedPath $Cycle
    $pathMap[[IO.Path]::GetFullPath($relocatedPath)] = $relocated.summaryPath

    foreach ($profile in @($RuntimeSummary.profileResults))
    {
        $profileName = [string]$profile.profile
        $profile.logPath = Archive-RuntimeLog `
            ([string]$profile.logPath) `
            $Cycle `
            "runtime-$profileName-scene"
        $runtimeLogPaths.Add([string]$profile.logPath)

        if ($profile.visualSummary.requested -eq $true)
        {
            $source = [IO.Path]::GetFullPath([string]$profile.visualSummary.path)
            $profile.visualSummary.path = $pathMap[$source]
        }
        if ($profile.worldStreaming.requested -eq $true)
        {
            $source = [IO.Path]::GetFullPath([string]$profile.worldStreaming.summaryPath)
            $profile.worldStreaming.summaryPath = $pathMap[$source]
            $profile.worldStreaming.logPath = Archive-RuntimeLog `
                ([string]$profile.worldStreaming.logPath) `
                $Cycle `
                "runtime-$profileName-world"
            $runtimeLogPaths.Add([string]$profile.worldStreaming.logPath)
        }
        if ($profile.terrainStreaming.requested -eq $true)
        {
            $source = [IO.Path]::GetFullPath([string]$profile.terrainStreaming.summaryPath)
            $profile.terrainStreaming.summaryPath = $pathMap[$source]
            $profile.terrainStreaming.logPath = Archive-RuntimeLog `
                ([string]$profile.terrainStreaming.logPath) `
                $Cycle `
                "runtime-$profileName-terrain"
            $runtimeLogPaths.Add([string]$profile.terrainStreaming.logPath)
        }
        if ($profile.editorViewportSmoke.requested -eq $true)
        {
            $source = [IO.Path]::GetFullPath([string]$profile.editorViewportSmoke.path)
            $profile.editorViewportSmoke.path = $pathMap[$source]
            $profile.editorViewportSmoke.logPath = Archive-RuntimeLog `
                ([string]$profile.editorViewportSmoke.logPath) `
                $Cycle `
                "runtime-$profileName-editor-viewport"
            $runtimeLogPaths.Add([string]$profile.editorViewportSmoke.logPath)
        }
        if ($profile.relocatedProduction.requested -eq $true)
        {
            $profile.relocatedProduction.summaryPath = $relocated.summaryPath
            $profile.relocatedProduction.logPath = $relocated.logPath
        }

        $validationLog = Join-Path $workspaceRoot `
            ".arisen\bin\$profileName\$Configuration\vk_validation.log"
        Assert-EmptyVulkanLog $validationLog
        $vulkanLogPaths.Add((Copy-CycleArtifact `
            $validationLog `
            $Cycle `
            "vulkan-$profileName"))
    }

    $RuntimeSummary.visualSummaryArtifactPaths = $sceneVisualPaths.ToArray()
    $RuntimeSummary.worldStreamingSummaryArtifactPaths = @(
        $worldResults | ForEach-Object { $_.summaryPath })
    $RuntimeSummary.terrainStreamingSummaryArtifactPaths = @(
        $terrainResults | ForEach-Object { $_.summaryPath })
    $RuntimeSummary.editorViewportSmokeArtifactPaths = @(
        $editorResults | ForEach-Object { $_.summaryPath })
    $RuntimeSummary.relocatedProductionSummaryArtifactPaths = @($relocated.summaryPath)
    $RuntimeSummary.relocatedProductionLogPaths = @($relocated.logPath)
    Write-JsonArtifact $RuntimeSummary $RuntimeArchivePath

    return [ordered]@{
        runtimeSummaryPath = $RuntimeArchivePath
        runtimeLogPaths = $runtimeLogPaths.ToArray()
        vulkanValidationLogPaths = $vulkanLogPaths.ToArray()
        sceneVisualPaths = $sceneVisualPaths.ToArray()
        world = $worldResults.ToArray()
        terrain = $terrainResults.ToArray()
        editor = $editorResults.ToArray()
        relocatedProduction = $relocated
        ownershipBaseline = [ordered]@{
            kernelShutdownLogCount = $runtimeLogPaths.Count + 1
            packageCount = 0
            managedLoadContextCount = 0
            nativeRuntimeCount = 0
            serviceCount = 0
            initializedSubsystemCount = 0
            renderSurfaceCount = 0
            worldShutdownDrained = @(
                $worldResults | Where-Object { -not $_.baseline.shutdownDrained }).Count -eq 0
            terrainShutdownDrained = @(
                $terrainResults | Where-Object { -not $_.baseline.shutdownDrained }).Count -eq 0
            terrainOutstandingTaskCount = 0
            editorFrameOwnershipObserved = $editorResults.Count -gt 0
            editorNativeResourceCachesBounded = @(
                $editorResults |
                    Where-Object { -not $_.baseline.importedResourceCachesBounded }).Count -eq 0
            copiedProductionPassed = $relocated.baseline.sourceIndependent -and
                $relocated.baseline.worldShutdownDrained -and
                $relocated.baseline.terrainShutdownDrained
            passed = $true
        }
    }
}

function Invoke-RenderDocCaptureSmoke([int]$Cycle, [DateTime]$CycleStartedUtc)
{
    $cycleLabel = $Cycle.ToString("D2")
    $executablePath = Join-Path $editorOutput "PackageGame.exe"
    $artifactPath = Join-Path $logDirectory "editor-viewport-summary-Editor-latest.json"
    $stdoutPath = Join-Path $logDirectory `
        "stability-stress-$Configuration-$timestamp-cycle-$cycleLabel-renderdoc.log"
    $stderrPath = "$stdoutPath.stderr"
    $validationLogPath = Join-Path $editorOutput "vk_validation.log"

    Assert-Condition (Test-Path -LiteralPath $executablePath -PathType Leaf) `
        "Editor executable is missing before RenderDoc stress: $executablePath"
    Remove-Item -LiteralPath $artifactPath, $stdoutPath, $stderrPath, $validationLogPath `
        -Force -ErrorAction SilentlyContinue

    $priorEnable = [Environment]::GetEnvironmentVariable(
        "ARISEN_ENABLE_RENDERDOC",
        [EnvironmentVariableTarget]::Process)
    $priorReplay = [Environment]::GetEnvironmentVariable(
        "ARISEN_RENDERDOC_OPEN_REPLAY",
        [EnvironmentVariableTarget]::Process)
    try
    {
        [Environment]::SetEnvironmentVariable(
            "ARISEN_ENABLE_RENDERDOC",
            $null,
            [EnvironmentVariableTarget]::Process)
        [Environment]::SetEnvironmentVariable(
            "ARISEN_RENDERDOC_OPEN_REPLAY",
            "0",
            [EnvironmentVariableTarget]::Process)

        $arguments =
            "--workspace `"$workspaceRoot`" --profile Editor " +
            "--editor-viewport-smoke --editor-viewport-smoke-timeout 120 " +
            "--editor-viewport-smoke-restart-renderdoc " +
            "--editor-viewport-smoke-capture-renderdoc"
        Write-Host "[Arisen] Running RenderDoc restart/capture stress cycle $Cycle/$Cycles."
        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $executablePath
        $startInfo.Arguments = $arguments
        $startInfo.WorkingDirectory = $editorOutput
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        $standardOutput = ""
        $standardError = ""
        $exitCode = $null
        $timedOut = $false
        try
        {
            Assert-Condition $process.Start() `
                "RenderDoc restart/capture smoke process could not be started."
            $standardOutputTask = $process.StandardOutput.ReadToEndAsync()
            $standardErrorTask = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit(180000))
            {
                $timedOut = $true
                try { $process.Kill() } catch { }
            }
            $process.WaitForExit()
            $standardOutput = $standardOutputTask.GetAwaiter().GetResult()
            $standardError = $standardErrorTask.GetAwaiter().GetResult()
            if (-not $timedOut)
            {
                $exitCode = $process.ExitCode
            }
        }
        finally
        {
            $process.Dispose()
        }

        $combinedOutput = $standardOutput
        if (-not [string]::IsNullOrEmpty($standardError))
        {
            if (-not [string]::IsNullOrEmpty($combinedOutput) -and
                -not $combinedOutput.EndsWith("`n", [StringComparison]::Ordinal))
            {
                $combinedOutput += [Environment]::NewLine
            }
            $combinedOutput += $standardError
        }
        [IO.File]::WriteAllText(
            $stdoutPath,
            $combinedOutput,
            [Text.UTF8Encoding]::new($false))
        if (-not [string]::IsNullOrWhiteSpace($combinedOutput))
        {
            Write-Host $combinedOutput
        }
        Assert-Condition (-not $timedOut) `
            "RenderDoc restart/capture smoke exceeded its 180 second process bound."
        Assert-Condition ($null -ne $exitCode -and $exitCode -eq 0) `
            "RenderDoc restart/capture smoke exited with code $exitCode."
    }
    finally
    {
        [Environment]::SetEnvironmentVariable(
            "ARISEN_ENABLE_RENDERDOC",
            $priorEnable,
            [EnvironmentVariableTarget]::Process)
        [Environment]::SetEnvironmentVariable(
            "ARISEN_RENDERDOC_OPEN_REPLAY",
            $priorReplay,
            [EnvironmentVariableTarget]::Process)
    }

    Invoke-Checked "powershell" @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $scriptRoot "validate_editor_viewport_summary.ps1"),
        "-ArtifactPath", $artifactPath,
        "-ExpectedProfile", "Editor",
        "-ExpectRenderDocRestart",
        "-ExpectRenderDocCapture")
    Assert-EmptyVulkanLog $validationLogPath
    $archivedValidationLogPath = Copy-CycleArtifact `
        $validationLogPath `
        $Cycle `
        "vulkan-editor-renderdoc"
    $archivedArtifact = Archive-EditorViewportArtifact `
        $artifactPath `
        $Cycle `
        "renderdoc" `
        $true

    $freshLogs = Get-FreshPlayerLogs $CycleStartedUtc
    Assert-CleanPlayerLogs $freshLogs
    $combinedLogText = ($freshLogs | ForEach-Object {
        Get-Content -LiteralPath $_.FullName -Raw
    }) -join "`n"
    Assert-Condition ($combinedLogText.Contains("[EditorViewportSmoke] RenderDoc capture")) `
        "RenderDoc stress produced no completed capture marker."
    Assert-Condition ($combinedOutput.Contains($shutdownBaselineMarker)) `
        "RenderDoc stress produced no zero-ownership shutdown baseline."
    Assert-Condition ($combinedOutput.Contains("[EngineKernel] Shutdown complete.")) `
        "RenderDoc stress produced no clean engine-shutdown marker."

    Assert-Condition (
        -not [string]::IsNullOrWhiteSpace([string]$archivedArtifact.capturePath)) `
        "RenderDoc stress summary did not own its capture artifact path."

    return [ordered]@{
        artifactPath = $archivedArtifact.summaryPath
        logPath = $stdoutPath
        capturePath = $archivedArtifact.capturePath
        vulkanValidationLogPath = $archivedValidationLogPath
        playerLogPaths = @($freshLogs.FullName)
        ownershipBaseline = $archivedArtifact.baseline
    }
}

try
{
    Push-Location $repositoryRoot
    try
    {
        Write-Host "[Arisen] Stability stress started. Configuration=$Configuration, Cycles=$Cycles"
        if (-not $SkipFast.IsPresent)
        {
            Invoke-Checked (Join-Path $scriptRoot "validate_fast.bat") @("--no-pause")
            $fastValidationPassed = $true
        }

        $priorFrameLimit = [Environment]::GetEnvironmentVariable(
            "ARISEN_NATIVE_TEST_FRAME_LIMIT",
            [EnvironmentVariableTarget]::Process)
        try
        {
            [Environment]::SetEnvironmentVariable(
                "ARISEN_NATIVE_TEST_FRAME_LIMIT",
                "1",
                [EnvironmentVariableTarget]::Process)
            Invoke-Checked (Join-Path $scriptRoot "build_workspace.bat") @(
                "--package", "com.arisen.rhi.vulkan.native",
                "--config", $Configuration,
                "--run-tests")
        }
        finally
        {
            [Environment]::SetEnvironmentVariable(
                "ARISEN_NATIVE_TEST_FRAME_LIMIT",
                $priorFrameLimit,
                [EnvironmentVariableTarget]::Process)
        }
        $nativePackageVulkanLog = Join-Path $testingOutput "vk_validation.log"
        Assert-EmptyVulkanLog $nativePackageVulkanLog
        $nativePackageVulkanLogPath = Join-Path $logDirectory `
            "stability-stress-$Configuration-$timestamp-native-tests-vulkan.log"
        Copy-Item `
            -LiteralPath $nativePackageVulkanLog `
            -Destination $nativePackageVulkanLogPath `
            -Force
        $nativePackageTestsPassed = $true

        for ($cycle = 1; $cycle -le $Cycles; $cycle++)
        {
            $cycleLabel = $cycle.ToString("D2")
            $cycleStartedUtc = [DateTime]::UtcNow
            Write-Host "[Arisen] Stability runtime cycle $cycle/$Cycles."
            Invoke-Checked (Join-Path $scriptRoot "validate_runtime.bat") @(
                "--no-pause",
                "--config", $Configuration,
                "--smoke-mode", "scene",
                "--frames", "1",
                "--require-gpu",
                "--skip-fast")

            $runtimeLatest = Join-Path $logDirectory "validate-runtime-$Configuration-latest.json"
            $runtimeArchive = Join-Path $logDirectory `
                "stability-stress-$Configuration-$timestamp-cycle-$cycleLabel-runtime.json"
            $runtimeSummary = Read-AndValidateRuntimeSummary $runtimeLatest
            $runtimeEvidence = Archive-RuntimeCycleEvidence `
                $runtimeSummary `
                $runtimeArchive `
                $cycle
            $renderDoc = Invoke-RenderDocCaptureSmoke $cycle $cycleStartedUtc
            $runtimeEvidence.ownershipBaseline["renderDocFrameOwnershipObserved"] =
                $renderDoc.ownershipBaseline.sceneFirstTicket -gt 0 -and
                $renderDoc.ownershipBaseline.gameFirstTicket -gt 0
            $runtimeEvidence.ownershipBaseline["renderDocGraphicsGenerationAdvanced"] =
                $renderDoc.ownershipBaseline.graphicsGenerationAfterRestart -gt
                $renderDoc.ownershipBaseline.graphicsGenerationBeforeRestart

            $cycleResults.Add([ordered]@{
                cycle = $cycle
                startedAtUtc = $cycleStartedUtc.ToString("o")
                completedAtUtc = [DateTime]::UtcNow.ToString("o")
                runtimeSummaryPath = $runtimeArchive
                runtime = $runtimeEvidence
                renderDoc = $renderDoc
                ownershipBaseline = $runtimeEvidence.ownershipBaseline
            })
        }
    }
    finally
    {
        Pop-Location
    }
}
catch
{
    $failure = [ordered]@{
        type = $_.Exception.GetType().FullName
        message = $_.Exception.Message
    }
    Write-Host "[ERROR] Stability stress failed: $($_.Exception.Message)"
}

$succeeded = $null -eq $failure -and $cycleResults.Count -eq $Cycles
$summary = [ordered]@{
    schemaVersion = 2
    capturedAtUtc = [DateTime]::UtcNow.ToString("o")
    repositoryRoot = $repositoryRoot
    workspacePath = $workspaceRoot
    configuration = $Configuration
    requestedCycles = $Cycles
    completedCycles = $cycleResults.Count
    fastValidationRequested = -not $SkipFast.IsPresent
    fastValidationPassed = $fastValidationPassed
    nativePackageTestsPassed = $nativePackageTestsPassed
    nativePackageVulkanLogPath = $nativePackageVulkanLogPath
    managedOwnershipBaselines = [ordered]@{
        verifiedByFastValidation = $fastValidationPassed -eq $true
        kernelBootAndPackageMountCycles = 16
        assetImportWorkerCycles = 8
        modelReimportCycles = 16
    }
    gpuRequired = $true
    succeeded = $succeeded
    exitCode = if ($succeeded) { 0 } else { 1 }
    failure = $failure
    cycles = $cycleResults
}
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
Copy-Item -LiteralPath $summaryPath -Destination $latestSummaryPath -Force
Write-Host "[Arisen] Stability stress summary: $summaryPath"

if ($succeeded)
{
    Write-Host "[Arisen] Stability stress completed successfully."
    exit 0
}

exit 1

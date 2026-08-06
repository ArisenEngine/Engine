$ErrorActionPreference = "Stop"

$profiles = @()
if (Test-Path -LiteralPath $env:PROFILE_RESULTS_JSONL) {
    $profiles = @(
        Get-Content -LiteralPath $env:PROFILE_RESULTS_JSONL |
            Where-Object { $_.Trim().Length -gt 0 } |
            ForEach-Object { $_ | ConvertFrom-Json })
}

$visualSummaryArtifactPaths = @(
    $profiles |
        Where-Object { $_.visualSummary.requested -eq $true -and $_.visualSummary.passed -eq $true -and $_.visualSummary.path } |
        ForEach-Object { [string]$_.visualSummary.path })
$worldStreamingSummaryArtifactPaths = @(
    $profiles |
        Where-Object { $_.worldStreaming.requested -eq $true -and $_.worldStreaming.passed -eq $true -and $_.worldStreaming.summaryPath } |
        ForEach-Object { [string]$_.worldStreaming.summaryPath })
$terrainStreamingSummaryArtifactPaths = @(
    $profiles |
        Where-Object { $_.terrainStreaming.requested -eq $true -and $_.terrainStreaming.passed -eq $true -and $_.terrainStreaming.summaryPath } |
        ForEach-Object { [string]$_.terrainStreaming.summaryPath })
$vegetationVisualSummaryArtifactPaths = @(
    $profiles |
        Where-Object {
            $_.vegetationVisualComparison.requested -eq $true -and
            $_.vegetationVisualComparison.passed -eq $true
        } |
        ForEach-Object {
            @(
                [string]$_.vegetationVisualComparison.disabledSummaryPath,
                [string]$_.vegetationVisualComparison.opaqueOnlySummaryPath,
                [string]$_.vegetationVisualComparison.fullSummaryPath
            )
        } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$editorViewportSmokeArtifactPaths = @(
    $profiles |
        Where-Object { $_.editorViewportSmoke.requested -eq $true -and $_.editorViewportSmoke.passed -eq $true -and $_.editorViewportSmoke.path } |
        ForEach-Object { [string]$_.editorViewportSmoke.path })
$relocatedProductionSummaryArtifactPaths = @(
    $profiles |
        Where-Object { $_.relocatedProduction.requested -eq $true -and $_.relocatedProduction.passed -eq $true -and $_.relocatedProduction.summaryPath } |
        ForEach-Object { [string]$_.relocatedProduction.summaryPath })
$relocatedProductionLogPaths = @(
    $profiles |
        Where-Object { $_.relocatedProduction.requested -eq $true -and $_.relocatedProduction.logPath } |
        ForEach-Object { [string]$_.relocatedProduction.logPath })

$failure = $null
if ($env:EXIT_CODE -ne "0") {
    $failure = [ordered]@{
        stage = if ($env:FAILURE_STAGE -ne "") { $env:FAILURE_STAGE } else { $null }
        profile = if ($env:FAILED_PROFILE -ne "") { $env:FAILED_PROFILE } else { $null }
        message = if ($env:FAILURE_MESSAGE -ne "") { $env:FAILURE_MESSAGE } else { "Runtime validation failed" }
    }
}

$summary = [ordered]@{
    schemaVersion = 8
    capturedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    repositoryRoot = $env:REPO_ROOT
    workspacePath = $env:WORKSPACE_DIR
    configuration = $env:CONFIG
    requestedProfiles = @($env:PROFILES -split " " | Where-Object { $_ -ne "" })
    smokeMode = $env:SMOKE_MODE
    smokeFrames = [int]$env:FRAMES
    gpuSmokePolicy = $env:GPU_SMOKE
    gpuAvailable = $env:GPU_AVAILABLE -eq "1"
    gpuProbeReason = $env:GPU_PROBE_REASON
    succeeded = $env:EXIT_CODE -eq "0"
    exitCode = [int]$env:EXIT_CODE
    smokeRuns = [int]$env:SMOKE_RUNS
    smokeSkips = [int]$env:SMOKE_SKIPS
    cpuFallbackRuns = [int]$env:CPU_FALLBACK_RUNS
    visualSummaryArtifactCount = $visualSummaryArtifactPaths.Count
    visualSummaryArtifactPaths = $visualSummaryArtifactPaths
    worldStreamingSmokeRuns = [int]$env:WORLD_STREAMING_SMOKE_RUNS
    worldStreamingSummaryArtifactCount = $worldStreamingSummaryArtifactPaths.Count
    worldStreamingSummaryArtifactPaths = $worldStreamingSummaryArtifactPaths
    terrainStreamingSmokeRuns = [int]$env:TERRAIN_STREAMING_SMOKE_RUNS
    terrainStreamingSummaryArtifactCount = $terrainStreamingSummaryArtifactPaths.Count
    terrainStreamingSummaryArtifactPaths = $terrainStreamingSummaryArtifactPaths
    vegetationVisualComparisonRuns = [int]$env:VEGETATION_VISUAL_COMPARISON_RUNS
    vegetationVisualSummaryArtifactCount = $vegetationVisualSummaryArtifactPaths.Count
    vegetationVisualSummaryArtifactPaths = $vegetationVisualSummaryArtifactPaths
    editorViewportSmokeRuns = [int]$env:EDITOR_VIEWPORT_SMOKE_RUNS
    editorViewportSmokeArtifactCount = $editorViewportSmokeArtifactPaths.Count
    editorViewportSmokeArtifactPaths = $editorViewportSmokeArtifactPaths
    relocatedProductionSmokeRuns = [int]$env:RELOCATED_PRODUCTION_SMOKE_RUNS
    relocatedProductionSummaryArtifactCount = $relocatedProductionSummaryArtifactPaths.Count
    relocatedProductionSummaryArtifactPaths = $relocatedProductionSummaryArtifactPaths
    relocatedProductionLogPaths = $relocatedProductionLogPaths
    failure = $failure
    profileResults = $profiles
}

$json = $summary | ConvertTo-Json -Depth 10
Set-Content -LiteralPath $env:SUMMARY_TIMESTAMP_PATH -Value $json -Encoding UTF8
Copy-Item -LiteralPath $env:SUMMARY_TIMESTAMP_PATH -Destination $env:SUMMARY_PATH -Force

param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedProfile,

    [int]$RequiredResizeTransitions = 4,
    [int]$RequiredConcurrentFrames = 320,
    [switch]$ExpectRenderDoc,
    [switch]$ExpectRenderDocRestart
)

$ErrorActionPreference = "Stop"

if ($ExpectRenderDoc.IsPresent -and $ExpectRenderDocRestart.IsPresent)
{
    throw "Process-start RenderDoc and in-process RenderDoc restart expectations are mutually exclusive."
}

function Fail-Validation([string]$Message)
{
    Write-Host "[ERROR] $Message"
    exit 1
}

if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Leaf))
{
    Fail-Validation "Editor viewport smoke artifact was not produced: $ArtifactPath"
}

try
{
    $artifact = Get-Content -LiteralPath $ArtifactPath -Raw | ConvertFrom-Json
}
catch
{
    Fail-Validation "Editor viewport smoke artifact is not valid JSON: $($_.Exception.Message)"
}

if ([int]$artifact.schemaVersion -ne 6)
{
    Fail-Validation "Editor viewport smoke schema mismatch. Expected 6, received $($artifact.schemaVersion)."
}

$expectedRenderDocRestart = $ExpectRenderDocRestart.IsPresent
if ([bool]$artifact.renderDocRestartExpected -ne $expectedRenderDocRestart -or
    [bool]$artifact.renderDocRestartRequested -ne $expectedRenderDocRestart -or
    [bool]$artifact.renderDocRestartCompleted -ne $expectedRenderDocRestart -or
    [bool]$artifact.renderDocAvailableAfterRestart -ne $expectedRenderDocRestart -or
    $artifact.checks.renderDocRestartExpectationMet -ne $true)
{
    Fail-Validation (
        "Editor viewport smoke RenderDoc restart state did not match the requested mode. " +
        "Expected=$expectedRenderDocRestart, " +
        "Requested=$($artifact.renderDocRestartRequested), " +
        "Completed=$($artifact.renderDocRestartCompleted), " +
        "Available=$($artifact.renderDocAvailableAfterRestart).")
}

if ($expectedRenderDocRestart)
{
    if ([uint64]$artifact.graphicsGenerationBeforeRestart -eq 0 -or
        [uint64]$artifact.graphicsGenerationAfterRestart -le
        [uint64]$artifact.graphicsGenerationBeforeRestart)
    {
        Fail-Validation (
            "Editor viewport smoke did not advance the graphics generation during RenderDoc activation. " +
            "Previous=$($artifact.graphicsGenerationBeforeRestart), " +
            "Current=$($artifact.graphicsGenerationAfterRestart).")
    }

    if ([int]$artifact.postRestartConcurrentSceneFrameCount -lt $RequiredConcurrentFrames -or
        [int]$artifact.postRestartConcurrentGameFrameCount -lt $RequiredConcurrentFrames -or
        $artifact.checks.postRestartSceneFramesPresented -ne $true -or
        $artifact.checks.postRestartGameFramesPresented -ne $true)
    {
        Fail-Validation (
            "Editor viewport smoke did not sustain presentation after the RenderDoc restart. " +
            "Scene=$($artifact.postRestartConcurrentSceneFrameCount), " +
            "Game=$($artifact.postRestartConcurrentGameFrameCount).")
    }
}
elseif ([int]$artifact.postRestartConcurrentSceneFrameCount -ne 0 -or
        [int]$artifact.postRestartConcurrentGameFrameCount -ne 0)
{
    Fail-Validation "Ordinary Editor viewport smoke unexpectedly recorded post-restart frames."
}

if ([string]$artifact.profile -cne $ExpectedProfile)
{
    Fail-Validation "Editor viewport smoke profile mismatch. Expected $ExpectedProfile, received $($artifact.profile)."
}

if ($artifact.passed -ne $true -or $artifact.checks.passed -ne $true)
{
    Fail-Validation "Editor viewport smoke checks did not pass: $ArtifactPath"
}

$expectedRenderDocAtStartup = $ExpectRenderDoc.IsPresent
if ($artifact.renderDocAvailabilityObserved -ne $true -or
    [bool]$artifact.renderDocExpectedAtStartup -ne $expectedRenderDocAtStartup -or
    [bool]$artifact.renderDocAvailableAtStartup -ne $expectedRenderDocAtStartup -or
    $artifact.checks.renderDocStartupExpectationMet -ne $true)
{
    Fail-Validation (
        "Editor viewport smoke RenderDoc startup state did not match the requested mode. " +
        "Expected=$expectedRenderDocAtStartup, " +
        "ArtifactExpected=$($artifact.renderDocExpectedAtStartup), " +
        "Available=$($artifact.renderDocAvailableAtStartup).")
}

if ($artifact.checks.interopResourceCachesBounded -ne $true -or
    [int]$artifact.maxSceneImportedImageCount -ne 3 -or
    [int]$artifact.maxSceneImportedSemaphoreCount -ne 4 -or
    [int]$artifact.maxGameImportedImageCount -ne 3 -or
    [int]$artifact.maxGameImportedSemaphoreCount -ne 4)
{
    Fail-Validation (
        "Editor viewport smoke did not keep imported resources bounded per viewport. " +
        "Scene=$($artifact.maxSceneImportedImageCount)/$($artifact.maxSceneImportedSemaphoreCount), " +
        "Game=$($artifact.maxGameImportedImageCount)/$($artifact.maxGameImportedSemaphoreCount).")
}

if ($null -eq $artifact.sceneFirstFrame -or
    $null -eq $artifact.sceneResizedFrame -or
    $null -eq $artifact.gameFirstFrame)
{
    Fail-Validation "Editor viewport smoke is missing a required SceneView or GameView observation."
}

if ([int]$artifact.sceneResizeRequestCount -ne $RequiredResizeTransitions -or
    [int]$artifact.sceneResizeTransitionCount -ne $RequiredResizeTransitions -or
    $artifact.checks.sceneResizeStressPassed -ne $true)
{
    Fail-Validation (
        "Editor viewport smoke did not complete the observable resize sequence. " +
        "Requests=$($artifact.sceneResizeRequestCount), " +
        "Transitions=$($artifact.sceneResizeTransitionCount), " +
        "Required=$RequiredResizeTransitions.")
}

if ([uint32]$artifact.sceneResizedFrame.resizeGeneration -le
    [uint32]$artifact.sceneFirstFrame.resizeGeneration)
{
    Fail-Validation "Editor viewport smoke did not advance the SceneView resize generation."
}

if ($artifact.sceneFirstFrame.consumptionReported -ne $true -or
    $artifact.sceneResizedFrame.consumptionReported -ne $true -or
    $artifact.gameFirstFrame.consumptionReported -ne $true)
{
    Fail-Validation "Editor viewport smoke did not report all required frames as consumed."
}

if ([double]$artifact.sceneFirstFrame.presentationScaleY -ne -1.0 -or
    [double]$artifact.sceneResizedFrame.presentationScaleY -ne -1.0 -or
    [double]$artifact.gameFirstFrame.presentationScaleY -ne -1.0)
{
    Fail-Validation "Editor viewport smoke compositor Y-flip policy is incorrect."
}

if ([int]$artifact.concurrentSceneFrameCount -lt $RequiredConcurrentFrames -or
    [int]$artifact.concurrentGameFrameCount -lt $RequiredConcurrentFrames)
{
    Fail-Validation (
        "Editor viewport smoke did not sustain concurrent presentation. " +
        "Scene=$($artifact.concurrentSceneFrameCount), Game=$($artifact.concurrentGameFrameCount).")
}

if ($artifact.terrainPaintAvailable -ne $true -or $artifact.terrainPaintActivated -ne $true)
{
    Fail-Validation "Editor viewport smoke did not activate the real Terrain Brush Paint mode."
}

if ($null -eq $artifact.worldPartition -or
    [int]$artifact.worldPartition.cellCount -le 0 -or
    $artifact.worldPartition.loadRequested -ne $true -or
    $artifact.worldPartition.activeObserved -ne $true -or
    $artifact.worldPartition.unloadRequested -ne $true -or
    $artifact.worldPartition.unloadedObserved -ne $true)
{
    Fail-Validation "Editor viewport smoke did not prove first-open world visibility and explicit cell load/unload."
}

if ([int]$artifact.worldPartition.cellX -ne 0 -or
    [int]$artifact.worldPartition.cellY -ne 0 -or
    [int]$artifact.worldPartition.cellZ -ne 0)
{
    Fail-Validation (
        "Editor viewport smoke selected cell " +
        "($($artifact.worldPartition.cellX),$($artifact.worldPartition.cellY),$($artifact.worldPartition.cellZ)) " +
        "instead of (0,0,0).")
}

Write-Host (
    "[Arisen] Editor viewport smoke passed: " +
    "Scene=$($artifact.sceneFirstFrame.width)x$($artifact.sceneFirstFrame.height), " +
    "Resized=$($artifact.sceneResizedFrame.width)x$($artifact.sceneResizedFrame.height), " +
    "ResizeTransitions=$($artifact.sceneResizeTransitionCount), " +
    "Game=$($artifact.gameFirstFrame.width)x$($artifact.gameFirstFrame.height), " +
    "ConcurrentFrames=$($artifact.concurrentSceneFrameCount)/$($artifact.concurrentGameFrameCount), " +
    "PostRestartFrames=$($artifact.postRestartConcurrentSceneFrameCount)/$($artifact.postRestartConcurrentGameFrameCount), " +
    "InteropCaches=$($artifact.maxSceneImportedImageCount)/$($artifact.maxSceneImportedSemaphoreCount), " +
    "RenderDocStartup=$($artifact.renderDocAvailableAtStartup), " +
    "RenderDocRestart=$($artifact.renderDocRestartCompleted), " +
    "Cell=(0,0,0), output=$ArtifactPath")

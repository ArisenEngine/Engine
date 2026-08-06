param(
    [Parameter(Mandatory = $true)]
    [string]$DisabledSummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$OpaqueOnlySummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$FullSummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedProfile
)

$ErrorActionPreference = "Stop"

$canonicalCellId = "5d13eda6-606a-57a0-bae4-cd559ddad464"
$minimumOpaqueAverageLuminanceDelta = 0.0005
$minimumOpaqueSpatialLuminanceDelta = 0.005
$minimumOpaqueAverageDepthDelta = 0.00002
$minimumOpaqueSpatialDepthDelta = 0.0002
$minimumOpaqueWrittenDepthPixelDelta = 128L
$minimumShadowAverageLuminanceDelta = 0.00001
$minimumShadowSpatialLuminanceDelta = 0.0001

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Test-Finite {
    param([double]$Value)

    return -not [double]::IsNaN($Value) -and -not [double]::IsInfinity($Value)
}

function Get-MaximumAbsoluteDelta {
    param(
        [object[]]$Left,
        [object[]]$Right,
        [string]$Context
    )

    Assert-Condition ($Left.Count -eq $Right.Count -and $Left.Count -gt 0) `
        "$Context metric arrays do not have the same nonzero length."
    $maximum = 0.0
    for ($index = 0; $index -lt $Left.Count; $index++) {
        $delta = [Math]::Abs([double]$Left[$index] - [double]$Right[$index])
        Assert-Condition (Test-Finite $delta) `
            "$Context produced a non-finite metric delta."
        $maximum = [Math]::Max($maximum, $delta)
    }

    return $maximum
}

function Get-MaximumPositiveDelta {
    param(
        [object[]]$Lighter,
        [object[]]$Darker,
        [string]$Context
    )

    Assert-Condition ($Lighter.Count -eq $Darker.Count -and $Lighter.Count -gt 0) `
        "$Context metric arrays do not have the same nonzero length."
    $maximum = 0.0
    for ($index = 0; $index -lt $Lighter.Count; $index++) {
        $delta = [double]$Lighter[$index] - [double]$Darker[$index]
        Assert-Condition (Test-Finite $delta) `
            "$Context produced a non-finite metric delta."
        $maximum = [Math]::Max($maximum, $delta)
    }

    return $maximum
}

function Read-DuringCapture {
    param(
        [string]$Mode,
        [string]$SummaryPath
    )

    $summaryFullPath = [System.IO.Path]::GetFullPath($SummaryPath)
    Assert-Condition (Test-Path -LiteralPath $summaryFullPath -PathType Leaf) `
        "Vegetation $Mode world-streaming summary was not produced: $summaryFullPath"
    $summary = Get-Content -LiteralPath $summaryFullPath -Raw | ConvertFrom-Json
    Assert-Condition ([int]$summary.schemaVersion -eq 1) `
        "Vegetation $Mode world-streaming summary schema mismatch."
    Assert-Condition ([string]$summary.mode -ceq "world-streaming") `
        "Vegetation $Mode summary mode mismatch."
    Assert-Condition ([string]$summary.profile -ceq $ExpectedProfile) `
        "Vegetation $Mode profile mismatch. Expected '$ExpectedProfile', received '$($summary.profile)'."
    Assert-Condition ($summary.passed -eq $true) `
        "Vegetation $Mode world-streaming scenario failed: $($summary.failure)"

    $checkpointMatches = @($summary.checkpoints | Where-Object {
        [string]$_.name -ceq "during"
    })
    Assert-Condition ($checkpointMatches.Count -eq 1) `
        "Vegetation $Mode summary must contain exactly one 'during' checkpoint."
    $checkpoint = $checkpointMatches[0]
    $activeCellIds = @($checkpoint.activeCellIds | ForEach-Object { [string]$_ })
    Assert-Condition (
        $checkpoint.passed -eq $true -and
        $activeCellIds.Count -eq 1 -and
        $activeCellIds[0] -ieq $canonicalCellId) `
        "Vegetation $Mode 'during' checkpoint does not own the exact canonical center cell."
    Assert-Condition (
        [int]$checkpoint.expectedEntityCount -eq [int]$checkpoint.actualEntityCount -and
        ($checkpoint.expectedComponents | ConvertTo-Json -Compress) -ceq
            ($checkpoint.actualComponents | ConvertTo-Json -Compress)) `
        "Vegetation $Mode 'during' checkpoint has stale or missing ECS content."

    $captureMatches = @($summary.visualCaptures | Where-Object {
        [string]$_.capture.name -ceq "during"
    })
    Assert-Condition ($captureMatches.Count -eq 1) `
        "Vegetation $Mode summary must contain exactly one 'during' visual capture."
    $capture = $captureMatches[0]
    Assert-Condition ([string]$capture.state -ceq "Succeeded") `
        "Vegetation $Mode 'during' visual capture did not succeed."
    Assert-Condition (
        [long]$capture.capture.frameIndex -eq ([long]$checkpoint.frameIndex + 1L)) `
        "Vegetation $Mode 'during' capture did not observe the frame after its committed checkpoint."

    $visualPath = [System.IO.Path]::GetFullPath([string]$capture.capture.outputPath)
    Assert-Condition (Test-Path -LiteralPath $visualPath -PathType Leaf) `
        "Vegetation $Mode 'during' visual artifact was not produced: $visualPath"
    $visual = Get-Content -LiteralPath $visualPath -Raw | ConvertFrom-Json
    Assert-Condition ([int]$visual.schemaVersion -eq 2) `
        "Vegetation $Mode visual schema mismatch."
    Assert-Condition ([string]$visual.profile -ceq $ExpectedProfile) `
        "Vegetation $Mode visual profile mismatch."
    Assert-Condition ($visual.passed -eq $true -and $visual.checks.passed -eq $true) `
        "Vegetation $Mode visual color checks failed."
    Assert-Condition ($visual.depth.passed -eq $true -and $visual.depth.checks.passed -eq $true) `
        "Vegetation $Mode visual depth checks failed."
    Assert-Condition (
        [long]$visual.nonBlankPixelCount -gt 0 -and
        [long]$visual.depth.writtenDepthPixelCount -gt 0 -and
        [string]$visual.pixelSha256 -match '^[0-9A-Fa-f]{64}$' -and
        [string]$visual.depth.pixelSha256 -match '^[0-9A-Fa-f]{64}$') `
        "Vegetation $Mode visual is blank or lacks valid color/depth hashes."
    Assert-Condition (
        (Test-Finite ([double]$visual.averageLuminance)) -and
        (Test-Finite ([double]$visual.depth.averageDepth))) `
        "Vegetation $Mode visual has non-finite aggregate metrics."

    $stateSignature = [ordered]@{
        activeCellIds = $activeCellIds
        expectedEntityCount = [int]$checkpoint.expectedEntityCount
        actualEntityCount = [int]$checkpoint.actualEntityCount
        expectedComponents = $checkpoint.expectedComponents
        actualComponents = $checkpoint.actualComponents
        origin = $checkpoint.origin.origin
        primarySource = $checkpoint.origin.primarySource
        hasPrimarySource = [bool]$checkpoint.origin.hasPrimarySource
        rebaseSequence = [long]$checkpoint.origin.rebaseSequence
    } | ConvertTo-Json -Depth 8 -Compress
    $renderSignature = [ordered]@{
        outputKind = [string]$visual.outputKind
        surfaceId = [string]$visual.surfaceId
        width = [int]$visual.width
        height = [int]$visual.height
        format = [string]$visual.format
        channelOrder = [string]$visual.channelOrder
        colorSpace = [string]$visual.colorSpace
        spatialGridWidth = [int]$visual.spatialGridWidth
        spatialGridHeight = [int]$visual.spatialGridHeight
        depthWidth = [int]$visual.depth.width
        depthHeight = [int]$visual.depth.height
        depthFormat = [string]$visual.depth.format
        depthSpatialGridWidth = [int]$visual.depth.spatialGridWidth
        depthSpatialGridHeight = [int]$visual.depth.spatialGridHeight
    } | ConvertTo-Json -Compress

    return [pscustomobject]@{
        Mode = $Mode
        SummaryPath = $summaryFullPath
        VisualPath = $visualPath
        StateSignature = $stateSignature
        RenderSignature = $renderSignature
        Visual = $visual
    }
}

try {
    $disabled = Read-DuringCapture -Mode "disabled" -SummaryPath $DisabledSummaryPath
    $opaqueOnly = Read-DuringCapture -Mode "opaque-only" -SummaryPath $OpaqueOnlySummaryPath
    $full = Read-DuringCapture -Mode "full" -SummaryPath $FullSummaryPath

    foreach ($candidate in @($opaqueOnly, $full)) {
        Assert-Condition ($candidate.StateSignature -ceq $disabled.StateSignature) `
            "Vegetation $($candidate.Mode) did not capture the same canonical 'during' world state as disabled."
        Assert-Condition ($candidate.RenderSignature -ceq $disabled.RenderSignature) `
            "Vegetation $($candidate.Mode) did not capture the same surface/camera output contract as disabled."
    }

    $disabledVisual = $disabled.Visual
    $opaqueVisual = $opaqueOnly.Visual
    $fullVisual = $full.Visual
    foreach ($enabledCapture in @($opaqueOnly, $full)) {
        $enabled = $enabledCapture.Visual
        $opaqueAverageLuminanceDelta = [Math]::Abs(
            [double]$enabled.averageLuminance - [double]$disabledVisual.averageLuminance)
        $opaqueSpatialLuminanceDelta = Get-MaximumAbsoluteDelta `
            @($enabled.spatialLuminanceGrid) `
            @($disabledVisual.spatialLuminanceGrid) `
            "Vegetation opaque color"
        $opaqueAverageDepthDelta = [Math]::Abs(
            [double]$enabled.depth.averageDepth - [double]$disabledVisual.depth.averageDepth)
        $opaqueSpatialDepthDelta = Get-MaximumAbsoluteDelta `
            @($enabled.depth.spatialDepthGrid) `
            @($disabledVisual.depth.spatialDepthGrid) `
            "Vegetation opaque depth"
        $opaqueWrittenDepthPixelDelta = [Math]::Abs(
            [long]$enabled.depth.writtenDepthPixelCount -
            [long]$disabledVisual.depth.writtenDepthPixelCount)

        Assert-Condition (
            [string]$enabled.pixelSha256 -cne [string]$disabledVisual.pixelSha256 -and
            $opaqueAverageLuminanceDelta -ge $minimumOpaqueAverageLuminanceDelta -and
            $opaqueSpatialLuminanceDelta -ge $minimumOpaqueSpatialLuminanceDelta) `
            "Vegetation $($enabledCapture.Mode) rendering did not produce meaningful opaque color coverage."
        Assert-Condition (
            [string]$enabled.depth.pixelSha256 -cne [string]$disabledVisual.depth.pixelSha256 -and
            $opaqueAverageDepthDelta -ge $minimumOpaqueAverageDepthDelta -and
            $opaqueSpatialDepthDelta -ge $minimumOpaqueSpatialDepthDelta -and
            $opaqueWrittenDepthPixelDelta -ge $minimumOpaqueWrittenDepthPixelDelta) `
            "Vegetation $($enabledCapture.Mode) rendering did not produce meaningful opaque depth coverage."
    }

    $opaqueDepthMetrics = $opaqueVisual.depth | ConvertTo-Json -Depth 8 -Compress
    $fullDepthMetrics = $fullVisual.depth | ConvertTo-Json -Depth 8 -Compress
    Assert-Condition (
        [string]$opaqueVisual.depth.pixelSha256 -ceq [string]$fullVisual.depth.pixelSha256 -and
        $opaqueDepthMetrics -ceq $fullDepthMetrics) `
        "Vegetation shadow-only validation changed frame-depth output."

    $shadowAverageLuminanceDelta =
        [double]$opaqueVisual.averageLuminance - [double]$fullVisual.averageLuminance
    $shadowSpatialLuminanceDelta = Get-MaximumPositiveDelta `
        @($opaqueVisual.spatialLuminanceGrid) `
        @($fullVisual.spatialLuminanceGrid) `
        "Vegetation shadow color"
    Assert-Condition (
        [string]$opaqueVisual.pixelSha256 -cne [string]$fullVisual.pixelSha256 -and
        $shadowAverageLuminanceDelta -ge $minimumShadowAverageLuminanceDelta -and
        $shadowSpatialLuminanceDelta -ge $minimumShadowSpatialLuminanceDelta) `
        "Full vegetation rendering did not produce a meaningful darker shadow contribution than opaque-only."

    Write-Host ((
        "[Arisen] Vegetation rendering visuals passed: profile={0}, " +
        "opaqueLuminanceDelta={1:R}, opaqueDepthDelta={2:R}, " +
        "shadowLuminanceDelta={3:R}, disabled={4}, opaqueOnly={5}, full={6}") -f
        $ExpectedProfile,
        [Math]::Abs([double]$opaqueVisual.averageLuminance - [double]$disabledVisual.averageLuminance),
        [Math]::Abs([double]$opaqueVisual.depth.averageDepth - [double]$disabledVisual.depth.averageDepth),
        $shadowAverageLuminanceDelta,
        $disabled.VisualPath,
        $opaqueOnly.VisualPath,
        $full.VisualPath)
    exit 0
}
catch {
    Write-Host "[ERROR] Vegetation rendering visual validation failed: $($_.Exception.Message)"
    exit 1
}

param(
    [Parameter(Mandatory = $true)]
    [string]$SummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedProfile,

    [ValidateRange(0, 7)]
    [int]$ExpectedVisualCaptureCount = 7
)

$ErrorActionPreference = "Stop"

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

try {
    $path = [System.IO.Path]::GetFullPath($SummaryPath)
    Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) `
        "World-streaming summary was not produced: $path"
    $artifact = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    Assert-Condition ([int]$artifact.schemaVersion -eq 1) `
        "World-streaming summary schema mismatch."
    Assert-Condition ([string]$artifact.mode -ceq "world-streaming") `
        "World-streaming summary mode mismatch."
    Assert-Condition ([string]$artifact.profile -ceq $ExpectedProfile) `
        "World-streaming profile mismatch. Expected '$ExpectedProfile', received '$($artifact.profile)'."
    Assert-Condition ($artifact.passed -eq $true) `
        "World-streaming scenario failed: $($artifact.failure)"
    Assert-Condition ([int]$artifact.completedSoakCycles -eq [int]$artifact.requestedSoakCycles) `
        "World-streaming soak did not complete every requested cycle."
    Assert-Condition ([int]$artifact.completedSoakCycles -ge 4) `
        "World-streaming soak completed fewer than four cycles."

    $requiredStates = @("Queued", "Active", "Cancelled", "Unloaded", "Failed")
    $observedStates = @($artifact.observedStates | ForEach-Object { [string]$_ })
    foreach ($requiredState in $requiredStates) {
        Assert-Condition ($observedStates -ccontains $requiredState) `
            "World-streaming summary did not observe state '$requiredState'."
    }

    $failedChecks = @($artifact.checks.PSObject.Properties | Where-Object { $_.Value -ne $true })
    Assert-Condition ($failedChecks.Count -eq 0) `
        "World-streaming checks failed: $($failedChecks.Name -join ', ')"

    $checkpointNames = @($artifact.checkpoints | ForEach-Object { [string]$_.name })
    foreach ($requiredCheckpoint in @("before", "during", "unloaded", "after")) {
        Assert-Condition ($checkpointNames -ccontains $requiredCheckpoint) `
            "World-streaming summary is missing checkpoint '$requiredCheckpoint'."
    }
    foreach ($checkpoint in @($artifact.checkpoints)) {
        Assert-Condition ($checkpoint.passed -eq $true) `
            "World-streaming checkpoint '$($checkpoint.name)' failed."
        Assert-Condition ([int]$checkpoint.expectedEntityCount -eq [int]$checkpoint.actualEntityCount) `
            "World-streaming checkpoint '$($checkpoint.name)' has stale or missing entities."
        Assert-Condition (
            ($checkpoint.expectedComponents | ConvertTo-Json -Compress) -ceq
            ($checkpoint.actualComponents | ConvertTo-Json -Compress)) `
            "World-streaming checkpoint '$($checkpoint.name)' component counts differ."
    }

    $diagnostics = @($artifact.diagnostics | ForEach-Object { [string]$_ })
    Assert-Condition ($diagnostics.Count -ge 1) `
        "World-streaming summary is missing its deterministic failure diagnostic."
    Assert-Condition ($diagnostics[0].StartsWith("Cell requires ", [StringComparison]::Ordinal)) `
        "World-streaming failure diagnostic is not stable."

    $captures = @($artifact.visualCaptures)
    Assert-Condition ($captures.Count -eq $ExpectedVisualCaptureCount) `
        "Expected $ExpectedVisualCaptureCount visual captures, received $($captures.Count)."
    if ($ExpectedVisualCaptureCount -gt 0) {
        $expectedNames = if ($ExpectedVisualCaptureCount -eq 7) {
            @(
                "before",
                "during",
                "shadow-near",
                "shadow-mid",
                "shadow-far",
                "shadow-far-stable",
                "after"
            )
        }
        elseif ($ExpectedVisualCaptureCount -eq 3) {
            @("before", "during", "after")
        }
        else {
            throw "Visual validation supports either 3 legacy captures or 7 shadow-path captures."
        }
        $visualsByName = @{}
        $previousFrame = -1L
        foreach ($expectedName in $expectedNames) {
            $capture = @($captures | Where-Object { [string]$_.capture.name -ceq $expectedName })
            Assert-Condition ($capture.Count -eq 1) `
                "Expected one visual capture named '$expectedName'."
            $capture = $capture[0]
            Assert-Condition ([string]$capture.state -ceq "Succeeded") `
                "Visual capture '$expectedName' did not succeed."
            Assert-Condition ([long]$capture.capture.frameIndex -gt $previousFrame) `
                "Visual capture frames are not strictly increasing."
            $previousFrame = [long]$capture.capture.frameIndex

            $visualPath = [System.IO.Path]::GetFullPath([string]$capture.capture.outputPath)
            Assert-Condition (Test-Path -LiteralPath $visualPath -PathType Leaf) `
                "Visual capture '$expectedName' was not produced: $visualPath"
            $visual = Get-Content -LiteralPath $visualPath -Raw | ConvertFrom-Json
            Assert-Condition ([int]$visual.schemaVersion -eq 2) `
                "Visual capture '$expectedName' schema mismatch."
            Assert-Condition ($visual.passed -eq $true -and $visual.checks.passed -eq $true) `
                "Visual capture '$expectedName' color checks failed."
            Assert-Condition ($visual.depth.passed -eq $true -and $visual.depth.checks.passed -eq $true) `
                "Visual capture '$expectedName' depth checks failed."
            Assert-Condition ([long]$visual.nonBlankPixelCount -gt 0) `
                "Visual capture '$expectedName' is blank."
            Assert-Condition ([long]$visual.depth.writtenDepthPixelCount -gt 0) `
                "Visual capture '$expectedName' contains no written depth."
            Assert-Condition ([string]$visual.pixelSha256 -match '^[0-9A-Fa-f]{64}$') `
                "Visual capture '$expectedName' has no valid color pixel hash."
            Assert-Condition ([string]$visual.depth.pixelSha256 -match '^[0-9A-Fa-f]{64}$') `
                "Visual capture '$expectedName' has no valid depth pixel hash."
            $visualsByName[$expectedName] = $visual
        }

        if ($ExpectedVisualCaptureCount -eq 7) {
            $distanceNames = @("shadow-near", "shadow-mid", "shadow-far")
            $distanceColorHashes = @($distanceNames | ForEach-Object {
                [string]$visualsByName[$_].pixelSha256
            })
            $distanceDepthHashes = @($distanceNames | ForEach-Object {
                [string]$visualsByName[$_].depth.pixelSha256
            })
            Assert-Condition (@($distanceColorHashes | Select-Object -Unique).Count -eq 3) `
                "Near/mid/far shadow captures did not produce three distinct color views."
            Assert-Condition (@($distanceDepthHashes | Select-Object -Unique).Count -eq 3) `
                "Near/mid/far shadow captures did not produce three distinct depth views."

            $farCapture = @($captures | Where-Object {
                [string]$_.capture.name -ceq "shadow-far"
            })[0]
            $stableCapture = @($captures | Where-Object {
                [string]$_.capture.name -ceq "shadow-far-stable"
            })[0]
            Assert-Condition (
                [long]$stableCapture.capture.frameIndex -eq
                ([long]$farCapture.capture.frameIndex + 1)) `
                "Far shadow stability captures are not consecutive frames."
            Assert-Condition (
                [string]$visualsByName["shadow-far"].pixelSha256 -ceq
                [string]$visualsByName["shadow-far-stable"].pixelSha256) `
                "Stationary far shadow frames changed color output."
            Assert-Condition (
                [string]$visualsByName["shadow-far"].depth.pixelSha256 -ceq
                [string]$visualsByName["shadow-far-stable"].depth.pixelSha256) `
                "Stationary far shadow frames changed depth output."
        }
    }

    $successTemplate =
        "[Arisen] World-streaming summary passed: profile={0}, frames={1}, soak={2}, " +
        "states={3}, visuals={4}, output={5}"
    $successMessage = $successTemplate -f
        $ExpectedProfile,
        (@($artifact.checkpoints | Measure-Object frameIndex -Maximum).Maximum + 1),
        $artifact.completedSoakCycles,
        $observedStates.Count,
        $captures.Count,
        $path
    Write-Host $successMessage
    exit 0
}
catch {
    Write-Host "[ERROR] World-streaming summary validation failed: $($_.Exception.Message)"
    exit 1
}

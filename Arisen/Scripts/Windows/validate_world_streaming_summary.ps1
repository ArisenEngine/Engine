param(
    [Parameter(Mandatory = $true)]
    [string]$SummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedProfile,

    [ValidateRange(0, 3)]
    [int]$ExpectedVisualCaptureCount = 3
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
        $expectedNames = @("before", "during", "after")
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

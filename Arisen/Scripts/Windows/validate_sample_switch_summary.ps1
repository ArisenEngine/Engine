param(
    [Parameter(Mandatory = $true)]
    [string]$SummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedProfile
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
        "Sample-switch summary was not produced: $path"
    $artifact = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    Assert-Condition ([int]$artifact.schemaVersion -eq 1) `
        "Sample-switch summary schema mismatch."
    Assert-Condition ([string]$artifact.mode -ceq "sample-switch") `
        "Sample-switch summary mode mismatch."
    Assert-Condition ([string]$artifact.profile -ceq $ExpectedProfile) `
        "Sample-switch profile mismatch. Expected '$ExpectedProfile', received '$($artifact.profile)'."
    Assert-Condition ($artifact.passed -eq $true) `
        "Sample-switch scenario failed: $($artifact.failure)"

    $steps = @($artifact.steps)
    Assert-Condition ($steps.Count -ge 1) `
        "Sample-switch summary must record at least one switched sample."
    foreach ($step in $steps) {
        Assert-Condition (-not [string]::IsNullOrWhiteSpace([string]$step.sampleId)) `
            "Sample-switch step is missing its sample id."
        Assert-Condition (-not [string]::IsNullOrWhiteSpace([string]$step.worldGuid)) `
            "Sample-switch step '$($step.sampleId)' is missing its world guid."
        Assert-Condition ($null -ne $step.activatedFrame) `
            "Sample-switch step '$($step.sampleId)' never activated its world."
    }

    # The last switched world has to be the world still active when the run ended, and at least one
    # step has to have replaced earlier content: the gate proves selection in a running process, not
    # a boot that merely happened to land on the same world.
    $lastWorld = [string]$steps[-1].worldGuid
    Assert-Condition ([string]$artifact.finalActiveWorldGuid -ceq $lastWorld) `
        "Sample-switch final active world '$($artifact.finalActiveWorldGuid)' is not the last switched world '$lastWorld'."
    $superseded = @($steps | Where-Object { -not [bool]$_.alreadyActive })
    Assert-Condition ($superseded.Count -ge 1) `
        "Sample-switch summary recorded no step that replaced earlier content."

    $successTemplate =
        "[Arisen] Sample-switch summary passed: profile={0}, samples={1}, frames={2}, output={3}"
    $successMessage = $successTemplate -f
        $ExpectedProfile,
        $steps.Count,
        $artifact.frames,
        $path
    Write-Host $successMessage
    exit 0
}
catch {
    Write-Host "[ERROR] Sample-switch summary validation failed: $($_.Exception.Message)"
    exit 1
}

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
        "Physics-settle summary was not produced: $path"
    $artifact = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    Assert-Condition ([int]$artifact.schemaVersion -eq 1) `
        "Physics-settle summary schema mismatch."
    Assert-Condition ([string]$artifact.mode -ceq "physics-settle") `
        "Physics-settle summary mode mismatch."
    Assert-Condition ([string]$artifact.profile -ceq $ExpectedProfile) `
        "Physics-settle profile mismatch. Expected '$ExpectedProfile', received '$($artifact.profile)'."
    Assert-Condition ($artifact.passed -eq $true) `
        "Physics-settle scenario failed: $($artifact.failure)"

    $bodies = @($artifact.bodies)
    Assert-Condition ($bodies.Count -ge 1) `
        "Physics-settle summary must observe at least one dynamic body."
    foreach ($body in $bodies) {
        Assert-Condition ($body.settled -eq $true) `
            "Physics-settle body did not settle: startY=$($body.startY), settledY=$($body.settledY)."
        Assert-Condition ([double]$body.startY - [double]$body.settledY -ge 0.5) `
            "Physics-settle body never fell from its authored position: startY=$($body.startY), settledY=$($body.settledY)."
    }

    # A scene character must be standing on the floor by the time the gate completes: the same
    # component-to-solver-to-transform path the bodies use, proven for the character too.
    $characters = @($artifact.characters)
    Assert-Condition ($characters.Count -ge 1) `
        "Physics-settle summary must observe at least one authored character."
    foreach ($character in $characters) {
        Assert-Condition ($character.grounded -eq $true) `
            "Physics-settle character is not grounded (feetY=$($character.feetY))."
        Assert-Condition ([Math]::Abs([double]$character.feetY) -le 0.1) `
            "Physics-settle character is not standing on the floor: feetY=$($character.feetY)."
    }

    # The solver world must be gone once the process shut down; a live world here is a leaked
    # simulation, not a gating curiosity.
    Assert-Condition ([int]$artifact.livePhysicsWorldsAfterShutdown -eq 0) `
        "Physics-settle shutdown left $($artifact.livePhysicsWorldsAfterShutdown) physics world(s) alive."

    $successTemplate =
        "[Arisen] Physics-settle summary passed: profile={0}, bodies={1}, characters={2}, frames={3}, output={4}"
    $successMessage = $successTemplate -f
        $ExpectedProfile,
        $bodies.Count,
        $characters.Count,
        $artifact.frames,
        $path
    Write-Host $successMessage
    exit 0
}
catch {
    Write-Host "[ERROR] Physics-settle summary validation failed: $($_.Exception.Message)"
    exit 1
}

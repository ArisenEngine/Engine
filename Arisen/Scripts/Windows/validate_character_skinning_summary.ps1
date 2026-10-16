param(
    [Parameter(Mandatory = $true)]
    [string]$SummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$SecondSummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$ThirdSummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$PlayerLogPath,

    [Parameter(Mandatory = $true)]
    [string]$GateSummaryPath,

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

function Read-VisualSummary {
    param([string]$Path, [string]$Label)

    $full = [System.IO.Path]::GetFullPath($Path)
    Assert-Condition (Test-Path -LiteralPath $full -PathType Leaf) `
        "Character-skinning $Label visual summary was not produced: $full"
    try {
        $artifact = Get-Content -LiteralPath $full -Raw | ConvertFrom-Json
    }
    catch {
        throw "Character-skinning $Label visual summary is not valid JSON: $($_.Exception.Message)"
    }

    Assert-Condition ([int]$artifact.schemaVersion -eq 2) `
        "Character-skinning $Label visual summary schema mismatch. Expected 2, received $($artifact.schemaVersion)."
    Assert-Condition ([string]$artifact.profile -ceq $ExpectedProfile) `
        "Character-skinning $Label visual summary profile mismatch. Expected '$ExpectedProfile', received '$($artifact.profile)'."
    Assert-Condition ($artifact.passed -eq $true -and $artifact.checks.passed -eq $true) `
        "Character-skinning $Label visual summary color checks did not pass: $full"
    $depth = $artifact.depth
    Assert-Condition ($null -ne $depth) `
        "Character-skinning $Label visual summary is missing its depth result."
    Assert-Condition ($depth.passed -eq $true -and $depth.checks.passed -eq $true) `
        "Character-skinning $Label visual summary depth checks did not pass: $full"
    Assert-Condition ([uint32]$depth.width -eq [uint32]$artifact.width -and [uint32]$depth.height -eq [uint32]$artifact.height) `
        "Character-skinning $Label depth dimensions do not match the color dimensions."
    Assert-Condition ([string]$depth.format -ceq "FORMAT_D32_SFLOAT") `
        "Character-skinning $Label depth format mismatch. Expected FORMAT_D32_SFLOAT, received '$($depth.format)'."
    Assert-Condition ([long]$depth.writtenDepthPixelCount -gt 0) `
        "Character-skinning $Label depth buffer wrote no pixels."

    return $artifact
}

try {
    $first = Read-VisualSummary -Path $SummaryPath -Label "first"
    $second = Read-VisualSummary -Path $SecondSummaryPath -Label "second"
    $third = Read-VisualSummary -Path $ThirdSummaryPath -Label "third"

    # Playback's observable proof from the captures: three runs end at different clip times, so a
    # live pose has to paint at least two distinct frames. A parked driver repaints the rest pose and
    # collapses the hashes.
    $hashes = @(
        [string]$first.pixelSha256,
        [string]$second.pixelSha256,
        [string]$third.pixelSha256)
    $distinctHashes = @($hashes | Sort-Object -Unique)
    Assert-Condition ($distinctHashes.Count -ge 2) `
        "Character-skinning captures are pixel-identical across three clip times; the pose is not advancing."

    $logFull = [System.IO.Path]::GetFullPath($PlayerLogPath)
    Assert-Condition (Test-Path -LiteralPath $logFull -PathType Leaf) `
        "Character-skinning player log was not resolved: $logFull"
    $log = Get-Content -LiteralPath $logFull -Raw
    Assert-Condition ($log.Contains("Bound the sample character")) `
        "Character-skinning player log never bound the sample character."
    Assert-Condition ($log.Contains("Clip 'Clip_0' | Elapsed")) `
        "Character-skinning player log never reported a sampled clip time."
    Assert-Condition (-not $log.Contains("playback is parked")) `
        "Character-skinning player log reports parked playback; the sample must play its clip."
    Assert-Condition ($log.Contains("Prepared skinned pipeline batch")) `
        "Character-skinning player log never prepared the SKINNED pipeline batch."
    Assert-Condition ($log.Contains(".kw-SKINNED.")) `
        "Character-skinning player log never loaded the cooked SKINNED shader variant."

    $gate = [ordered]@{
        schemaVersion = 1
        gate = "character-skinning"
        profile = $ExpectedProfile
        summaryPath = [System.IO.Path]::GetFullPath($SummaryPath)
        secondSummaryPath = [System.IO.Path]::GetFullPath($SecondSummaryPath)
        thirdSummaryPath = [System.IO.Path]::GetFullPath($ThirdSummaryPath)
        playerLogPath = $logFull
        pixelSha256 = $hashes
        distinctPixelSha256Count = $distinctHashes.Count
        checks = [ordered]@{
            visualSummariesPassed = $true
            animatedFramesDiffer = $true
            driverBound = $true
            clipSampled = $true
            playbackNotParked = $true
            skinnedPipelinePrepared = $true
            skinnedVariantLoaded = $true
        }
        passed = $true
    }
    $gateFull = [System.IO.Path]::GetFullPath($GateSummaryPath)
    $gateJson = $gate | ConvertTo-Json -Depth 8
    Set-Content -LiteralPath $gateFull -Value $gateJson -Encoding UTF8

    Write-Host ("[Arisen] Character-skinning gate passed: profile={0}, distinctCaptures={1}/{2}, output={3}" -f
        $ExpectedProfile, $distinctHashes.Count, $hashes.Count, $gateFull)
    exit 0
}
catch {
    Write-Host ("[ERROR] {0}" -f $_.Exception.Message)
    exit 1
}

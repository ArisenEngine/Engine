param(
    [Parameter(Mandatory = $true)]
    [string]$SummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$LogDirectory,

    [ValidateRange(1, 4)]
    [int]$ExpectedCascadeCount = 4,

    [string]$StartedUtc = ""
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

function Get-DrawCounts {
    param(
        [System.Text.RegularExpressions.Match]$Match,
        [int]$FirstGroup
    )

    return @(
        [int]$Match.Groups[$FirstGroup].Value,
        [int]$Match.Groups[$FirstGroup + 1].Value,
        [int]$Match.Groups[$FirstGroup + 2].Value,
        [int]$Match.Groups[$FirstGroup + 3].Value
    )
}

try {
    $summaryFullPath = [System.IO.Path]::GetFullPath($SummaryPath)
    $logDirectoryFullPath = [System.IO.Path]::GetFullPath($LogDirectory)
    Assert-Condition (Test-Path -LiteralPath $summaryFullPath -PathType Leaf) `
        "World-streaming summary was not produced: $summaryFullPath"
    Assert-Condition (Test-Path -LiteralPath $logDirectoryFullPath -PathType Container) `
        "Runtime log directory was not produced: $logDirectoryFullPath"

    $summary = Get-Content -LiteralPath $summaryFullPath -Raw | ConvertFrom-Json
    $requiredCaptureNames = @(
        "shadow-near",
        "shadow-mid",
        "shadow-far",
        "shadow-far-stable"
    )
    $captureFrames = @{}
    foreach ($name in $requiredCaptureNames) {
        $capture = @($summary.visualCaptures | Where-Object {
            [string]$_.capture.name -ceq $name
        })
        Assert-Condition ($capture.Count -eq 1) `
            "Expected one visual capture named '$name'."
        Assert-Condition ([string]$capture[0].state -ceq "Succeeded") `
            "Visual capture '$name' did not succeed."
        $captureFrames[$name] = [uint32]$capture[0].capture.frameIndex
    }

    $logs = @(Get-ChildItem -LiteralPath $logDirectoryFullPath -Filter "player_*.log" -File)
    if (-not [string]::IsNullOrWhiteSpace($StartedUtc)) {
        $started = [DateTime]::Parse(
            $StartedUtc,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
        $logs = @($logs | Where-Object { $_.LastWriteTimeUtc -ge $started.AddSeconds(-1) })
    }
    $playerLog = $logs |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    Assert-Condition ($null -ne $playerLog) `
        "No fresh player log was found for cascaded-shadow validation."
    $logText = Get-Content -LiteralPath $playerLog.FullName -Raw

    $genericPattern = [regex]::new(
        '\[GenericRP\.ShadowValidation\] Frame=(\d+) Cascades=(\d+) ' +
        'MeshDraws=(\d+),(\d+),(\d+),(\d+) Dropped=(\d+) ' +
        'SplitFar=([-+0-9.Ee]+),([-+0-9.Ee]+),([-+0-9.Ee]+),([-+0-9.Ee]+)',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    $terrainPattern = [regex]::new(
        '\[Terrain\.GenericRP\.ShadowValidation\] Frame=(\d+) Cascades=(\d+) ' +
        'TerrainDraws=(\d+),(\d+),(\d+),(\d+) Dropped=(\d+)',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)

    $genericByFrame = @{}
    foreach ($match in $genericPattern.Matches($logText)) {
        $splits = @()
        for ($group = 8; $group -le 11; $group++) {
            $splits += [double]::Parse(
                $match.Groups[$group].Value,
                [Globalization.CultureInfo]::InvariantCulture)
        }
        $genericByFrame[[uint32]$match.Groups[1].Value] = [pscustomobject]@{
            CascadeCount = [int]$match.Groups[2].Value
            Draws = Get-DrawCounts -Match $match -FirstGroup 3
            Dropped = [int]$match.Groups[7].Value
            Splits = $splits
        }
    }

    $terrainByFrame = @{}
    foreach ($match in $terrainPattern.Matches($logText)) {
        $terrainByFrame[[uint32]$match.Groups[1].Value] = [pscustomobject]@{
            CascadeCount = [int]$match.Groups[2].Value
            Draws = Get-DrawCounts -Match $match -FirstGroup 3
            Dropped = [int]$match.Groups[7].Value
        }
    }

    $meshCoveredCascades = [bool[]]::new($ExpectedCascadeCount)
    $terrainCoveredCascades = [bool[]]::new($ExpectedCascadeCount)
    $recordsByName = @{}
    foreach ($name in $requiredCaptureNames) {
        $frame = [uint32]$captureFrames[$name]
        Assert-Condition ($genericByFrame.ContainsKey($frame)) `
            "Capture '$name' frame $frame has no Generic RP cascade diagnostic."
        Assert-Condition ($terrainByFrame.ContainsKey($frame)) `
            "Capture '$name' frame $frame has no terrain cascade diagnostic."
        $generic = $genericByFrame[$frame]
        $terrain = $terrainByFrame[$frame]
        Assert-Condition ($generic.CascadeCount -eq $ExpectedCascadeCount) `
            "Capture '$name' used $($generic.CascadeCount) Generic RP cascades; expected $ExpectedCascadeCount."
        Assert-Condition ($terrain.CascadeCount -eq $ExpectedCascadeCount) `
            "Capture '$name' used $($terrain.CascadeCount) terrain cascades; expected $ExpectedCascadeCount."
        Assert-Condition ($generic.Dropped -eq 0 -and $terrain.Dropped -eq 0) `
            "Capture '$name' dropped bounded shadow commands."
        Assert-Condition (($generic.Draws | Measure-Object -Sum).Sum -gt 0) `
            "Capture '$name' rendered no static-mesh shadow commands."
        Assert-Condition (($terrain.Draws | Measure-Object -Sum).Sum -gt 0) `
            "Capture '$name' rendered no terrain shadow commands."

        for ($cascade = 0; $cascade -lt $ExpectedCascadeCount; $cascade++) {
            if ($cascade -gt 0) {
                Assert-Condition ($generic.Splits[$cascade] -gt $generic.Splits[$cascade - 1]) `
                    "Capture '$name' has non-increasing cascade splits."
            }
            if ($generic.Draws[$cascade] -gt 0) {
                $meshCoveredCascades[$cascade] = $true
            }
            if ($terrain.Draws[$cascade] -gt 0) {
                $terrainCoveredCascades[$cascade] = $true
            }
        }

        $recordsByName[$name] = [pscustomobject]@{
            Generic = $generic
            Terrain = $terrain
        }
    }

    for ($cascade = 0; $cascade -lt $ExpectedCascadeCount; $cascade++) {
        Assert-Condition ($meshCoveredCascades[$cascade]) `
            "Cascade layer $cascade received no static-mesh draw across near/mid/far captures."
        Assert-Condition ($terrainCoveredCascades[$cascade]) `
            "Cascade layer $cascade received no terrain draw across near/mid/far captures."
    }

    $far = $recordsByName["shadow-far"]
    $stable = $recordsByName["shadow-far-stable"]
    Assert-Condition (
        (($far.Generic.Draws -join ',') -ceq ($stable.Generic.Draws -join ',')) -and
        (($far.Terrain.Draws -join ',') -ceq ($stable.Terrain.Draws -join ',')) -and
        (($far.Generic.Splits -join ',') -ceq ($stable.Generic.Splits -join ','))) `
        "Stationary far frames changed cascade splits or draw ranges."

    Write-Host (
        "[Arisen] Cascaded-shadow visuals passed: cascades={0}, captures={1}, log={2}" -f
        $ExpectedCascadeCount,
        $requiredCaptureNames.Count,
        $playerLog.FullName)
    exit 0
}
catch {
    Write-Host "[ERROR] Cascaded-shadow visual validation failed: $($_.Exception.Message)"
    exit 1
}

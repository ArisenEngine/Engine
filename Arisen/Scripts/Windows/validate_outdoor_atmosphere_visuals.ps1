param(
    [Parameter(Mandatory = $true)]
    [string]$SummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$LogDirectory,

    [string]$ExpectedSkyMode = "ProceduralOutdoor",

    [string]$ExpectedExposurePolicy = "Scene",

    [string]$ExpectedDepthConvention = "ForwardZeroToOne",

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

function Test-Finite {
    param([double]$Value)

    return -not [double]::IsNaN($Value) -and
        -not [double]::IsInfinity($Value)
}

function Get-Average {
    param([double[]]$Values)

    Assert-Condition ($Values.Count -gt 0) "Cannot average an empty value set."
    return [double](($Values | Measure-Object -Average).Average)
}

function Get-GridRow {
    param(
        [object[]]$Values,
        [int]$Width,
        [int]$Row
    )

    $result = [double[]]::new($Width)
    for ($column = 0; $column -lt $Width; $column++) {
        $result[$column] = [double]$Values[($Row * $Width) + $column]
    }
    return $result
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
    $capturesByName = @{}
    foreach ($name in $requiredCaptureNames) {
        $captureRecords = @($summary.visualCaptures | Where-Object {
            [string]$_.capture.name -ceq $name
        })
        Assert-Condition ($captureRecords.Count -eq 1) `
            "Expected one visual capture named '$name'."
        Assert-Condition ([string]$captureRecords[0].state -ceq "Succeeded") `
            "Visual capture '$name' did not succeed."
        $captureFrames[$name] = [uint32]$captureRecords[0].capture.frameIndex

        $capturePath = [string]$captureRecords[0].capture.outputPath
        if (-not [System.IO.Path]::IsPathRooted($capturePath)) {
            $capturePath = Join-Path ([System.IO.Path]::GetDirectoryName($summaryFullPath)) $capturePath
        }
        Assert-Condition (Test-Path -LiteralPath $capturePath -PathType Leaf) `
            "Visual capture '$name' artifact is missing: $capturePath"
        $capture = Get-Content -LiteralPath $capturePath -Raw | ConvertFrom-Json
        Assert-Condition ([int]$capture.schemaVersion -eq 2) `
            "Visual capture '$name' schema is not version 2."
        Assert-Condition ($capture.passed -eq $true -and $capture.checks.passed -eq $true) `
            "Visual capture '$name' color checks did not pass."
        Assert-Condition ($capture.depth.passed -eq $true -and $capture.depth.checks.passed -eq $true) `
            "Visual capture '$name' depth checks did not pass."
        Assert-Condition (
            [int]$capture.spatialGridWidth -eq 4 -and
            [int]$capture.spatialGridHeight -eq 4 -and
            @($capture.spatialLuminanceGrid).Count -eq 16) `
            "Visual capture '$name' has an unexpected luminance-grid shape."
        Assert-Condition (
            [int]$capture.depth.spatialGridWidth -eq 4 -and
            [int]$capture.depth.spatialGridHeight -eq 4 -and
            @($capture.depth.spatialDepthGrid).Count -eq 16) `
            "Visual capture '$name' has an unexpected depth-grid shape."
        Assert-Condition (
            (Test-Finite ([double]$capture.averageLuminance)) -and
            [double]$capture.averageLuminance -ge 0.10 -and
            [double]$capture.averageLuminance -le 0.80 -and
            [double]$capture.minimumLuminance -ge 0.0 -and
            [double]$capture.maximumLuminance -le 1.0 -and
            @($capture.luminanceHistogram).Count -eq 16 -and
            [long]$capture.luminanceHistogram[15] -le
                [long]([Math]::Max(1, [long]$capture.pixelCount / 100))) `
            "Visual capture '$name' exposure is outside the canonical outdoor range."
        $capturesByName[$name] = $capture
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
        "No fresh player log was found for outdoor-atmosphere validation."
    $logText = Get-Content -LiteralPath $playerLog.FullName -Raw

    $number = '([-+0-9.Ee]+)'
    $diagnosticPattern = [regex]::new(
        '\[GenericRP\.AtmosphereValidation\] Frame=(\d+) SkyMode=([A-Za-z0-9]+) ' +
        'Atmosphere=([01]) Aerial=([01]) HeightFog=([01]) ExposurePolicy=([A-Za-z0-9]+) ' +
        'Exposure=' + $number + ' Depth=([A-Za-z0-9]+) IBL=([01]) SunCoupling=' + $number + ' ' +
        'AerialStart=' + $number + ' AerialDistance=' + $number + ' AerialStrength=' + $number + ' ' +
        'FogDensity=' + $number + ' FogFalloff=' + $number,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    $diagnosticsByFrame = @{}
    foreach ($match in $diagnosticPattern.Matches($logText)) {
        $diagnosticsByFrame[[uint32]$match.Groups[1].Value] = [pscustomobject]@{
            SkyMode = $match.Groups[2].Value
            Atmosphere = [int]$match.Groups[3].Value
            Aerial = [int]$match.Groups[4].Value
            HeightFog = [int]$match.Groups[5].Value
            ExposurePolicy = $match.Groups[6].Value
            Exposure = [double]::Parse($match.Groups[7].Value, [Globalization.CultureInfo]::InvariantCulture)
            DepthConvention = $match.Groups[8].Value
            Ibl = [int]$match.Groups[9].Value
            SunCoupling = [double]::Parse($match.Groups[10].Value, [Globalization.CultureInfo]::InvariantCulture)
            AerialStart = [double]::Parse($match.Groups[11].Value, [Globalization.CultureInfo]::InvariantCulture)
            AerialDistance = [double]::Parse($match.Groups[12].Value, [Globalization.CultureInfo]::InvariantCulture)
            AerialStrength = [double]::Parse($match.Groups[13].Value, [Globalization.CultureInfo]::InvariantCulture)
            FogDensity = [double]::Parse($match.Groups[14].Value, [Globalization.CultureInfo]::InvariantCulture)
            FogFalloff = [double]::Parse($match.Groups[15].Value, [Globalization.CultureInfo]::InvariantCulture)
        }
    }

    foreach ($name in $requiredCaptureNames) {
        $frame = [uint32]$captureFrames[$name]
        Assert-Condition ($diagnosticsByFrame.ContainsKey($frame)) `
            "Capture '$name' frame $frame has no atmosphere diagnostic."
        $diagnostic = $diagnosticsByFrame[$frame]
        Assert-Condition ([string]$diagnostic.SkyMode -ceq $ExpectedSkyMode) `
            "Capture '$name' used sky mode '$($diagnostic.SkyMode)', expected '$ExpectedSkyMode'."
        Assert-Condition (
            $diagnostic.Atmosphere -eq 1 -and
            $diagnostic.Aerial -eq 1 -and
            $diagnostic.HeightFog -eq 1) `
            "Capture '$name' did not enable the canonical atmosphere, aerial perspective, and height fog."
        Assert-Condition ([string]$diagnostic.ExposurePolicy -ceq $ExpectedExposurePolicy) `
            "Capture '$name' used exposure policy '$($diagnostic.ExposurePolicy)', expected '$ExpectedExposurePolicy'."
        Assert-Condition (
            (Test-Finite $diagnostic.Exposure) -and
            $diagnostic.Exposure -gt 0.0 -and
            $diagnostic.Exposure -le 64.0) `
            "Capture '$name' used an invalid effective exposure."
        Assert-Condition ([string]$diagnostic.DepthConvention -ceq $ExpectedDepthConvention) `
            "Capture '$name' used depth convention '$($diagnostic.DepthConvention)', expected '$ExpectedDepthConvention'."
        Assert-Condition ($diagnostic.Ibl -eq 1) `
            "Capture '$name' lost panorama-backed image-based lighting."
        Assert-Condition (
            $diagnostic.SunCoupling -gt 0.0 -and
            $diagnostic.SunCoupling -le 1.0 -and
            $diagnostic.AerialStart -ge 0.0 -and
            $diagnostic.AerialDistance -gt 0.0 -and
            $diagnostic.AerialStrength -gt 0.0 -and
            $diagnostic.AerialStrength -le 1.0 -and
            $diagnostic.FogDensity -gt 0.0 -and
            $diagnostic.FogFalloff -gt 0.0) `
            "Capture '$name' used an inactive or invalid canonical outdoor profile."
    }

    $near = $capturesByName["shadow-near"]
    $mid = $capturesByName["shadow-mid"]
    $far = $capturesByName["shadow-far"]
    $stable = $capturesByName["shadow-far-stable"]
    Assert-Condition (
        [double]$mid.averageLuminance -ge [double]$near.averageLuminance + 0.003 -and
        [double]$far.averageLuminance -ge [double]$mid.averageLuminance + 0.003) `
        "Near/mid/far captures do not retain the canonical distance-haze readability progression."

    $horizonRow = Get-GridRow -Values @($far.spatialLuminanceGrid) -Width 4 -Row 1
    $horizonRange = [double](($horizonRow | Measure-Object -Maximum).Maximum) -
        [double](($horizonRow | Measure-Object -Minimum).Minimum)
    Assert-Condition ($horizonRange -le 0.08) `
        "Far-view horizon luminance is discontinuous across the frame."

    $upperRows = @(
        (Get-GridRow -Values @($far.spatialLuminanceGrid) -Width 4 -Row 0) +
        $horizonRow)
    $bottomRow = Get-GridRow -Values @($far.spatialLuminanceGrid) -Width 4 -Row 3
    $upperLuminance = Get-Average -Values $upperRows
    $bottomLuminance = Get-Average -Values $bottomRow
    Assert-Condition ($upperLuminance -ge $bottomLuminance + 0.15) `
        "Far-view sky/terrain luminance orientation is inverted or unreadable."

    $topDepth = Get-Average -Values (Get-GridRow -Values @($far.depth.spatialDepthGrid) -Width 4 -Row 0)
    $bottomDepth = Get-Average -Values (Get-GridRow -Values @($far.depth.spatialDepthGrid) -Width 4 -Row 3)
    Assert-Condition (
        [long]$far.depth.clearDepthPixelCount -gt 0 -and
        [long]$far.depth.writtenDepthPixelCount -gt 0 -and
        $topDepth -ge $bottomDepth + 0.001) `
        "Far-view depth does not preserve forward-depth sky/terrain orientation."

    Assert-Condition (
        [string]$far.pixelSha256 -ceq [string]$stable.pixelSha256 -and
        [string]$far.depth.pixelSha256 -ceq [string]$stable.depth.pixelSha256) `
        "Stationary far atmosphere frames changed color or depth output."
    Assert-Condition ([string]$near.pixelSha256 -cne [string]$far.pixelSha256) `
        "Near and far atmosphere captures unexpectedly produced identical color output."

    Write-Host ((
        "[Arisen] Outdoor-atmosphere visuals passed: sky={0}, exposure={1:F3}, " +
        "near/mid/far={2:F3}/{3:F3}/{4:F3}, horizonRange={5:F4}, log={6}") -f
        $ExpectedSkyMode,
        $diagnosticsByFrame[[uint32]$captureFrames["shadow-far"]].Exposure,
        [double]$near.averageLuminance,
        [double]$mid.averageLuminance,
        [double]$far.averageLuminance,
        $horizonRange,
        $playerLog.FullName)
    exit 0
}
catch {
    Write-Host "[ERROR] Outdoor-atmosphere visual validation failed: $($_.Exception.Message)"
    exit 1
}

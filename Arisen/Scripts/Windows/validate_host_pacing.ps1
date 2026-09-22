param(
    [string]$EngineRoot = "",

    [string]$Profile = "Development",

    [string]$Configuration = "Debug",

    [double]$PresentingSampleSeconds = 3.0,

    # The park is asserted to advance no frame, which only proves pacing if the loop was advancing
    # frames before the minimize. The gate therefore waits for the visible window to present frames
    # of its own first, so a loop that was already stalled cannot pass the parked interval
    # vacuously.
    [int]$MinimumVisibleFrames = 30,

    [int]$PresentingWarmupSeconds = 180,

    [double]$ParkedSeconds = 5.0,

    # Fraction of one core the parked runtime may burn over the parked interval. The pre-park
    # runtime burned a whole core for as long as the window stayed minimized; a parked loop wakes
    # only for the window messages that change its state, so the budget is a small fraction of the
    # parked wall time rather than an estimate of how fast the loop may run.
    [double]$ParkedCpuBudgetRatio = 0.10,

    [double]$ResumedSampleSeconds = 3.0,

    [int]$WindowTimeoutSeconds = 240,

    [int]$MessageTimeoutSeconds = 60,

    [int]$ShutdownTimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($EngineRoot)) {
    $EngineRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
}

$workspaceDir = Join-Path $EngineRoot "Arisen\Development\PackageGame"
$binDir = Join-Path $workspaceDir ".arisen\bin\$Profile\$Configuration"
$exePath = Join-Path $binDir "PackageGame.exe"
$logsDir = Join-Path $binDir "logs"
$validationLogDir = Join-Path $workspaceDir ".arisen\Logs"
$artifactPath = Join-Path $validationLogDir "host-pacing-$Profile-latest.json"

$parkMarker = "[PlatformSubsystem] Main window minimized: parking the frame loop"
$resumeMarker = "[PlatformSubsystem] Main window can present again"
$submissionMarker = "[RenderSubmission] BeginFrame"

if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
    throw "Runtime executable is missing: $exePath. Build the $Profile profile first."
}

New-Item -ItemType Directory -Force -Path $validationLogDir | Out-Null

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Read-LogText {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return ""
    }

    # The runtime owns the player log and keeps it open for the whole run; reading it while the
    # process is alive is how the gate observes the park without owning the process's stdout.
    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::ReadWrite)
    try {
        $reader = [System.IO.StreamReader]::new($stream)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Wait-ForLogText {
    param(
        [string]$Path,
        [string]$Marker,
        [double]$TimeoutSeconds,
        [string]$Description
    )

    $deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([datetime]::UtcNow -lt $deadline) {
        $text = Read-LogText -Path $Path
        if ($text.IndexOf($Marker, [StringComparison]::Ordinal) -ge 0) {
            return $text
        }

        Start-Sleep -Milliseconds 100
    }

    throw "Timed out after $TimeoutSeconds s waiting for $Description in $Path"
}

function Get-SubmittedFrameCount {
    param([string]$Text)

    return ([regex]::Matches($Text, [regex]::Escape($submissionMarker))).Count
}

function Get-ParkFrameIndex {
    param([string]$Text)

    $match = [regex]::Match(
        $Text,
        [regex]::Escape($parkMarker) + "[^\r\n]*Frame=(\d+)")
    Assert-Condition $match.Success "The park line does not report the frame it parked."
    return [int]$match.Groups[1].Value
}

function Get-ResumeFrameIndex {
    param([string]$Text)

    $match = [regex]::Match(
        $Text,
        [regex]::Escape($resumeMarker) + "[^\r\n]*Frame=(\d+)")
    Assert-Condition $match.Success "The resume line does not report the frame it resumed."
    return [int]$match.Groups[1].Value
}

# The platform logs the window it created and owns, and that window is the one whose state drives
# the park. Resolving the handle from that line keeps the gate on the platform's window instead of
# a process heuristic that may pick the console or a helper window.
function Get-MainWindowHandle {
    param([string]$Text)

    $match = [regex]::Match($Text, "Created main window\. Handle=0x([0-9A-Fa-f]+)")
    if (-not $match.Success) {
        return [IntPtr]::Zero
    }

    return [IntPtr]::new([Convert]::ToInt64($match.Groups[1].Value, 16))
}

Add-Type -Namespace ArisenHostPacing -Name NativeMethods -MemberDefinition @"
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool IsWindowVisible(System.IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool IsIconic(System.IntPtr hWnd);

    // ShowWindow sends the state change to the target's thread and blocks until that thread
    // processes it, which would hand the gate the parking the runtime itself must prove. The async
    // form posts the request, and the gate asserts the resulting window state instead.
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(System.IntPtr hWnd, int nCmdShow);
"@

# A cooked-only runtime profile owns its assets in the mutable cooked cache, which the launch does
# not refresh on its own. The Editor profile reads source assets and Production ships cooked ones.
if ($Profile -ieq "Development") {
    Write-Host "[Arisen] Cooking runtime assets for $Profile."
    Push-Location $binDir
    try {
        & $exePath --arisen-cook-runtime-assets --workspace $workspaceDir --profile $Profile --configuration $Configuration --runtime-identifier win-x64 | Out-Null
        Assert-Condition ($LASTEXITCODE -eq 0) "Runtime asset cooking for $Profile failed with exit code $LASTEXITCODE."
    }
    finally {
        Pop-Location
    }
}

New-Item -ItemType Directory -Force -Path $logsDir | Out-Null
$playerLogsBefore = @(
    Get-ChildItem -LiteralPath $logsDir -Filter "player_*.log" -File -ErrorAction SilentlyContinue |
        ForEach-Object { $_.FullName }
)

$env:ARISEN_RENDER_DIAGNOSTICS = "submission"
$process = $null
try {
    Write-Host "[Arisen] Launching the interactive $Profile runtime: $exePath"
    $process = Start-Process -FilePath $exePath -WorkingDirectory $binDir -PassThru
}
finally {
    Remove-Item Env:\ARISEN_RENDER_DIAGNOSTICS -ErrorAction SilentlyContinue
}

try {
    $playerLog = $null
    $deadline = [datetime]::UtcNow.AddSeconds($MessageTimeoutSeconds)
    while ([datetime]::UtcNow -lt $deadline) {
        Assert-Condition (-not $process.HasExited) "The runtime exited before it created its player log."
        $candidates = @(
            Get-ChildItem -LiteralPath $logsDir -Filter "player_*.log" -File |
                Where-Object { $playerLogsBefore -notcontains $_.FullName }
        )
        if ($candidates.Count -ge 1) {
            $playerLog = ($candidates | Sort-Object LastWriteTimeUtc | Select-Object -Last 1).FullName
            break
        }

        Start-Sleep -Milliseconds 200
    }

    Assert-Condition ($null -ne $playerLog) `
        "The launch did not create a player log under $logsDir within $MessageTimeoutSeconds s."

    Write-Host "[Arisen] Player log: $playerLog"

    # The runtime creates its window hidden and reveals it through the startup presentation gate
    # once a frame owns scene content, so a visible main window also proves the gate released.
    $deadline = [datetime]::UtcNow.AddSeconds($WindowTimeoutSeconds)
    $windowHandle = [IntPtr]::Zero
    while ([datetime]::UtcNow -lt $deadline) {
        Assert-Condition (-not $process.HasExited) "The runtime exited before it revealed its main window."
        $candidateHandle = Get-MainWindowHandle -Text (Read-LogText -Path $playerLog)
        if ($candidateHandle -ne [IntPtr]::Zero -and
            [ArisenHostPacing.NativeMethods]::IsWindowVisible($candidateHandle)) {
            $windowHandle = $candidateHandle
            break
        }

        Start-Sleep -Milliseconds 250
    }

    Assert-Condition ($windowHandle -ne [IntPtr]::Zero) `
        "The runtime did not reveal its main window within $WindowTimeoutSeconds s."

    Write-Host ("[Arisen] Main window handle: 0x{0:X}" -f [int64]$windowHandle)

    # The visible window must present frames of its own before the minimize, so the zero-frame
    # parked interval below reports the park rather than a loop that had already stopped.
    $visibleFramesBefore = Get-SubmittedFrameCount -Text (Read-LogText -Path $playerLog)
    $visibleFrames = 0
    $deadline = [datetime]::UtcNow.AddSeconds($PresentingWarmupSeconds)
    while ([datetime]::UtcNow -lt $deadline) {
        Assert-Condition (-not $process.HasExited) `
            "The runtime exited before it presented frames through its visible window."
        $visibleFrames = (Get-SubmittedFrameCount -Text (Read-LogText -Path $playerLog)) - $visibleFramesBefore
        if ($visibleFrames -ge $MinimumVisibleFrames) {
            break
        }

        Start-Sleep -Milliseconds 100
    }

    Assert-Condition ($visibleFrames -ge $MinimumVisibleFrames) `
        "The visible runtime presented $visibleFrames frame(s) within $PresentingWarmupSeconds s; expected at least $MinimumVisibleFrames before the park is exercised."

    # Presenting baseline: the loop is paced by presentation, so the swapchain submits frames here.
    $presentingTextStart = Read-LogText -Path $playerLog
    $presentingFramesBefore = Get-SubmittedFrameCount -Text $presentingTextStart
    $presentingWallStart = [datetime]::UtcNow
    Start-Sleep -Seconds $PresentingSampleSeconds
    $presentingTextEnd = Read-LogText -Path $playerLog
    $presentingWallSeconds = ([datetime]::UtcNow - $presentingWallStart).TotalSeconds
    $presentingFrames = (Get-SubmittedFrameCount -Text $presentingTextEnd) - $presentingFramesBefore
    $presentingFramesPerSecond = $presentingFrames / $presentingWallSeconds
    Assert-Condition ($presentingFrames -gt 0) `
        "The visible runtime submitted no frame during the $PresentingSampleSeconds s baseline."

    # Minimize: the window reports a zero-extent surface, so the host must park instead of running
    # frames nobody can see. The log length recorded here anchors every assertion below to the
    # interval this gate drives, so a window-state change on the desktop cannot be mistaken for a
    # transition the runtime produced.
    $intervalStart = (Read-LogText -Path $playerLog).Length
    [ArisenHostPacing.NativeMethods]::ShowWindowAsync($windowHandle, 6) | Out-Null
    $parkedCpuStart = $process.TotalProcessorTime.TotalSeconds
    $parkedWallStart = [datetime]::UtcNow

    Wait-ForLogText -Path $playerLog -Marker $parkMarker -TimeoutSeconds $MessageTimeoutSeconds `
        -Description "the parked frame loop" | Out-Null
    Assert-Condition ([ArisenHostPacing.NativeMethods]::IsIconic($windowHandle)) `
        "The runtime's main window is not minimized; the park cannot be exercised."

    Start-Sleep -Seconds $ParkedSeconds

    $parkedInterval = (Read-LogText -Path $playerLog).Substring($intervalStart)
    $parkedWallSeconds = ([datetime]::UtcNow - $parkedWallStart).TotalSeconds
    $parkedCpuSeconds = $process.TotalProcessorTime.TotalSeconds - $parkedCpuStart
    $parkedCpuRatio = $parkedCpuSeconds / $parkedWallSeconds
    Assert-Condition ([ArisenHostPacing.NativeMethods]::IsIconic($windowHandle)) `
        "The runtime's main window was restored while the gate sampled the parked loop; rerun the gate on an undisturbed desktop."
    Assert-Condition ($parkedCpuSeconds -lt ($parkedWallSeconds * $ParkedCpuBudgetRatio)) `
        ("The parked runtime burned {0:F2}s of CPU over {1:F2}s wall ({2:P1} of one core), which is above the {3:P0} budget." -f `
            $parkedCpuSeconds, $parkedWallSeconds, $parkedCpuRatio, $ParkedCpuBudgetRatio)

    $parkFrame = Get-ParkFrameIndex -Text $parkedInterval
    $minimizeTransitions = ([regex]::Matches($parkedInterval, [regex]::Escape($parkMarker))).Count
    Assert-Condition ($minimizeTransitions -eq 1) `
        "The minimized interval reported $minimizeTransitions park transitions; the gate drives exactly one."

    # Restore: the park ends on the message that changes the window state, and the frame that
    # resumes is the frame the park was entered from.
    [ArisenHostPacing.NativeMethods]::ShowWindowAsync($windowHandle, 9) | Out-Null
    Wait-ForLogText -Path $playerLog -Marker $resumeMarker -TimeoutSeconds $MessageTimeoutSeconds `
        -Description "the resumed frame loop" | Out-Null
    Assert-Condition (-not [ArisenHostPacing.NativeMethods]::IsIconic($windowHandle)) `
        "The runtime's main window did not leave the minimized state."

    $resumedInterval = (Read-LogText -Path $playerLog).Substring($intervalStart)
    $resumeFrame = Get-ResumeFrameIndex -Text $resumedInterval
    Assert-Condition ($resumeFrame -eq $parkFrame) `
        "The runtime advanced from frame $parkFrame to frame $resumeFrame while its window was minimized."

    # The loop advances no frame while the host cannot present, so no frame may be submitted
    # between the park and the resume line that ends it.
    $parkIndex = $resumedInterval.IndexOf($parkMarker, [StringComparison]::Ordinal)
    $resumeIndex = $resumedInterval.IndexOf($resumeMarker, [StringComparison]::Ordinal)
    Assert-Condition ($parkIndex -ge 0 -and $resumeIndex -gt $parkIndex) `
        "The minimized interval is not reported as a park followed by a resume."
    $framesWhileParked = Get-SubmittedFrameCount -Text $resumedInterval.Substring($parkIndex, $resumeIndex - $parkIndex)
    Assert-Condition ($framesWhileParked -eq 0) `
        "The parked runtime submitted $framesWhileParked frame(s) while the window was minimized."

    $resumedFramesBefore = Get-SubmittedFrameCount -Text $resumedInterval.Substring(0, $resumeIndex)
    $resumedWallStart = [datetime]::UtcNow
    Start-Sleep -Seconds $ResumedSampleSeconds
    $resumedWallSeconds = ([datetime]::UtcNow - $resumedWallStart).TotalSeconds
    $resumedFrames = (Get-SubmittedFrameCount -Text (Read-LogText -Path $playerLog).Substring($intervalStart)) - $resumedFramesBefore
    $resumedFramesPerSecond = $resumedFrames / $resumedWallSeconds
    Assert-Condition ($resumedFrames -gt 0) `
        "The runtime submitted no frame after its window was restored."
    Assert-Condition (-not [ArisenHostPacing.NativeMethods]::IsIconic($windowHandle)) `
        "The runtime's main window was minimized again while the gate sampled the resumed loop; rerun the gate on an undisturbed desktop."

    $resumeTransitions = ([regex]::Matches($resumedInterval, [regex]::Escape($resumeMarker))).Count
    Assert-Condition ($resumeTransitions -eq 1) `
        "The minimized interval reported $resumeTransitions resume transitions; the gate drives exactly one."

    Assert-Condition ($process.CloseMainWindow()) "Closing the runtime's main window failed."
    Assert-Condition ($process.WaitForExit($ShutdownTimeoutSeconds * 1000)) `
        "The runtime did not exit within $ShutdownTimeoutSeconds s of its close request."
    Assert-Condition ($process.ExitCode -eq 0) "The runtime exited with code $($process.ExitCode)."

    $finalText = Read-LogText -Path $playerLog
    Assert-Condition ($finalText.IndexOf("[PackageSubsystem] Error unloading package", [StringComparison]::Ordinal) -lt 0) `
        "The runtime reported a package unload failure on shutdown."

    $vkValidationLog = Join-Path $binDir "vk_validation.log"
    if (Test-Path -LiteralPath $vkValidationLog -PathType Leaf) {
        Assert-Condition ((Get-Item -LiteralPath $vkValidationLog).Length -eq 0) `
            "Vulkan validation reported messages: $vkValidationLog"
    }

    $artifact = [ordered]@{
        schemaVersion = 1
        profile = $Profile
        configuration = $Configuration
        playerLog = $playerLog
        processId = $process.Id
        parkFrame = $parkFrame
        resumeFrame = $resumeFrame
        parkedSeconds = [math]::Round($parkedWallSeconds, 3)
        parkedCpuSeconds = [math]::Round($parkedCpuSeconds, 3)
        parkedCpuRatio = [math]::Round($parkedCpuRatio, 4)
        presentingFramesPerSecond = [math]::Round($presentingFramesPerSecond, 2)
        visibleFramesBeforePark = $visibleFrames
        resumedFramesPerSecond = [math]::Round($resumedFramesPerSecond, 2)
        framesSubmittedWhileParked = $framesWhileParked
        parkTransitions = $minimizeTransitions
        resumeTransitions = $resumeTransitions
        passed = $true
    }

    $artifact | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $artifactPath -Encoding utf8
    Write-Host (("[Arisen] Host pacing passed: presenting {0:F1} fps, parked {1:F2}s at {2:P1} of one core, " +
        "resumed at frame {3} ({4:F1} fps), artifact: {5}") -f `
        $presentingFramesPerSecond, $parkedWallSeconds, $parkedCpuRatio, $resumeFrame, $resumedFramesPerSecond, $artifactPath)
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }
}

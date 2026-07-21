param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,

    [Parameter(Mandatory = $true)]
    [string]$WorkspaceRoot,

    [string]$SmokeMode = "scene",

    [ValidateRange(1, 1000000)]
    [int]$Frames = 1,

    [Parameter(Mandatory = $true)]
    [string]$LogPath,

    [Parameter(Mandatory = $true)]
    [string]$SummaryPath
)

$ErrorActionPreference = "Stop"
$sourceRootPath = [System.IO.Path]::GetFullPath($SourceRoot)
$workspaceRootPath = [System.IO.Path]::GetFullPath($WorkspaceRoot)
$logPathFull = [System.IO.Path]::GetFullPath($LogPath)
$summaryPathFull = [System.IO.Path]::GetFullPath($SummaryPath)
$temporaryParent = Join-Path ([System.IO.Path]::GetTempPath()) "ArisenRelocatedProduction"
$temporaryRoot = Join-Path $temporaryParent ([Guid]::NewGuid().ToString("N"))
$relocatedRoot = Join-Path $temporaryRoot "Player"
$checks = [ordered]@{
    metadataIsSourceIndependent = $false
    sourceFilesAbsent = $false
    relocatedBootPassed = $false
    cookedSceneObserved = $false
    worldStreamingSmokePassed = $false
    worldStreamingVisualsPassed = $false
    vulkanValidationLogsEmpty = $false
    tamperRejected = $false
    missingArtifactRejected = $false
}
$artifactIdentity = $null
$worldStreamingSummaryArtifact = $null
$worldStreamingVisualArtifacts = @()
$failure = $null

function Write-ValidationLog {
    param([string]$Message)

    $Message | Tee-Object -FilePath $logPathFull -Append | Write-Host
}

function Get-RunOutput {
    param(
        [string]$CaseName,
        [string]$ExecutablePath,
        [string]$Arguments = "--smoke-mode $SmokeMode --frames $Frames",
        [switch]$RequireEmptyVulkanLog
    )

    $runtimeLogs = Join-Path $relocatedRoot "logs"
    if (Test-Path -LiteralPath $runtimeLogs) {
        Remove-Item -LiteralPath $runtimeLogs -Recurse -Force
    }
    $validationLog = Join-Path $temporaryRoot "vk_validation.log"
    Remove-Item -LiteralPath $validationLog -Force -ErrorAction SilentlyContinue

    $processInfo = New-Object System.Diagnostics.ProcessStartInfo
    $processInfo.FileName = $ExecutablePath
    $processInfo.Arguments = $Arguments
    $processInfo.WorkingDirectory = $temporaryRoot
    $processInfo.UseShellExecute = $false
    $processInfo.CreateNoWindow = $true
    $processInfo.RedirectStandardOutput = $true
    $processInfo.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $processInfo
    if (-not $process.Start()) {
        throw "Failed to start relocated Production $CaseName run."
    }

    $standardOutput = $process.StandardOutput.ReadToEndAsync()
    $standardError = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(60000)) {
        try { $process.Kill() } catch {}
        $process.WaitForExit()
        throw "Relocated Production $CaseName run exceeded 60 seconds."
    }

    $process.WaitForExit()
    $exitCode = [int]$process.ExitCode
    $outputParts = @($standardOutput.Result, $standardError.Result)
    if (Test-Path -LiteralPath $runtimeLogs) {
        $latestPlayerLog = Get-ChildItem -LiteralPath $runtimeLogs -Filter "player_*.log" -File |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        if ($null -ne $latestPlayerLog) {
            $outputParts += Get-Content -LiteralPath $latestPlayerLog.FullName -Raw
        }
    }

    $combined = $outputParts -join [Environment]::NewLine
    $process.Dispose()
    if ($RequireEmptyVulkanLog) {
        if (-not (Test-Path -LiteralPath $validationLog -PathType Leaf)) {
            throw "Relocated Production $CaseName run produced no Vulkan validation log."
        }
        if ((Get-Item -LiteralPath $validationLog).Length -ne 0) {
            $validationText = Get-Content -LiteralPath $validationLog -Raw
            Add-Content -LiteralPath $logPathFull -Value $validationText
            throw "Relocated Production $CaseName Vulkan validation log is not empty."
        }
    }
    Write-ValidationLog "[Arisen] Relocated Production $CaseName exit code: $exitCode"
    Add-Content -LiteralPath $logPathFull -Value $combined
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $combined
    }
}

try {
    $logDirectory = Split-Path -Parent $logPathFull
    $summaryDirectory = Split-Path -Parent $summaryPathFull
    New-Item -ItemType Directory -Path $logDirectory,$summaryDirectory -Force | Out-Null
    Remove-Item -LiteralPath $logPathFull,$summaryPathFull -Force -ErrorAction SilentlyContinue

    if (-not (Test-Path -LiteralPath $sourceRootPath -PathType Container)) {
        throw "Production output does not exist: $sourceRootPath"
    }

    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    Copy-Item -LiteralPath $sourceRootPath -Destination $relocatedRoot -Recurse -Force
    Write-ValidationLog "[Arisen] Copied Production output to isolated root: $relocatedRoot"

    $requiredFiles = @(
        "PackageGame.exe",
        "manifest.json",
        "manifest.resolved.json",
        "launch.config.json",
        "runtime-assets.json"
    )
    foreach ($requiredFile in $requiredFiles) {
        $requiredPath = Join-Path $relocatedRoot $requiredFile
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Relocated output is missing required file '$requiredFile'."
        }
    }

    $metadataFiles = @(
        (Join-Path $relocatedRoot "manifest.json"),
        (Join-Path $relocatedRoot "manifest.resolved.json"),
        (Join-Path $relocatedRoot "launch.config.json"),
        (Join-Path $relocatedRoot "runtime-assets.json")
    ) + @(
        Get-ChildItem -LiteralPath (Join-Path $relocatedRoot "Packages") -Filter "*.json" -File -Recurse |
            Select-Object -ExpandProperty FullName
    )
    foreach ($metadataFile in $metadataFiles) {
        $metadataText = Get-Content -LiteralPath $metadataFile -Raw
        if ($metadataText.IndexOf($workspaceRootPath, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $metadataText.IndexOf(".arisen/Cache", [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $metadataText.IndexOf(".arisen\\Cache", [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $metadataText.IndexOf("file://Local/", [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $metadataText.IndexOf("file://../", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Runtime metadata contains a workspace, cache, or source-package path: $metadataFile"
        }
    }
    $checks.metadataIsSourceIndependent = $true

    $forbiddenSourceFiles = Get-ChildItem -LiteralPath $relocatedRoot -File -Recurse | Where-Object {
        $_.Name -ieq "AssetManifest.json" -or
        $_.Extension -in @(".arisenscene", ".scene", ".yaml", ".yml", ".meta")
    }
    if (@($forbiddenSourceFiles).Count -ne 0) {
        throw "Relocated output contains authoring/cache files: $($forbiddenSourceFiles.FullName -join ', ')"
    }
    $checks.sourceFilesAbsent = $true

    $executable = Join-Path $relocatedRoot "PackageGame.exe"
    $success = Get-RunOutput -CaseName "boot" -ExecutablePath $executable -RequireEmptyVulkanLog
    if ($success.ExitCode -ne 0) {
        throw "Relocated Production boot failed with exit code $($success.ExitCode)."
    }
    $workspaceManifestPath = Join-Path $workspaceRootPath "manifest.json"
    $workspaceCachePath = Join-Path $workspaceRootPath ".arisen\Cache"
    if ($success.Output.IndexOf($workspaceManifestPath, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $success.Output.IndexOf($workspaceCachePath, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $success.Output -match "(?i)$([Regex]::Escape($workspaceRootPath)).*package\.json" -or
        $success.Output -match "(?i)$([Regex]::Escape($workspaceRootPath)).*\.arisenscene") {
        throw "Relocated Production boot accessed source project, package, cache, or scene metadata."
    }
    if ($success.Output -notmatch "Using deployed runtime metadata rooted at:" -or
        $success.Output -notmatch
            "ReadOnlyRuntime mode with Disabled source access: 0 indexed source asset\(s\)") {
        throw "Relocated Production boot did not prove deployed metadata and zero source indexing."
    }
    $checks.relocatedBootPassed = $true
    if ($success.Output -notmatch "Variant: runtime\.scene\.v1") {
        throw "Relocated Production boot did not report a cooked scene load."
    }
    $checks.cookedSceneObserved = $true

    $worldStreamingSummaryPath = Join-Path $relocatedRoot "logs\world-streaming-summary-Production.json"
    $worldStreamingVisualBasePath = Join-Path $relocatedRoot "logs\world-streaming-visual-Production.json"
    $worldArguments = '--smoke-mode world-streaming --frames 1 ' +
        '--smoke-summary-output "{0}" --visual-summary --visual-summary-output "{1}"' -f
        $worldStreamingSummaryPath,$worldStreamingVisualBasePath
    $worldStreaming = Get-RunOutput `
        -CaseName "world-streaming" `
        -ExecutablePath $executable `
        -Arguments $worldArguments `
        -RequireEmptyVulkanLog
    if ($worldStreaming.ExitCode -ne 0) {
        throw "Relocated Production world-streaming smoke failed with exit code $($worldStreaming.ExitCode)."
    }
    if ($worldStreaming.Output.IndexOf($workspaceManifestPath, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $worldStreaming.Output.IndexOf($workspaceCachePath, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $worldStreaming.Output -match "(?i)$([Regex]::Escape($workspaceRootPath)).*package\.json" -or
        $worldStreaming.Output -match "(?i)$([Regex]::Escape($workspaceRootPath)).*\.arisenscene") {
        throw "Relocated Production world-streaming smoke accessed workspace or source-scene state."
    }
    & (Join-Path $PSScriptRoot "validate_world_streaming_summary.ps1") `
        -SummaryPath $worldStreamingSummaryPath `
        -ExpectedProfile "Production" `
        -ExpectedVisualCaptureCount 3
    if ($LASTEXITCODE -ne 0) {
        throw "Relocated Production world-streaming summary validation failed."
    }
    $checks.worldStreamingSmokePassed = $true
    $checks.worldStreamingVisualsPassed = $true
    $checks.vulkanValidationLogsEmpty = $true

    $worldStreamingSummaryArtifact = Join-Path $summaryDirectory "relocated-production-world-streaming.json"
    Copy-Item -LiteralPath $worldStreamingSummaryPath -Destination $worldStreamingSummaryArtifact -Force
    foreach ($checkpoint in @("before", "during", "after")) {
        $sourceVisual = Join-Path $relocatedRoot "logs\world-streaming-visual-Production.$checkpoint.json"
        $destinationVisual = Join-Path $summaryDirectory "relocated-production-world-streaming.$checkpoint.json"
        Copy-Item -LiteralPath $sourceVisual -Destination $destinationVisual -Force
        $worldStreamingVisualArtifacts += $destinationVisual
    }
    $preservedWorldSummary = Get-Content -LiteralPath $worldStreamingSummaryArtifact -Raw | ConvertFrom-Json
    foreach ($capture in @($preservedWorldSummary.visualCaptures)) {
        $capture.capture.outputPath = @(
            $worldStreamingVisualArtifacts |
                Where-Object { $_.EndsWith(".$($capture.capture.name).json", [StringComparison]::OrdinalIgnoreCase) })[0]
    }
    $preservedWorldSummary |
        ConvertTo-Json -Depth 12 |
        Set-Content -LiteralPath $worldStreamingSummaryArtifact -Encoding UTF8

    $catalogPath = Join-Path $relocatedRoot "runtime-assets.json"
    $catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
    $artifact = @($catalog.Artifacts | Sort-Object sizeInBytes, path | Select-Object -First 1)[0]
    if ($null -eq $artifact) {
        throw "Runtime catalog has no artifact available for tamper validation."
    }
    $artifactIdentity = "{0}:{1}" -f $artifact.Guid,$artifact.Variant
    $artifactPath = Join-Path `
        (Join-Path $relocatedRoot "Content") `
        ([string]$artifact.path).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $originalBytes = [System.IO.File]::ReadAllBytes($artifactPath)
    if ($originalBytes.Length -eq 0) {
        throw "Selected runtime artifact '$artifactIdentity' is empty."
    }
    $tamperedBytes = [byte[]]$originalBytes.Clone()
    $tamperedBytes[0] = $tamperedBytes[0] -bxor 0xFF
    [System.IO.File]::WriteAllBytes($artifactPath, $tamperedBytes)
    $tampered = Get-RunOutput -CaseName "tampered" -ExecutablePath $executable
    if ($tampered.ExitCode -ne 1 -or $tampered.Output -notmatch "SHA-256 mismatch") {
        throw "Tampered runtime artifact was not rejected with a SHA-256 diagnostic."
    }
    $checks.tamperRejected = $true

    [System.IO.File]::WriteAllBytes($artifactPath, $originalBytes)
    Remove-Item -LiteralPath $artifactPath -Force
    $missing = Get-RunOutput -CaseName "missing" -ExecutablePath $executable
    if ($missing.ExitCode -ne 1 -or $missing.Output -notmatch "is missing at") {
        throw "Missing runtime artifact was not rejected with a stable missing-artifact diagnostic."
    }
    $checks.missingArtifactRejected = $true

    Write-ValidationLog "[Arisen] Relocated Production validation passed for artifact $artifactIdentity."
}
catch {
    $failure = $_.Exception.Message
    Write-ValidationLog "[ERROR] Relocated Production validation failed: $failure"
}
finally {
    $summary = [ordered]@{
        schemaVersion = 2
        capturedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        sourceRoot = $sourceRootPath
        relocatedRootWasOutsideWorkspace = -not $relocatedRoot.StartsWith(
            $workspaceRootPath,
            [StringComparison]::OrdinalIgnoreCase)
        smokeMode = $SmokeMode
        frames = $Frames
        artifactIdentity = $artifactIdentity
        worldStreamingSummaryArtifact = $worldStreamingSummaryArtifact
        worldStreamingVisualArtifacts = $worldStreamingVisualArtifacts
        passed = ($null -eq $failure -and -not ($checks.Values -contains $false))
        checks = $checks
        failure = $failure
        logPath = $logPathFull
    }
    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPathFull -Encoding UTF8

    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($null -ne $failure) {
    exit 1
}

exit 0

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
    terrainClosureComplete = $false
    relocatedBootPassed = $false
    cookedSceneObserved = $false
    worldStreamingSmokePassed = $false
    worldStreamingVisualsPassed = $false
    terrainStreamingSmokePassed = $false
    terrainStreamingVisualsPassed = $false
    terrainStreamingSourceIndependent = $false
    terrainStreamingShutdownPassed = $false
    vulkanValidationLogsEmpty = $false
    tamperRejected = $false
    missingArtifactRejected = $false
}
$artifactIdentity = $null
$worldStreamingSummaryArtifact = $null
$worldStreamingVisualArtifacts = @()
$terrainStreamingSummaryArtifact = $null
$terrainStreamingVisualArtifacts = @()
$failure = $null

function Write-ValidationLog {
    param([string]$Message)

    $Message | Tee-Object -FilePath $logPathFull -Append | Write-Host
}

function Get-CatalogIdentityKey {
    param($Value)

    return "{0}|{1}" -f ([string]$Value.guid).ToLowerInvariant(),([string]$Value.variant)
}

function Get-ReachableCatalogArtifactKeys {
    param(
        $Root,
        [hashtable]$ArtifactsByIdentity
    )

    $reachable = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $pending = [System.Collections.Generic.Queue[string]]::new()
    $pending.Enqueue((Get-CatalogIdentityKey $Root))
    while ($pending.Count -gt 0) {
        $key = $pending.Dequeue()
        if (-not $reachable.Add($key)) {
            continue
        }
        if (-not $ArtifactsByIdentity.ContainsKey($key)) {
            throw "Runtime catalog closure references missing artifact '$key'."
        }

        foreach ($dependency in @($ArtifactsByIdentity[$key].dependencies)) {
            $pending.Enqueue((Get-CatalogIdentityKey $dependency))
        }
    }

    return ,$reachable
}

function Assert-SourceIndependentRun {
    param(
        [string]$CaseName,
        [string]$Output
    )

    $workspaceManifestPath = Join-Path $workspaceRootPath "manifest.json"
    $workspaceCachePath = Join-Path $workspaceRootPath ".arisen\Cache"
    $workspaceSourcePattern = "(?i){0}[^\r\n]*(?:package\.json|AssetManifest\.json|\.arisenscene|\.aristerrain|\.ariterrainlayers|\.ariterraingenerated|\.ariweights|\.pgm|\.hlsl|\.shaderlab)" -f
        [Regex]::Escape($workspaceRootPath)
    if ($Output.IndexOf($workspaceManifestPath, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $Output.IndexOf($workspaceCachePath, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $Output -match $workspaceSourcePattern -or
        $Output.IndexOf("file://Local/", [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $Output.IndexOf("file://../", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Relocated Production $CaseName run accessed workspace, source-package, or mutable-cache state."
    }
}

function Assert-TerrainShutdown {
    param([string]$Output)

    if ($Output.IndexOf(
            "[PackageSubsystem] Error unloading package",
            [StringComparison]::Ordinal) -ge 0) {
        throw "Relocated Production terrain-streaming run reported a package unload failure."
    }

    $activationIndex = $Output.IndexOf("[GenericRP.Features] Froze ", [StringComparison]::Ordinal)
    $releaseIndex = $Output.IndexOf(
        "[Terrain.GenericRP] Feature device-resource release completed.",
        [StringComparison]::Ordinal)
    $destroyIndex = $Output.IndexOf(
        "[RHILoader::DestroyCurrentInstance] Destroying active RHI instance.",
        [StringComparison]::Ordinal)
    $unregisterIndex = $Output.IndexOf(
        "[Terrain.GenericRP] Render feature unregistered.",
        [StringComparison]::Ordinal)
    $vulkanUnloadIndex = $Output.IndexOf(
        "[VulkanRHIPackage] Unloaded Vulkan RHI backend.",
        [StringComparison]::Ordinal)
    if ($activationIndex -lt 0 -or
        $releaseIndex -lt 0 -or
        $destroyIndex -lt 0 -or
        $releaseIndex -ge $destroyIndex -or
        $unregisterIndex -lt 0 -or
        $vulkanUnloadIndex -lt 0 -or
        $unregisterIndex -ge $vulkanUnloadIndex) {
        throw "Relocated Production terrain resources did not release in the required package/RHI shutdown order."
    }
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
        $_.Extension -in @(
            ".arisenscene",
            ".scene",
            ".yaml",
            ".yml",
            ".meta",
            ".aristerrain",
            ".ariterrainlayers",
            ".ariterraingenerated",
            ".ariweights",
            ".pgm",
            ".hlsl",
            ".shaderlab",
            ".cs")
    }
    if (@($forbiddenSourceFiles).Count -ne 0) {
        throw "Relocated output contains authoring/cache files: $($forbiddenSourceFiles.FullName -join ', ')"
    }
    $checks.sourceFilesAbsent = $true

    $catalogPath = Join-Path $relocatedRoot "runtime-assets.json"
    $catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
    $artifactsByIdentity = @{}
    foreach ($catalogArtifact in @($catalog.artifacts)) {
        $key = Get-CatalogIdentityKey $catalogArtifact
        if ($artifactsByIdentity.ContainsKey($key)) {
            throw "Runtime catalog contains duplicate artifact identity '$key'."
        }
        $artifactsByIdentity.Add($key, $catalogArtifact)
    }

    $startupWorldRoot = @($catalog.roots | Where-Object { $_.name -ceq "startupWorld" })
    $renderPipelineRoot = @($catalog.roots | Where-Object { $_.name -ceq "renderPipeline" })
    if ($startupWorldRoot.Count -ne 1 -or $renderPipelineRoot.Count -ne 1) {
        throw "Runtime catalog must contain exactly one startupWorld and renderPipeline root."
    }
    $worldReachable = Get-ReachableCatalogArtifactKeys $startupWorldRoot[0] $artifactsByIdentity
    $pipelineReachable = Get-ReachableCatalogArtifactKeys $renderPipelineRoot[0] $artifactsByIdentity

    $terrainRoots = @($catalog.artifacts | Where-Object {
        $_.assetType -ceq "TerrainRoot" -and
        $_.variant -ceq "runtime.terrain-root.v2"
    })
    if ($terrainRoots.Count -ne 1) {
        throw "Runtime catalog must contain exactly one runtime.terrain-root.v2 artifact."
    }
    $terrainRoot = $terrainRoots[0]
    if ([int]$terrainRoot.formatVersion -ne 2) {
        throw "Runtime terrain root must use cooked format version 2."
    }
    if (-not $worldReachable.Contains((Get-CatalogIdentityKey $terrainRoot))) {
        throw "Startup world closure does not reach the terrain root artifact."
    }

    $terrainTileDependencies = @($terrainRoot.dependencies | Where-Object {
        $_.assetType -ceq "TerrainTile" -and
        $_.variant -ceq "runtime.terrain-tile.v1"
    })
    $terrainTextureDependencies = @($terrainRoot.dependencies | Where-Object {
        $_.assetType -ceq "Texture2D"
    })
    if ($terrainTileDependencies.Count -ne 4) {
        throw "Canonical terrain root must require exactly four cooked terrain tiles."
    }
    if ($terrainTextureDependencies.Count -ne 9) {
        throw "Canonical terrain root must close over exactly nine layer textures."
    }
    $expectedTerrainTextureVariants = [ordered]@{
        "r8g8b8a8unorm.srgb.mips" = 3
        "r8g8b8a8unorm.linear.mips.normalmap" = 3
        "r8g8b8a8unorm.linear.mips" = 3
    }
    foreach ($expectedVariant in $expectedTerrainTextureVariants.Keys) {
        $variantCount = @($terrainTextureDependencies | Where-Object {
            $_.variant -ceq $expectedVariant
        }).Count
        if ($variantCount -ne $expectedTerrainTextureVariants[$expectedVariant]) {
            throw "Canonical terrain root must require exactly $($expectedTerrainTextureVariants[$expectedVariant]) '$expectedVariant' textures; found $variantCount."
        }
    }

    foreach ($dependency in @($terrainTileDependencies + $terrainTextureDependencies)) {
        $dependencyKey = Get-CatalogIdentityKey $dependency
        if (-not [bool]$dependency.required -or
            -not $artifactsByIdentity.ContainsKey($dependencyKey) -or
            -not $worldReachable.Contains($dependencyKey)) {
            throw "Terrain dependency '$dependencyKey' is not required, resolved, and startup-world reachable."
        }
    }
    foreach ($textureDependency in $terrainTextureDependencies) {
        $textureArtifact = $artifactsByIdentity[(Get-CatalogIdentityKey $textureDependency)]
        if ([int]$textureArtifact.formatVersion -ne 2) {
            throw "Terrain texture '$((Get-CatalogIdentityKey $textureDependency))' must use cooked format version 2."
        }
    }

    $terrainTiles = @($catalog.artifacts | Where-Object {
        $_.assetType -ceq "TerrainTile" -and
        $_.variant -ceq "runtime.terrain-tile.v1"
    })
    if ($terrainTiles.Count -ne $terrainTileDependencies.Count) {
        throw "Runtime catalog contains stale or missing generated terrain tile rows."
    }
    $referencedTerrainTileKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($dependency in $terrainTileDependencies) {
        [void]$referencedTerrainTileKeys.Add((Get-CatalogIdentityKey $dependency))
    }
    foreach ($terrainTile in $terrainTiles) {
        if (-not $referencedTerrainTileKeys.Contains((Get-CatalogIdentityKey $terrainTile))) {
            throw "Runtime catalog contains an unreferenced generated terrain tile artifact."
        }
    }
    if (@($catalog.artifacts | Where-Object { $_.assetType -ceq "TerrainLayerSet" }).Count -ne 0) {
        throw "Terrain layer-set authoring assets must be embedded into the cooked root, not deployed."
    }

    $terrainShaderExpectations = @(
        [pscustomobject]@{
            Guid = "8f158c91-8c95-4bf4-9fc4-8ef5d777f3c1"
            EntryPoints = @("PSMain", "VSMain")
        },
        [pscustomobject]@{
            Guid = "26dd7ce8-e574-4ab2-8ab7-5e6e26f361b5"
            EntryPoints = @("VSMain")
        }
    )
    $terrainShaders = @()
    foreach ($expectation in $terrainShaderExpectations) {
        $matchingShaders = @($catalog.artifacts | Where-Object {
            ([string]$_.guid) -ieq $expectation.Guid -and
            $_.packageId -ceq "com.arisen.terrain.generic-renderpipeline" -and
            $_.assetType -ceq "ShaderSource"
        })
        if ($matchingShaders.Count -ne $expectation.EntryPoints.Count) {
            throw "Runtime catalog has an unexpected cooked stage count for terrain shader '$($expectation.Guid)'."
        }
        foreach ($entryPoint in $expectation.EntryPoints) {
            $entryPointCount = @($matchingShaders | Where-Object {
                ([string]$_.variant) -cmatch "\.$([Regex]::Escape($entryPoint))$"
            }).Count
            if ($entryPointCount -ne 1) {
                throw "Runtime catalog must contain exactly one '$entryPoint' stage for terrain shader '$($expectation.Guid)'."
            }
        }
        $terrainShaders += $matchingShaders
    }
    if ($terrainShaders.Count -ne 3) {
        throw "Runtime catalog must contain exactly three cooked terrain shader stages."
    }
    foreach ($terrainShader in $terrainShaders) {
        $shaderKey = Get-CatalogIdentityKey $terrainShader
        if (-not $pipelineReachable.Contains($shaderKey)) {
            throw "Render-pipeline closure does not reach terrain shader artifact '$shaderKey'."
        }
    }
    $checks.terrainClosureComplete = $true

    $executable = Join-Path $relocatedRoot "PackageGame.exe"
    $success = Get-RunOutput -CaseName "boot" -ExecutablePath $executable -RequireEmptyVulkanLog
    if ($success.ExitCode -ne 0) {
        throw "Relocated Production boot failed with exit code $($success.ExitCode)."
    }
    Assert-SourceIndependentRun -CaseName "boot" -Output $success.Output
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
    $terrainSubmissionMarker = "[Terrain.GenericRP] Submitted terrain draw commands"
    $terrainSubmissionPattern = [Regex]::Escape($terrainSubmissionMarker) +
        ' \| Draws: ([1-9][0-9]*) \| Ticket: ([1-9][0-9]*)\.'
    $terrainSubmission = [Regex]::Match(
        $worldStreaming.Output,
        $terrainSubmissionPattern)
    if (-not $terrainSubmission.Success) {
        throw "Relocated Production world-streaming smoke did not submit a positive terrain draw count."
    }
    Assert-SourceIndependentRun -CaseName "world-streaming" -Output $worldStreaming.Output
    & (Join-Path $PSScriptRoot "validate_world_streaming_summary.ps1") `
        -SummaryPath $worldStreamingSummaryPath `
        -ExpectedProfile "Production" `
        -ExpectedVisualCaptureCount 7
    if ($LASTEXITCODE -ne 0) {
        throw "Relocated Production world-streaming summary validation failed."
    }
    & (Join-Path $PSScriptRoot "validate_cascaded_shadow_visuals.ps1") `
        -SummaryPath $worldStreamingSummaryPath `
        -LogDirectory (Join-Path $relocatedRoot "logs") `
        -ExpectedCascadeCount 4
    if ($LASTEXITCODE -ne 0) {
        throw "Relocated Production cascaded-shadow visual validation failed."
    }
    & (Join-Path $PSScriptRoot "validate_outdoor_atmosphere_visuals.ps1") `
        -SummaryPath $worldStreamingSummaryPath `
        -LogDirectory (Join-Path $relocatedRoot "logs")
    if ($LASTEXITCODE -ne 0) {
        throw "Relocated Production outdoor-atmosphere visual validation failed."
    }
    $checks.worldStreamingSmokePassed = $true
    $checks.worldStreamingVisualsPassed = $true

    $worldStreamingSummaryArtifact = Join-Path $summaryDirectory "relocated-production-world-streaming.json"
    Copy-Item -LiteralPath $worldStreamingSummaryPath -Destination $worldStreamingSummaryArtifact -Force
    foreach ($checkpoint in @(
        "before",
        "during",
        "shadow-near",
        "shadow-mid",
        "shadow-far",
        "shadow-far-stable",
        "after")) {
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

    $terrainStreamingSummaryPath = Join-Path $relocatedRoot "logs\terrain-streaming-summary-Production.json"
    $terrainStreamingVisualBasePath = Join-Path $relocatedRoot "logs\terrain-streaming-visual-Production.json"
    $terrainArguments = '--smoke-mode terrain-streaming --frames 1 ' +
        '--smoke-summary-output "{0}" --visual-summary --visual-summary-output "{1}"' -f
        $terrainStreamingSummaryPath,$terrainStreamingVisualBasePath
    $terrainStreaming = Get-RunOutput `
        -CaseName "terrain-streaming" `
        -ExecutablePath $executable `
        -Arguments $terrainArguments `
        -RequireEmptyVulkanLog
    if ($terrainStreaming.ExitCode -ne 0) {
        throw "Relocated Production terrain-streaming smoke failed with exit code $($terrainStreaming.ExitCode)."
    }
    $terrainSubmission = [Regex]::Match(
        $terrainStreaming.Output,
        $terrainSubmissionPattern)
    if (-not $terrainSubmission.Success) {
        throw "Relocated Production terrain-streaming smoke did not submit a positive terrain draw count."
    }
    Assert-SourceIndependentRun -CaseName "terrain-streaming" -Output $terrainStreaming.Output
    $checks.terrainStreamingSourceIndependent = $true
    Assert-TerrainShutdown -Output $terrainStreaming.Output
    $checks.terrainStreamingShutdownPassed = $true
    & (Join-Path $PSScriptRoot "validate_terrain_streaming_summary.ps1") `
        -SummaryPath $terrainStreamingSummaryPath `
        -ExpectedProfile "Production" `
        -ExpectedVisualCaptureCount 5
    if ($LASTEXITCODE -ne 0) {
        throw "Relocated Production terrain-streaming summary validation failed."
    }
    $checks.terrainStreamingSmokePassed = $true
    $checks.terrainStreamingVisualsPassed = $true
    $checks.vulkanValidationLogsEmpty = $true

    $terrainStreamingSummaryArtifact = Join-Path $summaryDirectory "relocated-production-terrain-streaming.json"
    Copy-Item -LiteralPath $terrainStreamingSummaryPath -Destination $terrainStreamingSummaryArtifact -Force
    foreach ($checkpoint in @(
        "near",
        "boundary-mixed-lod",
        "far-cascade",
        "post-rebase",
        "returned-start")) {
        $sourceVisual = Join-Path $relocatedRoot "logs\terrain-streaming-visual-Production.$checkpoint.json"
        $destinationVisual = Join-Path $summaryDirectory "relocated-production-terrain-streaming.$checkpoint.json"
        Copy-Item -LiteralPath $sourceVisual -Destination $destinationVisual -Force
        $terrainStreamingVisualArtifacts += $destinationVisual
    }
    $preservedTerrainSummary = Get-Content -LiteralPath $terrainStreamingSummaryArtifact -Raw | ConvertFrom-Json
    foreach ($capture in @($preservedTerrainSummary.visualCaptures)) {
        $capture.capture.outputPath = @(
            $terrainStreamingVisualArtifacts |
                Where-Object { $_.EndsWith(".$($capture.capture.name).json", [StringComparison]::OrdinalIgnoreCase) })[0]
    }
    $preservedTerrainSummary |
        ConvertTo-Json -Depth 12 |
        Set-Content -LiteralPath $terrainStreamingSummaryArtifact -Encoding UTF8

    $artifact = @($terrainTiles | Sort-Object guid, variant | Select-Object -First 1)[0]
    if ($null -eq $artifact) {
        throw "Runtime catalog has no terrain tile artifact available for tamper validation."
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
        schemaVersion = 4
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
        terrainStreamingSummaryArtifact = $terrainStreamingSummaryArtifact
        terrainStreamingVisualArtifacts = $terrainStreamingVisualArtifacts
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

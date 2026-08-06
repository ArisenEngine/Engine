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
    vegetationClosureComplete = $false
    relocatedBootPassed = $false
    cookedSceneObserved = $false
    worldStreamingSmokePassed = $false
    worldStreamingVisualsPassed = $false
    terrainStreamingSmokePassed = $false
    terrainStreamingVisualsPassed = $false
    terrainStreamingSourceIndependent = $false
    vegetationStreamingSubmissionPassed = $false
    terrainStreamingShutdownPassed = $false
    vegetationStreamingShutdownPassed = $false
    vegetationVisualDisabledRunPassed = $false
    vegetationVisualOpaqueOnlyRunPassed = $false
    vegetationVisualComparisonPassed = $false
    vulkanValidationLogsEmpty = $false
    tamperRejected = $false
    missingArtifactRejected = $false
}
$artifactIdentity = $null
$worldStreamingSummaryArtifact = $null
$worldStreamingVisualArtifacts = @()
$terrainStreamingSummaryArtifact = $null
$terrainStreamingVisualArtifacts = @()
$vegetationVisualComparisonArtifacts = [ordered]@{
    disabledSummary = $null
    disabledDuringVisual = $null
    opaqueOnlySummary = $null
    opaqueOnlyDuringVisual = $null
    fullSummary = $null
    fullDuringVisual = $null
}
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

function Get-ExactCatalogArtifact {
    param(
        [object[]]$Artifacts,
        [string]$Guid,
        [string]$PackageId,
        [string]$AssetType,
        [string]$Variant,
        [int]$FormatVersion
    )

    $matches = @($Artifacts | Where-Object {
        ([string]$_.guid) -ieq $Guid -and
        ([string]$_.packageId) -ceq $PackageId -and
        ([string]$_.assetType) -ceq $AssetType -and
        ([string]$_.variant) -ceq $Variant -and
        [int]$_.formatVersion -eq $FormatVersion
    })
    if ($matches.Count -ne 1) {
        throw "Runtime catalog must contain exactly one '$Guid|$Variant' $AssetType artifact " +
            "owned by '$PackageId' at format version $FormatVersion."
    }

    return $matches[0]
}

function Assert-ExactRequiredCatalogDependencies {
    param(
        $Owner,
        [object[]]$ExpectedDependencies,
        [string]$OwnerName
    )

    $actualDependencies = @($Owner.dependencies)
    if ($actualDependencies.Count -ne $ExpectedDependencies.Count) {
        throw "Canonical $OwnerName must have exactly $($ExpectedDependencies.Count) required dependencies; " +
            "found $($actualDependencies.Count)."
    }

    foreach ($expected in $ExpectedDependencies) {
        $matches = @($actualDependencies | Where-Object {
            ([string]$_.guid) -ieq ([string]$expected.guid) -and
            ([string]$_.packageId) -ceq ([string]$expected.packageId) -and
            ([string]$_.assetType) -ceq ([string]$expected.assetType) -and
            ([string]$_.variant) -ceq ([string]$expected.variant) -and
            [bool]$_.required
        })
        if ($matches.Count -ne 1) {
            throw "Canonical $OwnerName does not require exact dependency " +
                "'$((Get-CatalogIdentityKey $expected))'."
        }
    }
}

function Assert-SourceIndependentRun {
    param(
        [string]$CaseName,
        [string]$Output
    )

    $workspaceManifestPath = Join-Path $workspaceRootPath "manifest.json"
    $workspaceCachePath = Join-Path $workspaceRootPath ".arisen\Cache"
    $workspaceSourcePattern = "(?i){0}[^\r\n]*(?:package\.json|AssetManifest\.json|\.arisenscene|\.aristerrain|\.ariterrainlayers|\.ariterraingenerated|\.ariweights|\.arivegetationspecies|\.arivegetationbiome|\.arivegetationscatter|\.arivegetationgenerated|\.pgm|\.hlsl|\.shaderlab)" -f
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

function Assert-VegetationShutdown {
    param([string]$Output)

    if ($Output.IndexOf(
            "[PackageSubsystem] Error unloading package",
            [StringComparison]::Ordinal) -ge 0) {
        throw "Relocated Production vegetation run reported a package unload failure."
    }

    $releaseIndex = $Output.IndexOf(
        "[Vegetation.GenericRP] Feature device-resource release completed.",
        [StringComparison]::Ordinal)
    $destroyIndex = $Output.IndexOf(
        "[RHILoader::DestroyCurrentInstance] Destroying active RHI instance.",
        [StringComparison]::Ordinal)
    $unregisterIndex = $Output.IndexOf(
        "[GenericRP.Features] Unregistering feature 'com.arisen.vegetation.generic-renderpipeline'.",
        [StringComparison]::Ordinal)
    $vulkanUnloadIndex = $Output.IndexOf(
        "[VulkanRHIPackage] Unloaded Vulkan RHI backend.",
        [StringComparison]::Ordinal)
    if ($releaseIndex -lt 0 -or
        $destroyIndex -lt 0 -or
        $releaseIndex -ge $destroyIndex -or
        $unregisterIndex -lt 0 -or
        $releaseIndex -ge $unregisterIndex -or
        $vulkanUnloadIndex -lt 0 -or
        $unregisterIndex -ge $vulkanUnloadIndex) {
        throw "Relocated Production vegetation resources did not release in the required feature/package/RHI shutdown order."
    }
}

function Assert-VegetationSubmission {
    param(
        [string]$CaseName,
        [string]$Output,
        [int]$RequiredDistinctSurfaceCount = 1
    )

    $marker = "[Vegetation.GenericRP.Validation]"
    $pattern = [Regex]::Escape($marker) +
        ' Surface=0x(?<surface>[0-9A-F]+) Frame=[0-9]+ ' +
        'DeviceGeneration=[0-9]+ Revision=[0-9]+ PreparedClusters=1 ' +
        'Cluster=e90ae5ab-24fb-2617-9983-3ed656bd652c ' +
        'Species=7b0f2e52-8b67-4e3d-bf0a-cbc42f622001 ' +
        'OpaqueBatches=1 OpaqueInstances=13 ' +
        'RecordedShadowBatches=4 RecordedShadowInstances=52 Cascades=4 ' +
        'ShadowBatches=1,1,1,1 ShadowInstances=13,13,13,13 ' +
        'Dropped=0 Ticket=[1-9][0-9]*'
    $markerCount = [Regex]::Matches($Output, [Regex]::Escape($marker)).Count
    $submissions = [Regex]::Matches(
        $Output,
        $pattern,
        [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($markerCount -eq 0 -or $submissions.Count -ne $markerCount) {
        throw "Relocated Production $CaseName vegetation validation records do not all " +
            "match the canonical cluster/species and exact opaque/shadow counts."
    }

    $surfaces = [System.Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($submission in $submissions) {
        [void]$surfaces.Add($submission.Groups["surface"].Value)
    }
    if ($surfaces.Count -lt $RequiredDistinctSurfaceCount) {
        throw "Relocated Production $CaseName vegetation validation requires " +
            "$RequiredDistinctSurfaceCount distinct surface record(s); found $($surfaces.Count)."
    }
}

function Get-RunOutput {
    param(
        [string]$CaseName,
        [string]$ExecutablePath,
        [string]$Arguments = "--smoke-mode $SmokeMode --frames $Frames",
        [ValidateSet("disabled", "opaque-only", "full")]
        [string]$VegetationRenderValidationMode = "full",
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
    $processInfo.EnvironmentVariables[
        "ARISEN_VEGETATION_RENDER_VALIDATION_MODE"] = $VegetationRenderValidationMode
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
    $playerLogs = @()
    if (Test-Path -LiteralPath $runtimeLogs) {
        $playerLogs = @(Get-ChildItem -LiteralPath $runtimeLogs -Filter "player_*.log" -File)
    }
    $launchPlayerLog = $null
    $launchPlayerLogText = $null
    if ($RequireEmptyVulkanLog) {
        if ($playerLogs.Count -ne 1) {
            $process.Dispose()
            throw "Relocated Production $CaseName run must produce exactly one launch-owned player log; " +
                "found $($playerLogs.Count)."
        }
        $launchPlayerLog = $playerLogs[0]
    }
    elseif ($playerLogs.Count -gt 0) {
        $launchPlayerLog = @($playerLogs | Sort-Object LastWriteTimeUtc -Descending)[0]
    }
    if ($null -ne $launchPlayerLog) {
        $launchPlayerLogText = Get-Content -LiteralPath $launchPlayerLog.FullName -Raw
        $outputParts += $launchPlayerLogText
    }

    $combined = $outputParts -join [Environment]::NewLine
    $process.Dispose()
    Write-ValidationLog "[Arisen] Relocated Production $CaseName exit code: $exitCode"
    Add-Content -LiteralPath $logPathFull -Value $combined
    if ($RequireEmptyVulkanLog) {
        if (-not (Test-Path -LiteralPath $validationLog -PathType Leaf)) {
            throw "Relocated Production $CaseName run produced no Vulkan validation log."
        }
        if ((Get-Item -LiteralPath $validationLog).Length -ne 0) {
            $validationText = Get-Content -LiteralPath $validationLog -Raw
            Add-Content -LiteralPath $logPathFull -Value $validationText
            throw "Relocated Production $CaseName Vulkan validation log is not empty."
        }
        foreach ($marker in @("vk message warning:", "vk message error:")) {
            if ($launchPlayerLogText.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "Relocated Production $CaseName launch-owned player log contains Vulkan " +
                    "warning/error marker '$marker': $($launchPlayerLog.FullName)"
            }
        }
    }
    $launchPlayerLogPath = $null
    if ($null -ne $launchPlayerLog) {
        $launchPlayerLogPath = $launchPlayerLog.FullName
    }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $combined
        PlayerLogPath = $launchPlayerLogPath
        PlayerLogText = $launchPlayerLogText
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
            ".arivegetationscatter",
            ".arivegetationgenerated",
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

    $vegetationExpectations = [ordered]@{
        Cluster = [pscustomobject]@{
            Guid = "e90ae5ab-24fb-2617-9983-3ed656bd652c"
            PackageId = "com.arisen.packagegame"
            AssetType = "VegetationCluster"
            Variant = "runtime.vegetation-cluster.v1"
            FormatVersion = 1
        }
        Page = [pscustomobject]@{
            Guid = "c1d7d00e-4aac-3819-b9f5-7a2a65e8e1eb"
            PackageId = "com.arisen.packagegame"
            AssetType = "VegetationInstancePage"
            Variant = "runtime.vegetation-instance-page.v1"
            FormatVersion = 1
        }
        Biome = [pscustomobject]@{
            Guid = "c0a92f10-0eb9-4d24-b729-7d0f38313001"
            PackageId = "com.arisen.packagegame"
            AssetType = "VegetationBiome"
            Variant = "runtime.vegetation-biome.v1"
            FormatVersion = 1
        }
        Species = [pscustomobject]@{
            Guid = "7b0f2e52-8b67-4e3d-bf0a-cbc42f622001"
            PackageId = "com.arisen.packagegame"
            AssetType = "VegetationSpecies"
            Variant = "runtime.vegetation-species.v1"
            FormatVersion = 1
        }
        Mesh = [pscustomobject]@{
            Guid = "9f57d9cc-2db6-4c85-ae7b-544338806e2c"
            PackageId = "com.arisen.generic-renderpipeline"
            AssetType = "Mesh"
            Variant = "staticmesh.uint32"
            FormatVersion = 4
        }
        Material = [pscustomobject]@{
            Guid = "4ac21c64-e984-4ed0-9e21-93878de5249e"
            PackageId = "com.arisen.generic-renderpipeline"
            AssetType = "Material"
            Variant = "material.runtime"
            FormatVersion = 7
        }
    }
    $vegetationArtifacts = @{}
    foreach ($name in $vegetationExpectations.Keys) {
        $expected = $vegetationExpectations[$name]
        $vegetationArtifacts[$name] = Get-ExactCatalogArtifact `
            -Artifacts @($catalog.artifacts) `
            -Guid $expected.Guid `
            -PackageId $expected.PackageId `
            -AssetType $expected.AssetType `
            -Variant $expected.Variant `
            -FormatVersion $expected.FormatVersion
    }

    Assert-ExactRequiredCatalogDependencies `
        -Owner $vegetationArtifacts.Cluster `
        -ExpectedDependencies @(
            $vegetationArtifacts.Species,
            $vegetationArtifacts.Biome,
            $vegetationArtifacts.Page) `
        -OwnerName "vegetation cluster"
    Assert-ExactRequiredCatalogDependencies `
        -Owner $vegetationArtifacts.Page `
        -ExpectedDependencies @($vegetationArtifacts.Species) `
        -OwnerName "vegetation instance page"
    Assert-ExactRequiredCatalogDependencies `
        -Owner $vegetationArtifacts.Biome `
        -ExpectedDependencies @($vegetationArtifacts.Species) `
        -OwnerName "vegetation biome"
    Assert-ExactRequiredCatalogDependencies `
        -Owner $vegetationArtifacts.Species `
        -ExpectedDependencies @(
            $vegetationArtifacts.Mesh,
            $vegetationArtifacts.Material) `
        -OwnerName "vegetation species"

    foreach ($name in @("Cluster", "Page", "Biome", "Species", "Mesh", "Material")) {
        $artifactKey = Get-CatalogIdentityKey $vegetationArtifacts[$name]
        if (-not $worldReachable.Contains($artifactKey)) {
            throw "Startup-world closure does not reach canonical vegetation $name artifact '$artifactKey'."
        }
    }

    $vegetationRuntimeAssetTypes = @(
        "VegetationCluster",
        "VegetationInstancePage",
        "VegetationBiome",
        "VegetationSpecies")
    $vegetationRuntimeArtifacts = @($catalog.artifacts | Where-Object {
        $vegetationRuntimeAssetTypes -ccontains ([string]$_.assetType)
    })
    if ($vegetationRuntimeArtifacts.Count -ne 4) {
        throw "Runtime catalog must contain exactly four canonical cooked vegetation artifacts."
    }
    $expectedVegetationRuntimeKeys = [System.Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal)
    foreach ($name in @("Cluster", "Page", "Biome", "Species")) {
        [void]$expectedVegetationRuntimeKeys.Add(
            (Get-CatalogIdentityKey $vegetationArtifacts[$name]))
    }
    foreach ($runtimeArtifact in $vegetationRuntimeArtifacts) {
        if (-not $expectedVegetationRuntimeKeys.Contains(
                (Get-CatalogIdentityKey $runtimeArtifact))) {
            throw "Runtime catalog contains an unreferenced cooked vegetation artifact."
        }
    }

    $vegetationShaderExpectations = @(
        [pscustomobject]@{
            Guid = "2a536b1f-81cf-4d91-a84f-39bc6f7e15a2"
            EntryPoints = @("PSMain", "VSMain")
        },
        [pscustomobject]@{
            Guid = "9d7a4c3e-f2b6-46a1-8c59-5e1087b34d20"
            EntryPoints = @("VSMain")
        }
    )
    $vegetationShaders = @()
    foreach ($expectation in $vegetationShaderExpectations) {
        $matchingShaders = @($catalog.artifacts | Where-Object {
            ([string]$_.guid) -ieq $expectation.Guid -and
            ([string]$_.packageId) -ceq "com.arisen.vegetation.generic-renderpipeline" -and
            ([string]$_.assetType) -ceq "ShaderSource" -and
            [int]$_.formatVersion -eq 1
        })
        if ($matchingShaders.Count -ne $expectation.EntryPoints.Count) {
            throw "Runtime catalog has an unexpected cooked stage count for vegetation shader '$($expectation.Guid)'."
        }
        foreach ($entryPoint in $expectation.EntryPoints) {
            $entryPointCount = @($matchingShaders | Where-Object {
                ([string]$_.variant) -cmatch "\.$([Regex]::Escape($entryPoint))$"
            }).Count
            if ($entryPointCount -ne 1) {
                throw "Runtime catalog must contain exactly one '$entryPoint' stage for vegetation shader '$($expectation.Guid)'."
            }
        }
        $vegetationShaders += $matchingShaders
    }
    if ($vegetationShaders.Count -ne 3) {
        throw "Runtime catalog must contain exactly three cooked vegetation shader stages."
    }
    foreach ($vegetationShader in $vegetationShaders) {
        $shaderKey = Get-CatalogIdentityKey $vegetationShader
        if (-not $pipelineReachable.Contains($shaderKey)) {
            throw "Render-pipeline closure does not reach vegetation shader artifact '$shaderKey'."
        }
    }

    $expectedVegetationPaths = [System.Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal)
    foreach ($vegetationArtifact in @($vegetationRuntimeArtifacts + $vegetationShaders)) {
        $catalogPath = ([string]$vegetationArtifact.path).Replace('\', '/')
        if (-not $expectedVegetationPaths.Add($catalogPath)) {
            throw "Runtime catalog contains duplicate vegetation deployment path '$catalogPath'."
        }
    }
    if ($expectedVegetationPaths.Count -ne 7) {
        throw "Runtime catalog vegetation deployment closure must contain exactly seven files."
    }

    $contentRoot = Join-Path $relocatedRoot "Content"
    $vegetationCookedExtensions = @(
        ".arivegetationcluster",
        ".arivegetationpage",
        ".arivegetationbiome",
        ".arivegetationspecies")
    $deployedVegetationPaths = @()
    foreach ($contentFile in @(Get-ChildItem -LiteralPath $contentRoot -File -Recurse)) {
        $relativePath = $contentFile.FullName.Substring($contentRoot.Length)
        $relativePath = $relativePath.TrimStart([char[]]"\/").Replace('\', '/')
        if ($vegetationCookedExtensions -contains $contentFile.Extension -or
            $relativePath.StartsWith(
                "com.arisen.vegetation.generic-renderpipeline/",
                [StringComparison]::Ordinal)) {
            $deployedVegetationPaths += $relativePath
        }
    }
    if ($deployedVegetationPaths.Count -ne $expectedVegetationPaths.Count) {
        throw "Relocated Content must contain exactly the seven catalog-referenced vegetation files."
    }
    foreach ($deployedPath in $deployedVegetationPaths) {
        if (-not $expectedVegetationPaths.Contains($deployedPath)) {
            throw "Relocated Content contains unreferenced vegetation file '$deployedPath'."
        }
    }
    foreach ($expectedPath in $expectedVegetationPaths) {
        if ($deployedVegetationPaths -cnotcontains $expectedPath) {
            throw "Relocated Content is missing catalog-referenced vegetation file '$expectedPath'."
        }
    }
    $checks.vegetationClosureComplete = $true

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
        -VegetationRenderValidationMode "full" `
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
    Assert-VegetationSubmission -CaseName "world-streaming" -Output $worldStreaming.Output
    $checks.vegetationStreamingSubmissionPassed = $true
    Assert-SourceIndependentRun -CaseName "world-streaming" -Output $worldStreaming.Output
    Assert-VegetationShutdown -Output $worldStreaming.Output
    $checks.vegetationStreamingShutdownPassed = $true
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

    $disabledVegetationSummaryPath = Join-Path `
        $summaryDirectory `
        "relocated-production-vegetation-rendering-disabled-summary.json"
    $disabledVegetationVisualBasePath = Join-Path `
        $summaryDirectory `
        "relocated-production-vegetation-rendering-disabled-visual.json"
    $opaqueOnlyVegetationSummaryPath = Join-Path `
        $summaryDirectory `
        "relocated-production-vegetation-rendering-opaque-only-summary.json"
    $opaqueOnlyVegetationVisualBasePath = Join-Path `
        $summaryDirectory `
        "relocated-production-vegetation-rendering-opaque-only-visual.json"
    $vegetationVisualModes = @(
        [pscustomobject]@{
            Mode = "disabled"
            SummaryPath = $disabledVegetationSummaryPath
            VisualBasePath = $disabledVegetationVisualBasePath
        },
        [pscustomobject]@{
            Mode = "opaque-only"
            SummaryPath = $opaqueOnlyVegetationSummaryPath
            VisualBasePath = $opaqueOnlyVegetationVisualBasePath
        })
    foreach ($visualMode in $vegetationVisualModes) {
        $modeArguments = '--smoke-mode world-streaming --frames 1 ' +
            '--smoke-summary-output "{0}" --visual-summary --visual-summary-output "{1}"' -f
            $visualMode.SummaryPath,$visualMode.VisualBasePath
        $modeRun = Get-RunOutput `
            -CaseName "vegetation-rendering-$($visualMode.Mode)" `
            -ExecutablePath $executable `
            -Arguments $modeArguments `
            -VegetationRenderValidationMode $visualMode.Mode `
            -RequireEmptyVulkanLog
        if ($modeRun.ExitCode -ne 0) {
            throw "Relocated Production vegetation $($visualMode.Mode) world-streaming smoke " +
                "failed with exit code $($modeRun.ExitCode)."
        }
        Assert-SourceIndependentRun `
            -CaseName "vegetation-rendering-$($visualMode.Mode)" `
            -Output $modeRun.Output
        Assert-VegetationShutdown -Output $modeRun.Output
        & (Join-Path $PSScriptRoot "validate_world_streaming_summary.ps1") `
            -SummaryPath $visualMode.SummaryPath `
            -ExpectedProfile "Production" `
            -ExpectedVisualCaptureCount 7
        if ($LASTEXITCODE -ne 0) {
            throw "Relocated Production vegetation $($visualMode.Mode) world-streaming " +
                "summary validation failed."
        }

        if ($visualMode.Mode -ceq "disabled") {
            $checks.vegetationVisualDisabledRunPassed = $true
        }
        else {
            $checks.vegetationVisualOpaqueOnlyRunPassed = $true
        }
    }

    $disabledVegetationDuringPath =
        "$([System.IO.Path]::ChangeExtension($disabledVegetationVisualBasePath, $null)).during.json"
    $opaqueOnlyVegetationDuringPath =
        "$([System.IO.Path]::ChangeExtension($opaqueOnlyVegetationVisualBasePath, $null)).during.json"
    $fullVegetationDuringPath = Join-Path `
        $summaryDirectory `
        "relocated-production-world-streaming.during.json"
    & (Join-Path $PSScriptRoot "validate_vegetation_rendering_visuals.ps1") `
        -DisabledSummaryPath $disabledVegetationSummaryPath `
        -OpaqueOnlySummaryPath $opaqueOnlyVegetationSummaryPath `
        -FullSummaryPath $worldStreamingSummaryArtifact `
        -ExpectedProfile "Production"
    if ($LASTEXITCODE -ne 0) {
        throw "Relocated Production vegetation rendering visual comparison failed."
    }
    $checks.vegetationVisualComparisonPassed = $true
    $vegetationVisualComparisonArtifacts.disabledSummary = $disabledVegetationSummaryPath
    $vegetationVisualComparisonArtifacts.disabledDuringVisual = $disabledVegetationDuringPath
    $vegetationVisualComparisonArtifacts.opaqueOnlySummary = $opaqueOnlyVegetationSummaryPath
    $vegetationVisualComparisonArtifacts.opaqueOnlyDuringVisual = $opaqueOnlyVegetationDuringPath
    $vegetationVisualComparisonArtifacts.fullSummary = $worldStreamingSummaryArtifact
    $vegetationVisualComparisonArtifacts.fullDuringVisual = $fullVegetationDuringPath

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
    Assert-VegetationSubmission -CaseName "terrain-streaming" -Output $terrainStreaming.Output
    $checks.vegetationStreamingSubmissionPassed = $true
    Assert-SourceIndependentRun -CaseName "terrain-streaming" -Output $terrainStreaming.Output
    $checks.terrainStreamingSourceIndependent = $true
    Assert-TerrainShutdown -Output $terrainStreaming.Output
    $checks.terrainStreamingShutdownPassed = $true
    Assert-VegetationShutdown -Output $terrainStreaming.Output
    $checks.vegetationStreamingShutdownPassed = $true
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
        schemaVersion = 6
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
        vegetationVisualComparisonArtifacts = $vegetationVisualComparisonArtifacts
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

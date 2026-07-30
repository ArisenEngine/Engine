param(
    [Parameter(Mandatory = $true)]
    [string]$SummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedProfile,

    [ValidateRange(0, 5)]
    [int]$ExpectedVisualCaptureCount = 5
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

function Assert-FiniteNumber {
    param(
        [object]$Value,
        [string]$Name
    )

    $number = [double]$Value
    Assert-Condition (-not [double]::IsNaN($number) -and -not [double]::IsInfinity($number)) `
        "$Name is not finite."
}

function Assert-Vector3 {
    param(
        [object]$Value,
        [string]$Name
    )

    Assert-Condition ($null -ne $Value) "$Name is missing."
    Assert-FiniteNumber $Value.x "$Name.x"
    Assert-FiniteNumber $Value.y "$Name.y"
    Assert-FiniteNumber $Value.z "$Name.z"
}

function Get-GridHalfAverage {
    param(
        [object[]]$Grid,
        [int]$Width,
        [int]$Height,
        [bool]$Top
    )

    $halfHeight = [int]($Height / 2)
    $firstRow = if ($Top) { 0 } else { $halfHeight }
    $lastRow = if ($Top) { $halfHeight - 1 } else { $Height - 1 }
    $sum = 0.0
    $count = 0
    for ($row = $firstRow; $row -le $lastRow; $row++) {
        for ($column = 0; $column -lt $Width; $column++) {
            $value = [double]$Grid[($row * $Width) + $column]
            Assert-Condition (-not [double]::IsNaN($value) -and -not [double]::IsInfinity($value)) `
                "Spatial grid contains a non-finite value."
            $sum += $value
            $count++
        }
    }

    return $sum / $count
}

function Get-MaxGridDifference {
    param(
        [object[]]$Left,
        [object[]]$Right
    )

    Assert-Condition ($Left.Count -eq $Right.Count) "Compared spatial grids have different sizes."
    $maximum = 0.0
    for ($index = 0; $index -lt $Left.Count; $index++) {
        $difference = [math]::Abs([double]$Left[$index] - [double]$Right[$index])
        $maximum = [math]::Max($maximum, $difference)
    }

    return $maximum
}

function Assert-VisualSimilarity {
    param(
        [object]$Left,
        [object]$Right,
        [string]$Description
    )

    $averageLuminanceDifference = [math]::Abs(
        [double]$Left.averageLuminance - [double]$Right.averageLuminance)
    $colorGridDifference = Get-MaxGridDifference `
        @($Left.spatialLuminanceGrid) `
        @($Right.spatialLuminanceGrid)
    $averageDepthDifference = [math]::Abs(
        [double]$Left.depth.averageDepth - [double]$Right.depth.averageDepth)
    $depthGridDifference = Get-MaxGridDifference `
        @($Left.depth.spatialDepthGrid) `
        @($Right.depth.spatialDepthGrid)
    $writtenDepthDifference = [math]::Abs(
        [long]$Left.depth.writtenDepthPixelCount -
        [long]$Right.depth.writtenDepthPixelCount)

    Assert-Condition ($averageLuminanceDifference -le 0.0001) `
        "$Description changed average luminance by $averageLuminanceDifference."
    Assert-Condition ($colorGridDifference -le 0.00025) `
        "$Description changed the spatial luminance grid by $colorGridDifference."
    Assert-Condition ($averageDepthDifference -le 0.00002) `
        "$Description changed average depth by $averageDepthDifference."
    Assert-Condition ($depthGridDifference -le 0.00003) `
        "$Description changed the spatial depth grid by $depthGridDifference."
    Assert-Condition ($writtenDepthDifference -le 16) `
        "$Description changed written-depth coverage by $writtenDepthDifference pixels."
}

try {
    $path = [System.IO.Path]::GetFullPath($SummaryPath)
    Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) `
        "Terrain-streaming summary was not produced: $path"
    $artifact = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json

    Assert-Condition ([int]$artifact.schemaVersion -eq 1) `
        "Terrain-streaming summary schema mismatch."
    Assert-Condition ([string]$artifact.mode -ceq "terrain-streaming") `
        "Terrain-streaming summary mode mismatch."
    Assert-Condition ([string]$artifact.profile -ceq $ExpectedProfile) `
        "Terrain-streaming profile mismatch. Expected '$ExpectedProfile', received '$($artifact.profile)'."
    Assert-Condition ($artifact.passed -eq $true) `
        "Terrain-streaming scenario failed: $($artifact.failure)"
    Assert-Condition ([string]::IsNullOrWhiteSpace([string]$artifact.failure)) `
        "A passing terrain-streaming summary contains a failure diagnostic."
    Assert-Condition ([string]$artifact.worldGuid -match '^[0-9A-Fa-f-]{36}$') `
        "Terrain-streaming summary has no valid world GUID."
    Assert-Condition ([string]$artifact.terrainRootGuid -match '^[0-9A-Fa-f-]{36}$') `
        "Terrain-streaming summary has no valid terrain-root GUID."
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string]$artifact.terrainCellId)) `
        "Terrain-streaming summary has no terrain owner cell."

    $requestedCycles = [int]$artifact.requestedSoakCycles
    $completedCycles = [int]$artifact.completedSoakCycles
    Assert-Condition ($requestedCycles -eq 4 -and $completedCycles -eq $requestedCycles) `
        "Terrain-streaming must complete exactly four requested soak cycles."
    Assert-Condition (@($artifact.rebaseSequences).Count -eq 1) `
        "Terrain-streaming must complete exactly one origin rebase."
    Assert-Condition ([long]@($artifact.rebaseSequences)[0] -gt 0) `
        "Terrain-streaming origin rebase sequence is invalid."
    Assert-Condition ($artifact.shutdownDrained -eq $true) `
        "Terrain-streaming shutdown did not drain all runtime state."
    Assert-Condition ([string]$artifact.terminalStage -ceq "ReadyForShutdown") `
        "Terrain-streaming terminated from '$($artifact.terminalStage)'."

    $drain = $artifact.lastDrain
    Assert-Condition ($null -ne $drain -and $drain.isDrained -eq $true) `
        "Terrain-streaming did not observe a fully drained soak boundary."
    Assert-Condition (
        ([string]$drain.cellState -ceq "Unloaded" -or
         [string]$drain.cellState -ceq "Cancelled") -and
        $drain.cellDesired -eq $false -and
        $drain.cellPinned -eq $false -and
        [string]$drain.cellDesiredSources -ceq "None") `
        "The final terrain cell remained desired or pinned."
    foreach ($field in @(
        "visibleTileCount",
        "runtimeRootCount",
        "runtimeTileCount",
        "diagnosticRootCount",
        "diagnosticTileCount",
        "diagnosticResourceCount",
        "terrainResidencyResourceCount",
        "terrainPendingDisposalCount",
        "totalPendingDisposalCount",
        "outstandingTaskCount")) {
        Assert-Condition ([long]$drain.$field -eq 0) `
            "Final drain field '$field' is not zero."
    }

    $expectedCheckpointNames = @(
        "near",
        "boundary-mixed-lod",
        "far-cascade",
        "post-rebase",
        "returned-start"
    )
    for ($cycle = 1; $cycle -le $requestedCycles; $cycle++) {
        $expectedCheckpointNames += "soak-load-$cycle"
        $expectedCheckpointNames += "soak-reload-$cycle"
    }

    $checkpoints = @($artifact.checkpoints)
    Assert-Condition ($checkpoints.Count -eq $expectedCheckpointNames.Count) `
        "Expected $($expectedCheckpointNames.Count) terrain checkpoints, received $($checkpoints.Count)."
    $checkpointByName = @{}
    $previousFrame = -1L
    foreach ($checkpoint in $checkpoints) {
        $name = [string]$checkpoint.name
        Assert-Condition (-not $checkpointByName.ContainsKey($name)) `
            "Terrain checkpoint '$name' is duplicated."
        Assert-Condition ([long]$checkpoint.frameIndex -gt $previousFrame) `
            "Terrain checkpoint frames are not strictly increasing."
        $previousFrame = [long]$checkpoint.frameIndex
        $checkpointByName[$name] = $checkpoint
    }
    foreach ($name in $expectedCheckpointNames) {
        Assert-Condition ($checkpointByName.ContainsKey($name)) `
            "Terrain-streaming summary is missing checkpoint '$name'."
    }

    $near = $checkpointByName["near"]
    $expectedTileCount = @($near.tiles).Count
    Assert-Condition ($expectedTileCount -gt 0) `
        "Terrain-streaming fixture contains no terrain tiles."
    $baselineMemory = $near.memory
    $peaks = $artifact.peaks

    foreach ($checkpoint in $checkpoints) {
        $name = [string]$checkpoint.name
        Assert-Condition ($checkpoint.passed -eq $true) `
            "Terrain checkpoint '$name' failed."
        Assert-Condition ([string]$checkpoint.terrainRootGuid -ceq [string]$artifact.terrainRootGuid) `
            "Terrain checkpoint '$name' changed root identity."
        Assert-Condition ([string]$checkpoint.terrainCellId -ceq [string]$artifact.terrainCellId) `
            "Terrain checkpoint '$name' changed owner-cell identity."

        $tiles = @($checkpoint.tiles)
        $queries = @($checkpoint.querySamples)
        Assert-Condition ($tiles.Count -eq $expectedTileCount) `
            "Terrain checkpoint '$name' has an unexpected tile count."
        Assert-Condition ([int]$checkpoint.ecsTileCount -eq $expectedTileCount) `
            "Terrain checkpoint '$name' has stale or missing ECS terrain tiles."
        Assert-Condition ($queries.Count -eq $expectedTileCount) `
            "Terrain checkpoint '$name' has an unexpected query-sample count."
        Assert-Condition (@($tiles | ForEach-Object { [string]$_.tileGuid } | Select-Object -Unique).Count -eq $expectedTileCount) `
            "Terrain checkpoint '$name' contains duplicate tile identities."
        Assert-Condition (@($tiles | ForEach-Object { "$($_.coordinate.x),$($_.coordinate.z)" } | Select-Object -Unique).Count -eq $expectedTileCount) `
            "Terrain checkpoint '$name' contains duplicate tile coordinates."

        $lod = $checkpoint.lod
        Assert-Condition (
            [int]$lod.sourceTileCount -eq $expectedTileCount -and
            [int]$lod.residentTileCount -eq $expectedTileCount -and
            [int]$lod.selectedPatchCount -gt 0 -and
            [int]$lod.candidatePatchCount -ge [int]$lod.selectedPatchCount -and
            [int]$lod.unavailableTileCount -eq 0 -and
            [int]$lod.overflowPatchCount -eq 0 -and
            $lod.overflowed -eq $false) `
            "Terrain checkpoint '$name' has invalid LOD metrics."
        Assert-Condition ([int]$checkpoint.seamViolationCount -eq 0) `
            "Terrain checkpoint '$name' reported a seam violation."
        $histogramPatchCount = (@($checkpoint.lodHistogram) |
            Measure-Object -Property patchCount -Sum).Sum
        Assert-Condition ([int]$histogramPatchCount -eq [int]$lod.selectedPatchCount) `
            "Terrain checkpoint '$name' has an inconsistent LOD histogram."

        $tileByGuid = @{}
        foreach ($tile in $tiles) {
            $tileGuid = [string]$tile.tileGuid
            $tileByGuid[$tileGuid] = $tile
            Assert-Condition ([long]$tile.generation -gt 0) `
                "Terrain tile '$tileGuid' has no generation at checkpoint '$name'."
            Assert-Condition (
                [int]$tile.patchCount -gt 0 -and
                [int]$tile.minimumLod -ge 0 -and
                [int]$tile.maximumLod -ge [int]$tile.minimumLod -and
                [int]$tile.maximumLod -le 12 -and
                [int]$tile.seamViolationCount -eq 0) `
                "Terrain tile '$tileGuid' has invalid patch/LOD state at checkpoint '$name'."
            Assert-Condition ($tile.worldBounds.isValid -eq $true) `
                "Terrain tile '$tileGuid' has invalid world bounds at checkpoint '$name'."
            Assert-Vector3 $tile.worldBounds.min "Terrain tile '$tileGuid' minimum bound"
            Assert-Vector3 $tile.worldBounds.max "Terrain tile '$tileGuid' maximum bound"
            Assert-Condition (
                [double]$tile.worldBounds.min.x -le [double]$tile.worldBounds.max.x -and
                [double]$tile.worldBounds.min.y -le [double]$tile.worldBounds.max.y -and
                [double]$tile.worldBounds.min.z -le [double]$tile.worldBounds.max.z) `
                "Terrain tile '$tileGuid' has inverted world bounds at checkpoint '$name'."
        }

        foreach ($query in $queries) {
            $tileGuid = [string]$query.tileGuid
            Assert-Condition ($tileByGuid.ContainsKey($tileGuid)) `
                "Terrain query '$tileGuid' has no matching tile at checkpoint '$name'."
            $tile = $tileByGuid[$tileGuid]
            Assert-Condition (
                $query.passed -eq $true -and
                [string]$query.status -ceq "Available" -and
                [long]$query.generation -eq [long]$tile.generation -and
                [int]$query.coordinate.x -eq [int]$tile.coordinate.x -and
                [int]$query.coordinate.z -eq [int]$tile.coordinate.z) `
                "Terrain query '$tileGuid' failed identity/generation parity at checkpoint '$name'."
            Assert-Condition ($query.surfacePosition.isFinite -eq $true) `
                "Terrain query '$tileGuid' returned a non-finite surface position."
            Assert-Vector3 $query.surfacePosition "Terrain query '$tileGuid' surface position"
            Assert-Vector3 $query.normal "Terrain query '$tileGuid' normal"
            $normalLength = [math]::Sqrt(
                ([double]$query.normal.x * [double]$query.normal.x) +
                ([double]$query.normal.y * [double]$query.normal.y) +
                ([double]$query.normal.z * [double]$query.normal.z))
            Assert-Condition ([math]::Abs($normalLength - 1.0) -le 0.001) `
                "Terrain query '$tileGuid' returned a non-unit normal."

            $weightSum = 0.0
            foreach ($component in @("x", "y", "z", "w")) {
                $weight = [double]$query.layerWeights.$component
                Assert-Condition (
                    -not [double]::IsNaN($weight) -and
                    -not [double]::IsInfinity($weight) -and
                    $weight -ge -0.0001 -and
                    $weight -le 1.0001) `
                    "Terrain query '$tileGuid' returned an invalid layer weight."
                $weightSum += $weight
            }
            Assert-Condition ([math]::Abs($weightSum - 1.0) -le 0.001) `
                "Terrain query '$tileGuid' layer weights are not normalized."
        }

        $memory = $checkpoint.memory
        Assert-Condition (
            [int]$memory.allocatedEntitySlots -gt 0 -and
            [int]$memory.loadedCookedHandles -gt 0 -and
            [long]$memory.loadedCookedBytes -gt 0 -and
            [int]$memory.residentAssets -gt 0 -and
            [long]$memory.preparedGpuBytes -gt 0 -and
            [int]$memory.preparedDescriptors -gt 0 -and
            [long]$memory.terrainCpuBytes -gt 0 -and
            [long]$memory.terrainPreparedBytes -gt 0 -and
            [int]$memory.terrainLayerDescriptors -ge 3 -and
            [int]$memory.selectedPatches -eq [int]$lod.selectedPatchCount -and
            [int]$memory.pendingDisposals -eq 0) `
            "Terrain checkpoint '$name' has invalid memory/residency metrics."

        foreach ($field in @(
            "allocatedEntitySlots",
            "loadedCookedHandles",
            "loadedCookedBytes",
            "residentAssets",
            "preparedGpuBytes",
            "preparedDescriptors",
            "terrainCpuBytes",
            "terrainPreparedBytes",
            "terrainLayerDescriptors",
            "selectedPatches",
            "pendingDisposals")) {
            Assert-Condition ([long]$memory.$field -le [long]$peaks.$field) `
                "Terrain checkpoint '$name' exceeds peak field '$field'."
        }
        foreach ($field in @(
            "allocatedEntitySlots",
            "loadedCookedHandles",
            "residentAssets",
            "preparedDescriptors",
            "terrainCpuBytes",
            "terrainPreparedBytes",
            "terrainLayerDescriptors")) {
            Assert-Condition ([long]$memory.$field -le [long]$baselineMemory.$field) `
                "Terrain checkpoint '$name' exceeds its first loaded steady-state '$field' bound."
        }
    }

    $previousReloadGenerations = @{}
    for ($cycle = 1; $cycle -le $requestedCycles; $cycle++) {
        $load = $checkpointByName["soak-load-$cycle"]
        $reload = $checkpointByName["soak-reload-$cycle"]
        $reloadByGuid = @{}
        foreach ($tile in @($reload.tiles)) {
            $reloadByGuid[[string]$tile.tileGuid] = $tile
        }

        foreach ($loadTile in @($load.tiles)) {
            $tileGuid = [string]$loadTile.tileGuid
            Assert-Condition ($reloadByGuid.ContainsKey($tileGuid)) `
                "Soak reload $cycle changed terrain tile identity '$tileGuid'."
            $reloadTile = $reloadByGuid[$tileGuid]
            Assert-Condition ([long]$reloadTile.generation -gt [long]$loadTile.generation) `
                "Soak reload $cycle did not advance generation for '$tileGuid'."
            if ($previousReloadGenerations.ContainsKey($tileGuid)) {
                Assert-Condition ([long]$loadTile.generation -gt [long]$previousReloadGenerations[$tileGuid]) `
                    "Soak load $cycle reused stale generation for '$tileGuid'."
            }
            $previousReloadGenerations[$tileGuid] = [long]$reloadTile.generation
        }
    }

    $boundary = $checkpointByName["boundary-mixed-lod"]
    $postRebase = $checkpointByName["post-rebase"]
    $returned = $checkpointByName["returned-start"]
    Assert-Condition (
        ($boundary.cameraWorldPosition | ConvertTo-Json -Compress) -ceq
        ($postRebase.cameraWorldPosition | ConvertTo-Json -Compress)) `
        "Post-rebase checkpoint did not return to the boundary camera position."
    Assert-Condition (
        ([long]$postRebase.origin.rebaseSequence -eq
         ([long]$boundary.origin.rebaseSequence + 1)) -and
        ([double]$postRebase.origin.origin.x -ne [double]$boundary.origin.origin.x -or
         [double]$postRebase.origin.origin.y -ne [double]$boundary.origin.origin.y -or
         [double]$postRebase.origin.origin.z -ne [double]$boundary.origin.origin.z)) `
        "Post-rebase checkpoint did not retain a distinct rebased origin."
    Assert-Condition (
        ($boundary.tiles | ForEach-Object { $_.worldBounds } | ConvertTo-Json -Compress) -ceq
        ($postRebase.tiles | ForEach-Object { $_.worldBounds } | ConvertTo-Json -Compress)) `
        "Terrain world bounds changed across the origin rebase."
    Assert-Condition (
        ($near.cameraWorldPosition | ConvertTo-Json -Compress) -ceq
        ($returned.cameraWorldPosition | ConvertTo-Json -Compress)) `
        "Returned-start checkpoint did not restore the original camera position."
    Assert-Condition (
        ($near.querySamples | ForEach-Object { $_.surfacePosition } | ConvertTo-Json -Compress) -ceq
        ($returned.querySamples | ForEach-Object { $_.surfacePosition } | ConvertTo-Json -Compress)) `
        "Terrain query surfaces changed after returning from the origin rebase."

    $captures = @($artifact.visualCaptures)
    Assert-Condition ($captures.Count -eq $ExpectedVisualCaptureCount) `
        "Expected $ExpectedVisualCaptureCount terrain visual captures, received $($captures.Count)."
    if ($ExpectedVisualCaptureCount -gt 0) {
        $expectedCaptureNames = @(
            "near",
            "boundary-mixed-lod",
            "far-cascade",
            "post-rebase",
            "returned-start"
        )
        $visuals = @{}
        $previousCaptureFrame = -1L
        foreach ($name in $expectedCaptureNames) {
            $matching = @($captures | Where-Object { [string]$_.capture.name -ceq $name })
            Assert-Condition ($matching.Count -eq 1) `
                "Expected one terrain visual capture named '$name'."
            $capture = $matching[0]
            Assert-Condition ([string]$capture.state -ceq "Succeeded") `
                "Terrain visual capture '$name' did not succeed."
            Assert-Condition ([long]$capture.capture.frameIndex -gt $previousCaptureFrame) `
                "Terrain visual capture frames are not strictly increasing."
            $previousCaptureFrame = [long]$capture.capture.frameIndex

            $visualPath = [System.IO.Path]::GetFullPath([string]$capture.capture.outputPath)
            Assert-Condition (Test-Path -LiteralPath $visualPath -PathType Leaf) `
                "Terrain visual capture '$name' was not produced: $visualPath"
            $visual = Get-Content -LiteralPath $visualPath -Raw | ConvertFrom-Json
            Assert-Condition ([int]$visual.schemaVersion -eq 2) `
                "Terrain visual capture '$name' schema mismatch."
            Assert-Condition ([string]$visual.profile -ceq $ExpectedProfile) `
                "Terrain visual capture '$name' profile mismatch."
            Assert-Condition ($visual.passed -eq $true -and $visual.checks.passed -eq $true) `
                "Terrain visual capture '$name' color checks failed."
            Assert-Condition ($visual.depth.passed -eq $true -and $visual.depth.checks.passed -eq $true) `
                "Terrain visual capture '$name' depth checks failed."
            Assert-Condition (
                [int]$visual.width -gt 0 -and
                [int]$visual.height -gt 0 -and
                [long]$visual.pixelCount -eq ([long]$visual.width * [long]$visual.height) -and
                [long]$visual.finitePixelCount -eq [long]$visual.pixelCount -and
                [long]$visual.nonBlankPixelCount -ge [math]::Ceiling([long]$visual.pixelCount * 0.01)) `
                "Terrain visual capture '$name' has invalid color coverage."
            Assert-Condition (
                [int]$visual.depth.width -eq [int]$visual.width -and
                [int]$visual.depth.height -eq [int]$visual.height -and
                [string]$visual.depth.format -ceq "FORMAT_D32_SFLOAT" -and
                [long]$visual.depth.finiteDepthPixelCount -eq [long]$visual.depth.pixelCount -and
                [long]$visual.depth.normalizedDepthPixelCount -eq [long]$visual.depth.pixelCount -and
                [long]$visual.depth.writtenDepthPixelCount -ge [math]::Ceiling([long]$visual.depth.pixelCount * 0.001)) `
                "Terrain visual capture '$name' has invalid depth coverage."
            Assert-Condition ([string]$visual.pixelSha256 -match '^[0-9A-Fa-f]{64}$') `
                "Terrain visual capture '$name' has no valid color hash."
            Assert-Condition ([string]$visual.depth.pixelSha256 -match '^[0-9A-Fa-f]{64}$') `
                "Terrain visual capture '$name' has no valid depth hash."

            $gridWidth = [int]$visual.spatialGridWidth
            $gridHeight = [int]$visual.spatialGridHeight
            Assert-Condition (
                $gridWidth -ge 2 -and
                $gridHeight -ge 2 -and
                ($gridHeight % 2) -eq 0 -and
                @($visual.spatialLuminanceGrid).Count -eq ($gridWidth * $gridHeight) -and
                [int]$visual.depth.spatialGridWidth -eq $gridWidth -and
                [int]$visual.depth.spatialGridHeight -eq $gridHeight -and
                @($visual.depth.spatialDepthGrid).Count -eq ($gridWidth * $gridHeight)) `
                "Terrain visual capture '$name' has invalid spatial grids."
            $topLuminance = Get-GridHalfAverage `
                @($visual.spatialLuminanceGrid) $gridWidth $gridHeight $true
            $bottomLuminance = Get-GridHalfAverage `
                @($visual.spatialLuminanceGrid) $gridWidth $gridHeight $false
            $topDepth = Get-GridHalfAverage `
                @($visual.depth.spatialDepthGrid) $gridWidth $gridHeight $true
            $bottomDepth = Get-GridHalfAverage `
                @($visual.depth.spatialDepthGrid) $gridWidth $gridHeight $false
            Assert-Condition ($topLuminance -gt ($bottomLuminance + 0.05)) `
                "Terrain visual capture '$name' failed upright color orientation."
            Assert-Condition ($topDepth -gt ($bottomDepth + 0.001)) `
                "Terrain visual capture '$name' failed upright depth orientation."
            $visuals[$name] = $visual
        }

        $pathColorHashes = @(
            [string]$visuals["near"].pixelSha256,
            [string]$visuals["boundary-mixed-lod"].pixelSha256,
            [string]$visuals["far-cascade"].pixelSha256)
        $pathDepthHashes = @(
            [string]$visuals["near"].depth.pixelSha256,
            [string]$visuals["boundary-mixed-lod"].depth.pixelSha256,
            [string]$visuals["far-cascade"].depth.pixelSha256)
        Assert-Condition (@($pathColorHashes | Select-Object -Unique).Count -eq 3) `
            "Near/boundary/far terrain captures did not produce distinct color views."
        Assert-Condition (@($pathDepthHashes | Select-Object -Unique).Count -eq 3) `
            "Near/boundary/far terrain captures did not produce distinct depth views."

        Assert-VisualSimilarity `
            $visuals["boundary-mixed-lod"] `
            $visuals["post-rebase"] `
            "Origin rebase at the boundary camera"
        Assert-VisualSimilarity `
            $visuals["near"] `
            $visuals["returned-start"] `
            "Returning to the start camera"
    }

    $maximumFrame = (@($checkpoints | Measure-Object frameIndex -Maximum).Maximum + 1)
    $successMessage = (
        "[Arisen] Terrain-streaming summary passed: profile={0}, frames={1}, " +
        "tiles={2}, soak={3}, rebases={4}, visuals={5}, output={6}") -f
        $ExpectedProfile,
        $maximumFrame,
        $expectedTileCount,
        $completedCycles,
        @($artifact.rebaseSequences).Count,
        $captures.Count,
        $path
    Write-Host $successMessage
    exit 0
}
catch {
    Write-Host "[ERROR] Terrain-streaming summary validation failed: $($_.Exception.Message)"
    exit 1
}

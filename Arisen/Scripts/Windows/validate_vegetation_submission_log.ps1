param(
    [Parameter(Mandatory = $true)]
    [string]$LogPath,

    # Two viewports in the editor host, one surface in a standalone runtime launch.
    [ValidateRange(1, 8)]
    [int]$RequiredDistinctSurfaceCount = 1,

    [string]$CaseName = "runtime",

    # The directional-shadow pass records one batch/instance pair per cascade, so its recorded work
    # is the opaque work repeated for every cascade and every per-cascade count equals the opaque
    # count. Four cascades is the shipped directional-shadow setting.
    [ValidateRange(1, 8)]
    [int]$ExpectedCascadeCount = 4,

    # Optional deployment pin. A caller that holds the cooked catalog passes every cooked
    # vegetation-cluster identity, so a drawn cluster outside the deployed startup world fails
    # instead of only being counted. Without it the check is limited to distinct, well-formed
    # identities of shipped species.
    [string[]]$AllowedClusterGuids = @(),

    # Optional selection gate. A profile that does not select the vegetation feature produces no
    # validation record at all, so the caller passes the resolved manifest and the package it
    # requires rather than duplicating the selection check.
    [string]$ResolvedManifestPath = "",
    [string]$RequiredPackageId = ""
)

$ErrorActionPreference = "Stop"

# A cooked cluster is scoped to one species, so every drawn cluster reports one of the four shipped
# species identities. An unknown species means the drawn set did not come from the shipped content.
$canonicalSpeciesGuids = @(
    "3eb515ce3dea499481a1a14fc2fe0eb5",
    "7b0f2e528b674e3dbf0acbc42f622001",
    "83212e385d3e44948929fe27639b5a29",
    "e560581300b6412985b205b754ccde74"
)

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Test-VegetationPackageSelected {
    param(
        [string]$ManifestPath,
        [string]$PackageId
    )

    if ([string]::IsNullOrWhiteSpace($ManifestPath) -or
        [string]::IsNullOrWhiteSpace($PackageId)) {
        return $true
    }

    $manifestFullPath = [System.IO.Path]::GetFullPath($ManifestPath)
    Assert-Condition (Test-Path -LiteralPath $manifestFullPath -PathType Leaf) `
        "Resolved manifest is missing for vegetation submission validation: $manifestFullPath"
    $resolved = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
    $packageIds = @($resolved.ResolvedPackages | ForEach-Object { [string]$_.Id })
    return $packageIds -ccontains $PackageId
}

try {
    if (-not (Test-VegetationPackageSelected $ResolvedManifestPath $RequiredPackageId)) {
        Write-Host (
            "[Arisen] Vegetation submission validation skipped: '{0}' is not selected." -f
            $RequiredPackageId)
        exit 0
    }

    $logFullPath = [System.IO.Path]::GetFullPath($LogPath)
    Assert-Condition (Test-Path -LiteralPath $logFullPath -PathType Leaf) `
        "Launch-owned player log is missing for vegetation submission validation: $logFullPath"

    $text = Get-Content -LiteralPath $logFullPath -Raw
    $marker = "[Vegetation.GenericRP.Validation]"
    $pattern = [Regex]::Escape($marker) +
        ' Surface=0x(?<surface>[0-9A-F]+) Frame=[0-9]+ DeviceGeneration=[0-9]+' +
        ' Revision=[0-9]+ Extracted=(?<extracted>[0-9]+)' +
        ' CullingInputs=(?<cullingInputs>[0-9]+)' +
        ' PreparedClusters=(?<preparedClusters>[0-9]+)' +
        ' Cluster=(?<firstCluster>[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-' +
        '[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})' +
        ' Species=(?<firstSpecies>[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-' +
        '[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})' +
        ' Clusters=(?<clusters>[0-9A-Fa-f]{32}:[0-9A-Fa-f]{32}:[0-9]+' +
        '(?:,[0-9A-Fa-f]{32}:[0-9A-Fa-f]{32}:[0-9]+)*)' +
        ' ClustersOverflow=(?<clustersOverflow>[0-9]+)' +
        ' OpaqueBatches=(?<opaqueBatches>[0-9]+)' +
        ' OpaqueInstances=(?<opaqueInstances>[0-9]+)' +
        ' RecordedShadowBatches=(?<recordedShadowBatches>[0-9]+)' +
        ' RecordedShadowInstances=(?<recordedShadowInstances>[0-9]+)' +
        ' Cascades=(?<cascades>[0-9]+)' +
        ' ShadowBatches=(?<shadowBatches>[0-9]+(?:,[0-9]+)*)' +
        ' ShadowInstances=(?<shadowInstances>[0-9]+(?:,[0-9]+)*)' +
        ' Dropped=(?<dropped>[0-9]+) Ticket=(?<ticket>[1-9][0-9]*)'

    $markerCount = [Regex]::Matches($text, [Regex]::Escape($marker)).Count
    Assert-Condition ($markerCount -gt 0) `
        "Runtime run produced no vegetation validation record: $logFullPath"

    $records = [Regex]::Matches(
        $text,
        $pattern,
        [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    Assert-Condition ($records.Count -eq $markerCount) `
        ("Vegetation validation records do not all match the submission contract: extracted," +
        " verified, and prepared cluster accounting, distinct cluster identities of shipped species," +
        " exact opaque and per-cascade shadow work, and no dropped draw: $logFullPath")

    # PowerShell variable names are case-insensitive, so the set must not reuse the parameter name.
    $allowedClusterGuidSet = [System.Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($allowed in @($AllowedClusterGuids)) {
        if ($null -eq $allowed) {
            continue
        }

        foreach ($entry in $allowed.Split(
                [char[]]',',
                [System.StringSplitOptions]::RemoveEmptyEntries)) {
            $identity = $entry.Trim().Replace("-", "")
            Assert-Condition ($identity -match '^[0-9A-Fa-f]{32}$') `
                "The deployed cluster identity pin is not a Guid: '$entry'."
            [void]$allowedClusterGuidSet.Add($identity)
        }
    }

    $surfaces = [System.Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($record in $records) {
        [void]$surfaces.Add($record.Groups["surface"].Value)

        $extracted = [long]$record.Groups["extracted"].Value
        $cullingInputs = [long]$record.Groups["cullingInputs"].Value
        $preparedClusters = [long]$record.Groups["preparedClusters"].Value
        $dropped = [long]$record.Groups["dropped"].Value
        $opaqueBatches = [long]$record.Groups["opaqueBatches"].Value
        $opaqueInstances = [long]$record.Groups["opaqueInstances"].Value
        $cascades = [long]$record.Groups["cascades"].Value

        # Extraction sees the clusters of every active scene; verification narrows that set to the
        # clusters whose resident and prepared data exists. Nothing may be lost between the two, so
        # a verified set smaller than the extracted set is a residency/activation ordering defect.
        Assert-Condition ($cullingInputs -le $extracted) `
            "Vegetation record verified more clusters than it extracted."
        Assert-Condition ($cullingInputs -gt 0) `
            "Vegetation record verified no cluster at all."
        # A cluster the plan evaluated and did not accept is culled work - invisible clusters, a
        # cluster beyond its LOD distance, or work outside the batch/instance budget - so the
        # prepared set is the accepted subset of the verified set and the dropped count stays zero.
        Assert-Condition ($preparedClusters -ge 1 -and $preparedClusters -le $cullingInputs) `
            "Vegetation record prepared an impossible cluster count."
        Assert-Condition ($dropped -eq 0) `
            ("Vegetation record dropped $dropped verified draw(s); every accepted cluster must be" +
            " prepared for submission.")
        Assert-Condition ([long]$record.Groups["clustersOverflow"].Value -eq 0) `
            "Vegetation record overflowed its bounded cluster log."

        Assert-Condition ($cascades -eq $ExpectedCascadeCount) `
            "Vegetation record did not report $ExpectedCascadeCount shadow cascades."
        Assert-Condition ($opaqueBatches -ge 1 -and $opaqueInstances -ge 1) `
            "Vegetation record submitted no opaque work."
        Assert-Condition (
            [long]$record.Groups["recordedShadowBatches"].Value -eq ($opaqueBatches * $cascades)) `
            "Vegetation recorded shadow batches are not the opaque batches repeated per cascade."
        Assert-Condition (
            [long]$record.Groups["recordedShadowInstances"].Value -eq
            ($opaqueInstances * $cascades)) `
            "Vegetation recorded shadow instances are not the opaque instances repeated per cascade."

        $perCascadeBatches = @($record.Groups["shadowBatches"].Value.Split(","))
        $perCascadeInstances = @($record.Groups["shadowInstances"].Value.Split(","))
        Assert-Condition ($perCascadeBatches.Count -eq $cascades) `
            "Vegetation record did not report one shadow batch count per cascade."
        Assert-Condition ($perCascadeInstances.Count -eq $cascades) `
            "Vegetation record did not report one shadow instance count per cascade."
        foreach ($batchCount in $perCascadeBatches) {
            Assert-Condition ([long]$batchCount -eq $opaqueBatches) `
                "Vegetation record drew a different batch count in one cascade than in the opaque pass."
        }
        foreach ($instanceCount in $perCascadeInstances) {
            Assert-Condition ([long]$instanceCount -eq $opaqueInstances) `
                "Vegetation record drew a different instance count in one cascade than in the opaque pass."
        }

        $clusterGuids = [System.Collections.Generic.HashSet[string]]::new(
            [StringComparer]::OrdinalIgnoreCase)
        $reportedInstances = 0L
        $entries = @($record.Groups["clusters"].Value.Split(","))
        Assert-Condition ($entries.Count -eq $preparedClusters) `
            ("Vegetation record logged $($entries.Count) cluster entr(ies) for a prepared count of" +
            " $preparedClusters.")
        foreach ($entry in $entries) {
            $parts = $entry.Split(":")
            $entryClusterGuid = $parts[0]
            $entrySpeciesGuid = $parts[1]
            $entryInstances = [long]$parts[2]
            Assert-Condition ($clusterGuids.Add($entryClusterGuid)) `
                "Vegetation record logged the same cluster identity twice."
            Assert-Condition ($canonicalSpeciesGuids -ccontains $entrySpeciesGuid.ToLowerInvariant()) `
                "Vegetation record logged a cluster of an unknown species."
            Assert-Condition ($entryInstances -ge 1) `
                "Vegetation record logged a cluster with no instance."
            if ($allowedClusterGuidSet.Count -gt 0) {
                Assert-Condition ($allowedClusterGuidSet.Contains($entryClusterGuid)) `
                    "Vegetation record logged a cluster that is not part of the deployed world."
            }

            $reportedInstances += $entryInstances
        }

        Assert-Condition ($reportedInstances -eq $opaqueInstances) `
            ("Vegetation record logged $reportedInstances cluster instances for an opaque instance" +
            " count of $opaqueInstances.")
        Assert-Condition (
            $record.Groups["firstCluster"].Value.Replace("-", "").ToLowerInvariant() -ceq
            $entries[0].Split(":")[0] -and
            $record.Groups["firstSpecies"].Value.Replace("-", "").ToLowerInvariant() -ceq
            $entries[0].Split(":")[1]) `
            "Vegetation record lead cluster does not match the first logged cluster entry."
    }

    Assert-Condition ($surfaces.Count -ge $RequiredDistinctSurfaceCount) `
        ("Vegetation submission validation requires $RequiredDistinctSurfaceCount distinct surface" +
        " record(s); found $($surfaces.Count).")

    $successMessage =
        "[Arisen] Vegetation submission log passed: case={0}, records={1}, distinctSurfaces={2}, " +
        "log={3}"
    Write-Host ($successMessage -f $CaseName,$records.Count,$surfaces.Count,$logFullPath)
    exit 0
}
catch {
    Write-Host "[ERROR] Vegetation submission validation failed for ${CaseName}: $($_.Exception.Message)"
    exit 1
}

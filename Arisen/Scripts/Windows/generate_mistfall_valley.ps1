<#
.SYNOPSIS
    Regenerates the MistfallValley streaming-world terrain sources, root asset, tile stubs, cell
    scenes, world descriptor, persistent scene, and per-cell scatter recipes.

.DESCRIPTION
    MistfallValley is the streaming-world fixture: a 4x4 grid of 256 m world cells over one 1 km
    raster, one terrain root that binds the raster, 8x8 generated tile identities, and one cell
    scene per world cell that owns exactly the tiles inside its own bounds. Every generated
    identity is derived with the same child-GUID rule the engine loaders use, so rerunning the
    script reproduces identical bytes and any drift fails the loaders instead of leaving stale
    identities behind.

.PARAMETER CellsPerAxis
    World cells per axis. Four produces the canonical 1 km streaming world.

.PARAMETER TilesPerCell
    Terrain tiles per axis inside each cell. Two produces 128 m tiles.

.PARAMETER DryRun
    Compute and report the plan without writing any file.
#>
[CmdletBinding()]
param(
    [ValidateRange(2, 8)]
    [int]$CellsPerAxis = 4,
    [ValidateRange(1, 4)]
    [int]$TilesPerCell = 2,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# --- Fixture identity -------------------------------------------------------------------------

$WorldSeed = [Guid]'7f2e5c31-9a4b-4d18-8f0a-2b6d3e5c7a91'
$PackageId = 'com.arisen.packagegame'
$WorldAssetName = 'MistfallValley'
$WorldDisplayName = 'Mistfall Valley World'
$LayerSetGuid = [Guid]'5dcaa6bd-2b51-498d-9fa9-bd68f4642761'
$BiomeGuid = [Guid]'c0a92f10-0eb9-4d24-b729-7d0f38313001'
$TileEntityNameFormat = 'Mistfall Valley Terrain Tile {0},{1}'

# --- Raster layout ----------------------------------------------------------------------------

$SampleSpacing = 0.5
$TileResolution = 257
$TileIntervals = $TileResolution - 1
$TileMetres = $TileIntervals * $SampleSpacing
$CellMetres = $TilesPerCell * $TileMetres
$WorldMetres = $CellsPerAxis * $CellMetres
$WorldOrigin = -0.5 * $WorldMetres
$WorldSamples = ($CellsPerAxis * $TilesPerCell * $TileIntervals) + 1
$HeightMaximum = 56.0
$CellSampleCount = ($TilesPerCell * $TileIntervals) + 1
$TileEntityOffsetY = 64.0

# Scatter entries: radius mirrors each species' unscaled conservative radius.
$ScatterEntries = @(
    [pscustomobject]@{ EntryId = 'valley-rock'; Radius = 2.0 }
    [pscustomobject]@{ EntryId = 'valley-grass'; Radius = 0.3 }
    [pscustomobject]@{ EntryId = 'valley-shrub'; Radius = 1.2 }
    [pscustomobject]@{ EntryId = 'valley-tree'; Radius = 3.2 }
)

$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$packageRoot = Join-Path $repoRoot 'Arisen\Development\PackageGame\Local\com.arisen.packagegame'
$assetRoot = Join-Path $packageRoot "Assets\Terrain\$WorldAssetName"
$sceneRoot = Join-Path $packageRoot 'Assets\Scenes'
$cellSceneRoot = Join-Path $sceneRoot "$WorldAssetName Cells"
$worldRoot = Join-Path $packageRoot 'Assets\Worlds'
$vegetationRoot = Join-Path $packageRoot "Assets\Vegetation\$WorldAssetName"
$heightPath = Join-Path $assetRoot "Height\$WorldAssetName.pgm"
$weightPath = Join-Path $assetRoot "$WorldAssetName.ariweights"
$rootPath = Join-Path $assetRoot "$WorldAssetName.aristerrain"
$generatedPath = Join-Path $assetRoot "Generated\$WorldAssetName"
$worldPath = Join-Path $worldRoot "$WorldAssetName.arisenworld"
$persistentScenePath = Join-Path $sceneRoot "$WorldAssetName-Persistent.arisenscene"
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $packageRoot)
$clusterReportPath = Join-Path $workspaceRoot '.arisen\Cache\vegetation-clusters.json'

# --- Identity derivation ----------------------------------------------------------------------

function Get-Sha256Digest([byte[]]$data)
{
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try
    {
        return ,$sha256.ComputeHash($data)
    }
    finally
    {
        $sha256.Dispose()
    }
}

function Get-ChildGuid([Guid]$sourceGuid, [string]$childKind, [string]$childKey)
{
    $identity = @(
        'arisen.generated-asset-child.v1'
        $sourceGuid.ToString('N')
        $PackageId.ToLowerInvariant()
        $childKind.ToLowerInvariant()
        $childKey
    ) -join "`n"
    $digest = Get-Sha256Digest ([System.Text.Encoding]::UTF8.GetBytes($identity))
    $bytes = [byte[]]::new(16)
    [Array]::Copy($digest, $bytes, 16)
    return [Guid]::new($bytes)
}

function Get-BigEndianBytes([Guid]$value)
{
    $text = $value.ToString('N')
    $bytes = [byte[]]::new(16)
    for ($index = 0; $index -lt 16; $index++)
    {
        $bytes[$index] = [Convert]::ToByte($text.Substring($index * 2, 2), 16)
    }

    return ,$bytes
}

function Get-GuidFromBigEndianBytes([byte[]]$bytes)
{
    $text = -join ($bytes[0..15] | ForEach-Object { $_.ToString('x2') })
    return [Guid]::ParseExact($text, 'N')
}

$WorldGuid = Get-ChildGuid $WorldSeed 'world' 'mistfall-valley'
$PersistentSceneGuid = Get-ChildGuid $WorldGuid 'scene' 'persistent'
$TerrainRootGuid = Get-ChildGuid $WorldGuid 'terrain-root' 'valley'

function Get-CellSceneGuid([int]$cellX, [int]$cellZ)
{
    return Get-ChildGuid $WorldGuid 'scene' "cell=x=$cellX;z=$cellZ"
}

function Get-TileGuid([int]$tileX, [int]$tileZ)
{
    return Get-ChildGuid $TerrainRootGuid 'terrain-tile' "x=$tileX;z=$tileZ"
}

function Get-TileEntityGuid([Guid]$cellSceneGuid, [Guid]$tileGuid)
{
    $domain = [System.Text.Encoding]::ASCII.GetBytes('Arisen.TerrainTileEntity.v1')
    $buffer = [byte[]]::new($domain.Length + 32)
    [Array]::Copy($domain, $buffer, $domain.Length)
    [Array]::Copy((Get-BigEndianBytes $cellSceneGuid), 0, $buffer, $domain.Length, 16)
    [Array]::Copy((Get-BigEndianBytes $tileGuid), 0, $buffer, $domain.Length + 16, 16)
    $digest = Get-Sha256Digest $buffer
    $digest[6] = [byte](($digest[6] -band 0x0F) -bor 0x50)
    $digest[8] = [byte](($digest[8] -band 0x3F) -bor 0x80)
    return Get-GuidFromBigEndianBytes $digest
}

function Get-RecipeGuid([int]$cellX, [int]$cellZ, [string]$entryId)
{
    return Get-ChildGuid $WorldGuid 'vegetation-recipe' "entry=$entryId;cell=$cellX,$cellZ"
}

# --- World-cell identity ----------------------------------------------------------------------

function Get-WorldCellGuid([Guid]$worldGuid, [int]$cellX, [int]$cellY, [int]$cellZ, [string]$layer)
{
    # Mirrors WorldCellIdentity.Create: the runtime compares the authored owning-cell GUID with the
    # activation cell identity, so both sides have to derive it from the same canonical text.
    $identity = @(
        'arisen.world-cell.v1'
        $worldGuid.ToString('N')
        $cellX
        $cellY
        $cellZ
        $layer.Trim().ToLowerInvariant()
    ) -join '|'
    $digest = Get-Sha256Digest ([System.Text.Encoding]::UTF8.GetBytes($identity))
    $digest[6] = [byte](($digest[6] -band 0x0F) -bor 0x50)
    $digest[8] = [byte](($digest[8] -band 0x3F) -bor 0x80)
    return Get-GuidFromBigEndianBytes $digest
}

function Format-RoundTrip([double]$value)
{
    # Cooked cluster bounds are compared bit for bit against the authored component, so the scene
    # has to carry a round-trippable scalar rather than a rounded display value.
    return $value.ToString('R', [System.Globalization.CultureInfo]::InvariantCulture)
}

function Get-ClusterKey([int]$cellX, [int]$cellY, [int]$cellZ, [string]$layer, [string]$entryId)
{
    return "$cellX|$cellY|$cellZ|$layer|$entryId"
}

function Read-VegetationClusterReport([string]$path)
{
    # Cook output written by com.arisen.vegetation. Cell scenes bind vegetation clusters, and the
    # baked page count, instance count, and bounds only exist after the scatter bake, so the recipes
    # are authored first and this report is read back on the following run.
    $clusters = @{}
    if (-not (Test-Path -LiteralPath $path))
    {
        return $clusters
    }

    $document = [System.Text.Json.JsonDocument]::Parse(
        [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8))
    try
    {
        $root = $document.RootElement
        if ($root.GetProperty('schemaVersion').GetInt32() -ne 1)
        {
            throw "Vegetation cluster report '$path' declares an unsupported schema version."
        }

        $worldText = $WorldGuid.ToString('D')
        $terrainText = $TerrainRootGuid.ToString('D')
        $biomeText = $BiomeGuid.ToString('D')
        foreach ($element in $root.GetProperty('clusters').EnumerateArray())
        {
            if ($element.GetProperty('worldGuid').GetString() -ne $worldText)
            {
                continue
            }

            if ($element.GetProperty('terrainRootGuid').GetString() -ne $terrainText -or
                $element.GetProperty('biomeGuid').GetString() -ne $biomeText)
            {
                throw "Vegetation cluster report '$path' is stale: it binds a different terrain root or biome."
            }

            $cell = $element.GetProperty('cell')
            $origin = $element.GetProperty('origin')
            $min = $element.GetProperty('bounds').GetProperty('min')
            $max = $element.GetProperty('bounds').GetProperty('max')
            $record = [pscustomobject]@{
                EntryId = $element.GetProperty('entryId').GetString()
                RecipeGuid = [Guid]$element.GetProperty('recipeGuid').GetString()
                ClusterGuid = [Guid]$element.GetProperty('clusterGuid').GetString()
                ClusterPackageId = $element.GetProperty('clusterPackageId').GetString()
                BiomeGuid = [Guid]$element.GetProperty('biomeGuid').GetString()
                BiomePackageId = $element.GetProperty('biomePackageId').GetString()
                SpeciesGuid = [Guid]$element.GetProperty('speciesGuid').GetString()
                SpeciesPackageId = $element.GetProperty('speciesPackageId').GetString()
                CellX = $cell.GetProperty('x').GetInt32()
                CellY = $cell.GetProperty('y').GetInt32()
                CellZ = $cell.GetProperty('z').GetInt32()
                Layer = $cell.GetProperty('layer').GetString()
                OriginX = $origin.GetProperty('x').GetDouble()
                OriginY = $origin.GetProperty('y').GetDouble()
                OriginZ = $origin.GetProperty('z').GetDouble()
                MinX = $min.GetProperty('x').GetDouble()
                MinY = $min.GetProperty('y').GetDouble()
                MinZ = $min.GetProperty('z').GetDouble()
                MaxX = $max.GetProperty('x').GetDouble()
                MaxY = $max.GetProperty('y').GetDouble()
                MaxZ = $max.GetProperty('z').GetDouble()
                PageCount = $element.GetProperty('pageCount').GetInt32()
                InstanceCount = $element.GetProperty('instanceCount').GetInt32()
            }
            if ($ScatterEntries.EntryId -notcontains $record.EntryId)
            {
                throw "Vegetation cluster report '$path' is stale: it carries unconfigured entry '$($record.EntryId)'."
            }

            $key = Get-ClusterKey $record.CellX $record.CellY $record.CellZ $record.Layer $record.EntryId
            if ($clusters.ContainsKey($key))
            {
                throw "Vegetation cluster report '$path' declares cell entry '$key' more than once."
            }

            $clusters[$key] = $record
        }
    }
    finally
    {
        $document.Dispose()
    }

    return $clusters
}

# --- Raster synthesis kernel ------------------------------------------------------------------

$rasterSource = @"
using System;
using System.Globalization;
using System.IO;
using System.Text;

public static class MistfallValleyRaster
{
    private const int WeightChannels = 4;
    private const double SampleSpacing = $($SampleSpacing.ToString([System.Globalization.CultureInfo]::InvariantCulture));
    private const double HeightMaximum = $($HeightMaximum.ToString([System.Globalization.CultureInfo]::InvariantCulture));
    private const double WorldOrigin = $($WorldOrigin.ToString([System.Globalization.CultureInfo]::InvariantCulture));
    private const double CellMetres = $($CellMetres.ToString([System.Globalization.CultureInfo]::InvariantCulture));
    private const double RockSlopeDegrees = 24.0;
    private const double RockSlopeSpanDegrees = 16.0;

    public sealed class RasterResult
    {
        internal RasterResult(int width, byte[] heightBytes, byte[] weightBytes)
        {
            Width = width;
            HeightBytes = heightBytes;
            WeightBytes = weightBytes;
        }

        public int Width { get; }
        public byte[] HeightBytes { get; }
        public byte[] WeightBytes { get; }
    }

    public static RasterResult Build(int width)
    {
        int sampleCount = checked(width * width);
        ushort[] codes = new ushort[sampleCount];
        double[] metres = new double[sampleCount];
        byte[] weights = new byte[checked(sampleCount * WeightChannels)];
        for (int row = 0; row < width; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int sample = (row * width) + column;
                double x = WorldOrigin + (column * SampleSpacing);
                double z = WorldOrigin + (row * SampleSpacing);
                double height = HeightAt(x, z);
                metres[sample] = height;
                codes[sample] = (ushort)Math.Round((height / HeightMaximum) * 65535.0);
            }
        }

        for (int row = 0; row < width; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int sample = (row * width) + column;
                double x = WorldOrigin + (column * SampleSpacing);
                double z = WorldOrigin + (row * SampleSpacing);
                double west = metres[(row * width) + Math.Max(0, column - 1)];
                double east = metres[(row * width) + Math.Min(width - 1, column + 1)];
                double north = metres[(Math.Max(0, row - 1) * width) + column];
                double south = metres[(Math.Min(width - 1, row + 1) * width) + column];
                double gradientX = (east - west) / (2.0 * SampleSpacing);
                double gradientZ = (south - north) / (2.0 * SampleSpacing);
                double slope = Math.Atan(
                    Math.Sqrt((gradientX * gradientX) + (gradientZ * gradientZ))) *
                    180.0 / Math.PI;
                double rock = SmoothStep((slope - RockSlopeDegrees) / RockSlopeSpanDegrees);
                double trail = TrailWeight(x, z);
                double grass = Math.Max(0.0, 1.0 - rock - trail);
                int destination = sample * WeightChannels;
                weights[destination] = ToByte(grass);
                weights[destination + 1] = ToByte(rock);
                weights[destination + 2] = ToByte(trail);
                weights[destination + 3] = 0;
            }
        }

        return new RasterResult(width, EncodeHeight(width, codes), EncodeWeights(width, weights));
    }

    public static double HeightAt(double x, double z)
    {
        double ridgeX = Math.Max(0.0, x + 60.0) / 420.0;
        double ridgeZ = Math.Max(0.0, z + 120.0) / 420.0;
        double ridge = Math.Min(1.0, ridgeX + ridgeZ);
        double metres = 7.0 + (24.0 * ridge * ridge) +
            (9.5 * FractalNoise(x / 210.0, z / 210.0)) +
            (3.5 * FractalNoise(x / 46.0, z / 46.0));
        double basin = ValleyBasin(x, z);
        metres -= 6.0 * basin;
        return Math.Clamp(metres, 0.0, HeightMaximum);
    }

    private static double ValleyBasin(double x, double z)
    {
        // One soft basin under the authored startup camera keeps the opening view a valley floor
        // instead of a hillside.
        double dx = (x + 102.0) / 260.0;
        double dz = (z + 128.0) / 260.0;
        double distance = Math.Sqrt((dx * dx) + (dz * dz));
        return SmoothStep(1.0 - distance);
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value * 255.0), 0.0, 255.0);
    }

    private static double SmoothStep(double value)
    {
        double clamped = Math.Clamp(value, 0.0, 1.0);
        return clamped * clamped * (3.0 - (2.0 * clamped));
    }

    private static double TrailWeight(double x, double z)
    {
        double centre = (z * 0.34) + 96.0 + (34.0 * FractalNoise(z / 260.0, 4.5));
        double distance = Math.Abs(x - centre);
        return distance > 6.0 ? 0.0 : SmoothStep(1.0 - (distance / 6.0));
    }

    private static double FractalNoise(double x, double z)
    {
        double sum = 0.0;
        double total = 0.0;
        double amplitude = 1.0;
        double frequency = 1.0;
        for (int octave = 0; octave < 4; octave++)
        {
            sum += ValueNoise(x * frequency, z * frequency, 17 + octave) * amplitude;
            total += amplitude;
            amplitude *= 0.5;
            frequency *= 2.0;
        }

        return sum / total;
    }

    private static double ValueNoise(double x, double z, int salt)
    {
        int x0 = (int)Math.Floor(x);
        int z0 = (int)Math.Floor(z);
        double fx = SmoothStep(x - x0);
        double fz = SmoothStep(z - z0);
        double n00 = HashUnit(x0, z0, salt);
        double n10 = HashUnit(x0 + 1, z0, salt);
        double n01 = HashUnit(x0, z0 + 1, salt);
        double n11 = HashUnit(x0 + 1, z0 + 1, salt);
        double top = (n00 * (1.0 - fx)) + (n10 * fx);
        double bottom = (n01 * (1.0 - fx)) + (n11 * fx);
        return (top * (1.0 - fz)) + (bottom * fz);
    }

    private static double HashUnit(int x, int z, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)x) * 16777619u;
            hash = (hash ^ (uint)z) * 16777619u;
            hash = (hash ^ (uint)salt) * 16777619u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            hash ^= hash >> 16;
            return (hash & 0xFFFFFF) / (double)0xFFFFFF;
        }
    }

    private static byte[] EncodeHeight(int width, ushort[] codes)
    {
        string size = width.ToString(CultureInfo.InvariantCulture);
        byte[] header = Encoding.ASCII.GetBytes("P5\n" + size + " " + size + "\n65535\n");
        byte[] bytes = new byte[header.Length + (codes.Length * 2)];
        Array.Copy(header, bytes, header.Length);
        int cursor = header.Length;
        for (int index = 0; index < codes.Length; index++)
        {
            ushort value = codes[index];
            bytes[cursor++] = (byte)(value >> 8);
            bytes[cursor++] = (byte)(value & 0xFF);
        }

        return bytes;
    }

    private static byte[] EncodeWeights(int width, byte[] weights)
    {
        const string Hex = "0123456789abcdef";
        string size = width.ToString(CultureInfo.InvariantCulture);
        byte[] header = Encoding.ASCII.GetBytes("ARIWEIGHTS\n1\n" + size + " " + size + "\n");
        int sampleCount = weights.Length / WeightChannels;
        byte[] bytes = new byte[header.Length + (sampleCount * ((WeightChannels * 2) + 1))];
        Array.Copy(header, bytes, header.Length);
        int cursor = header.Length;
        for (int sample = 0; sample < sampleCount; sample++)
        {
            int source = sample * WeightChannels;
            for (int channel = 0; channel < WeightChannels; channel++)
            {
                byte value = weights[source + channel];
                bytes[cursor++] = (byte)Hex[value >> 4];
                bytes[cursor++] = (byte)Hex[value & 0x0F];
            }

            bytes[cursor++] = (byte)((sample % width) == width - 1 ? '\n' : ' ');
        }

        return bytes;
    }
}
"@
Add-Type -TypeDefinition $rasterSource -Language CSharp

function Add-Line([System.Text.StringBuilder]$builder, [string]$value)
{
    [void]$builder.Append($value).Append("`n")
}

function Format-Scalar([double]$value)
{
    return $value.ToString('0.0#########', [System.Globalization.CultureInfo]::InvariantCulture)
}

function Write-Text([string]$path, [string]$content)
{
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrEmpty($directory) -and -not (Test-Path -LiteralPath $directory))
    {
        [void][System.IO.Directory]::CreateDirectory($directory)
    }

    [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))
}

function Write-Bytes([string]$path, [byte[]]$bytes)
{
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrEmpty($directory) -and -not (Test-Path -LiteralPath $directory))
    {
        [void][System.IO.Directory]::CreateDirectory($directory)
    }

    [System.IO.File]::WriteAllBytes($path, $bytes)
}

function New-SourceMeta([Guid]$guid, [string]$assetType, [string]$importer)
{
    $text = [System.Text.StringBuilder]::new(256)
    Add-Line $text ("Guid: $($guid.ToString('D'))")
    Add-Line $text ("AssetType: `"$assetType`"")
    Add-Line $text ("Importer: `"$importer`"")
    return $text.ToString()
}

function New-GeneratedMeta(
    [Guid]$guid,
    [string]$assetType,
    [string]$importer,
    [string]$childKind,
    [string]$childKey)
{
    $text = [System.Text.StringBuilder]::new(512)
    Add-Line $text ("Guid: $($guid.ToString('D'))")
    Add-Line $text ("AssetType: `"$assetType`"")
    Add-Line $text ("Importer: `"$importer`"")
    Add-Line $text ('Generated:')
    Add-Line $text ("  SourceGuid: $($TerrainRootGuid.ToString('D'))")
    Add-Line $text ("  SourcePackageId: `"$PackageId`"")
    Add-Line $text ("  ChildKind: `"$childKind`"")
    Add-Line $text ("  ChildKey: `"$childKey`"")
    Add-Line $text ("  GeneratedByImporter: `"$importer`"")
    return $text.ToString()
}

function New-RootText($tileRecords)
{
    $text = [System.Text.StringBuilder]::new(2048)
    Add-Line $text ('Version: 2')
    Add-Line $text ("TerrainGuid: $($TerrainRootGuid.ToString('D'))")
    Add-Line $text ("Name: `"$WorldDisplayName Terrain`"")
    Add-Line $text ("WorldPlacement: { X: $(Format-Scalar $WorldOrigin), Y: 0, Z: $(Format-Scalar $WorldOrigin) }")
    Add-Line $text ("SampleSpacing: { X: $(Format-Scalar $SampleSpacing), Z: $(Format-Scalar $SampleSpacing) }")
    Add-Line $text ("HeightRange: { Min: 0, Max: $(Format-Scalar $HeightMaximum) }")
    Add-Line $text ('HeightSource:')
    Add-Line $text ("  Path: `"Height/$WorldAssetName.pgm`"")
    Add-Line $text ('  Format: Pgm16BigEndianScalar')
    Add-Line $text ('WeightSource:')
    Add-Line $text ("  Path: `"$WorldAssetName.ariweights`"")
    Add-Line $text ('  Format: Rgba8Hex')
    Add-Line $text ("TileResolution: $TileResolution")
    Add-Line $text ('BorderPolicy: SharedEdgeSamples')
    Add-Line $text ('TileOrigin: { X: 0, Z: 0 }')
    Add-Line $text ('LayerSet:')
    Add-Line $text ("  Guid: $($LayerSetGuid.ToString('D'))")
    Add-Line $text ("  PackageId: `"$PackageId`"")
    Add-Line $text ('GeneratedTiles:')
    foreach ($tile in $tileRecords)
    {
        Add-Line $text ("- Coordinate: { X: $($tile.X), Z: $($tile.Z) }")
        Add-Line $text ("  Guid: $($tile.Guid.ToString('D'))")
    }

    return $text.ToString()
}

function New-TileEntityBlock([int]$tileX, [int]$tileZ, [Guid]$tileGuid, [double]$cellOriginX, [double]$cellOriginZ)
{
    $worldX = $WorldOrigin + ($tileX * $TileMetres)
    $worldZ = $WorldOrigin + ($tileZ * $TileMetres)
    $text = [System.Text.StringBuilder]::new(1024)
    Add-Line $text ("- Guid: $((Get-TileEntityGuid (Get-CellSceneGuid $script:cellX $script:cellZ) $tileGuid).ToString('D'))")
    Add-Line $text ("  Name: $($TileEntityNameFormat -f $tileX, $tileZ)")
    Add-Line $text ('  Transform:')
    Add-Line $text ("    Position: { X: $(Format-Scalar ($worldX - $cellOriginX)), Y: $(Format-Scalar $TileEntityOffsetY), Z: $(Format-Scalar ($worldZ - $cellOriginZ)) }")
    Add-Line $text ('    Rotation: { X: 0.0, Y: 0.0, Z: 0.0, W: 1.0 }')
    Add-Line $text ('    Scale: { X: 1.0, Y: 1.0, Z: 1.0 }')
    Add-Line $text ('  TerrainTile:')
    Add-Line $text ("    TerrainRoot: { Guid: $($TerrainRootGuid.ToString('D')), PackageId: $PackageId }")
    Add-Line $text ("    TileGuid: $($tileGuid.ToString('D'))")
    Add-Line $text ("    LayerSet: { Guid: $($LayerSetGuid.ToString('D')), PackageId: $PackageId }")
    Add-Line $text ("    Coordinate: { X: $tileX, Z: $tileZ }")
    Add-Line $text ("    WorldPlacement: { X: $(Format-Scalar $worldX), Y: 0.0, Z: $(Format-Scalar $worldZ) }")
    Add-Line $text ('    Visible: true')
    Add-Line $text ('    CastShadows: true')
    Add-Line $text ('    ReceiveShadows: true')
    Add-Line $text ('    PreferHighQuality: false')
    return $text.ToString()
}

function New-ClusterEntityBlock($cluster, [Guid]$cellSceneGuid, [Guid]$owningCellGuid)
{
    $text = [System.Text.StringBuilder]::new(1024)
    Add-Line $text ("- Guid: $((Get-ChildGuid $cellSceneGuid 'vegetation-cluster' "entry=$($cluster.EntryId)").ToString('D'))")
    Add-Line $text ("  Name: $WorldDisplayName Vegetation Cluster $($cluster.EntryId)")
    Add-Line $text ('  Transform:')
    Add-Line $text ('    Position: { X: 0, Y: 0, Z: 0 }')
    Add-Line $text ('    Rotation: { X: 0, Y: 0, Z: 0, W: 1 }')
    Add-Line $text ('    Scale: { X: 1, Y: 1, Z: 1 }')
    Add-Line $text ('  VegetationCluster:')
    Add-Line $text ("    Cluster: { Guid: $($cluster.ClusterGuid.ToString('D')), PackageId: $($cluster.ClusterPackageId) }")
    Add-Line $text ("    Biome: { Guid: $($cluster.BiomeGuid.ToString('D')), PackageId: $($cluster.BiomePackageId) }")
    Add-Line $text ("    Species: { Guid: $($cluster.SpeciesGuid.ToString('D')), PackageId: $($cluster.SpeciesPackageId) }")
    Add-Line $text ("    WorldGuid: $($WorldGuid.ToString('D'))")
    Add-Line $text ("    OwningCellGuid: $($owningCellGuid.ToString('D'))")
    Add-Line $text ("    Cell: { X: $($cluster.CellX), Y: $($cluster.CellY), Z: $($cluster.CellZ), Layer: $($cluster.Layer) }")
    Add-Line $text ("    Origin: { X: $(Format-RoundTrip $cluster.OriginX), Y: $(Format-RoundTrip $cluster.OriginY), Z: $(Format-RoundTrip $cluster.OriginZ) }")
    Add-Line $text ('    Bounds:')
    Add-Line $text ("      Min: { X: $(Format-RoundTrip $cluster.MinX), Y: $(Format-RoundTrip $cluster.MinY), Z: $(Format-RoundTrip $cluster.MinZ) }")
    Add-Line $text ("      Max: { X: $(Format-RoundTrip $cluster.MaxX), Y: $(Format-RoundTrip $cluster.MaxY), Z: $(Format-RoundTrip $cluster.MaxZ) }")
    Add-Line $text ('    Visible: true')
    Add-Line $text ('    CastShadows: true')
    Add-Line $text ('    ReceiveShadows: true')
    Add-Line $text ('    QualityGroup: 0')
    Add-Line $text ("    PageCount: $($cluster.PageCount)")
    Add-Line $text ("    InstanceCount: $($cluster.InstanceCount)")
    return $text.ToString()
}

function New-CellSceneText([int]$cellX, [int]$cellZ, $tileRecords, $clusterRecords)
{
    $cellOriginX = $WorldOrigin + ($cellX * $CellMetres)
    $cellOriginZ = $WorldOrigin + ($cellZ * $CellMetres)
    $cellSceneGuid = Get-CellSceneGuid $cellX $cellZ
    $text = [System.Text.StringBuilder]::new(8192)
    Add-Line $text ('Version: 2')
    Add-Line $text ("Name: $WorldDisplayName Cell $cellX,$cellZ")
    Add-Line $text ('ComponentSchemas:')
    Add-Line $text ('- TypeId: 1')
    Add-Line $text ('  Name: Transform')
    Add-Line $text ('  Version: 1')
    Add-Line $text ('  Required: true')
    Add-Line $text ('- TypeId: 1413829202')
    Add-Line $text ('  Name: TerrainTile')
    Add-Line $text ('  Version: 1')
    Add-Line $text ('  Required: true')
    if ($clusterRecords.Count -gt 0)
    {
        Add-Line $text ('- TypeId: 1447380803')
        Add-Line $text ('  Name: VegetationCluster')
        Add-Line $text ('  Version: 1')
        Add-Line $text ('  Required: false')
    }

    Add-Line $text ('Entities:')
    foreach ($tile in $tileRecords)
    {
        $block = (New-TileEntityBlock $tile.X $tile.Z $tile.Guid $cellOriginX $cellOriginZ).TrimEnd("`n")
        foreach ($entry in $block -split "`n")
        {
            Add-Line $text ($entry)
        }
    }

    $owningCellGuid = Get-WorldCellGuid $WorldGuid $cellX 0 $cellZ 'surface'
    foreach ($cluster in $clusterRecords)
    {
        $block = (New-ClusterEntityBlock $cluster $cellSceneGuid $owningCellGuid).TrimEnd("`n")
        foreach ($entry in $block -split "`n")
        {
            Add-Line $text ($entry)
        }
    }

    return $text.ToString()
}

function New-WorldText($cellRecords)
{
    $text = [System.Text.StringBuilder]::new(16384)
    Add-Line $text ('Version: 2')
    Add-Line $text ("WorldGuid: $($WorldGuid.ToString('D'))")
    Add-Line $text ("Name: $WorldDisplayName")
    Add-Line $text ('PersistentScene:')
    Add-Line $text ("  Guid: $($PersistentSceneGuid.ToString('D'))")
    Add-Line $text ("  PackageId: $PackageId")
    Add-Line $text ('Partition:')
    Add-Line $text ("  Origin: { X: $(Format-Scalar $WorldOrigin), Y: -64, Z: $(Format-Scalar $WorldOrigin) }")
    Add-Line $text ("  CellSize: { X: $(Format-Scalar $CellMetres), Y: 128, Z: $(Format-Scalar $CellMetres) }")
    Add-Line $text ('  LoadRadius: 1')
    Add-Line $text ('  UnloadHysteresis: 1')
    Add-Line $text ('  MaxActiveCells: 12')
    Add-Line $text ('Policy:')
    Add-Line $text ('  UnresolvedReferences: KeepUnresolved')
    Add-Line $text ('  UnloadedTargets: ClearAndLateResolve')
    Add-Line $text ('  DependencyCycles: Reject')
    Add-Line $text ('Layers:')
    Add-Line $text ('- Id: surface')
    Add-Line $text ('  Priority: 0')
    Add-Line $text ('Cells:')
    foreach ($cell in $cellRecords)
    {
        $centreX = $cell.OriginX + (0.5 * $CellMetres)
        $centreZ = $cell.OriginZ + (0.5 * $CellMetres)
        $surface = [MistfallValleyRaster]::HeightAt($centreX, $centreZ)
        $oversized = ($cell.X -eq ($CellsPerAxis - 1)) -and ($cell.Z -eq ($CellsPerAxis - 1))
        $cpuBytes = if ($oversized) { 2097152 } else { 1048576 }
        $gpuBytes = if ($oversized) { 134217728 } else { 67108864 }
        Add-Line $text ("- Coordinate: { X: $($cell.X), Y: 0, Z: $($cell.Z) }")
        Add-Line $text ('  Layer: surface')
        Add-Line $text ('  Scene:')
        Add-Line $text ("    Guid: $($cell.SceneGuid.ToString('D'))")
        Add-Line $text ("    PackageId: $PackageId")
        Add-Line $text ('  Bounds:')
        Add-Line $text ("    Min: { X: $(Format-Scalar $cell.OriginX), Y: -64, Z: $(Format-Scalar $cell.OriginZ) }")
        Add-Line $text ("    Max: { X: $(Format-Scalar ($cell.OriginX + $CellMetres)), Y: 64, Z: $(Format-Scalar ($cell.OriginZ + $CellMetres)) }")
        Add-Line $text ('  FocusBounds:')
        Add-Line $text ("    Min: { X: $(Format-Scalar ($centreX - 3.0)), Y: $(Format-Scalar ($surface - 1.0)), Z: $(Format-Scalar ($centreZ - 3.0)) }")
        Add-Line $text ("    Max: { X: $(Format-Scalar ($centreX + 3.0)), Y: $(Format-Scalar ($surface + 7.0)), Z: $(Format-Scalar ($centreZ + 3.0)) }")
        Add-Line $text ("  EstimatedCpuBytes: $cpuBytes")
        Add-Line $text ("  EstimatedGpuBytes: $gpuBytes")
    }

    return $text.ToString()
}

function New-OutcropBlock([string]$name, [int]$index, [double]$x, [double]$z, [double]$scale, [double]$rotationY, [double]$rotationW)
{
    $surface = [MistfallValleyRaster]::HeightAt($x, $z)
    $text = [System.Text.StringBuilder]::new(1024)
    Add-Line $text ("- Guid: 30000000-0000-0000-0000-00000000000$index")
    Add-Line $text ("  Name: $name")
    Add-Line $text ('  Transform:')
    Add-Line $text ('    Position:')
    Add-Line $text ("      X: $(Format-Scalar $x)")
    Add-Line $text ("      Y: $(Format-Scalar ($surface + (0.72 * $scale) - 0.12))")
    Add-Line $text ("      Z: $(Format-Scalar $z)")
    Add-Line $text ('    Rotation:')
    Add-Line $text ('      X: 0')
    Add-Line $text ("      Y: $(Format-Scalar $rotationY)")
    Add-Line $text ('      Z: 0')
    Add-Line $text ("      W: $(Format-Scalar $rotationW)")
    Add-Line $text ('    Scale:')
    Add-Line $text ("      X: $(Format-Scalar $scale)")
    Add-Line $text ("      Y: $(Format-Scalar $scale)")
    Add-Line $text ("      Z: $(Format-Scalar $scale)")
    Add-Line $text ('  MeshRenderer:')
    Add-Line $text ('    Mesh:')
    Add-Line $text ('      Guid: 89ae1524-c1c0-47c3-85a5-6a16838035f1')
    Add-Line $text ("      PackageId: $PackageId")
    Add-Line $text ('    Material:')
    Add-Line $text ('      Guid: e87b0335-5689-45a0-9fe1-455a01b59a20')
    Add-Line $text ("      PackageId: $PackageId")
    Add-Line $text ('    Visible: true')
    return $text.ToString()
}

function New-PersistentSceneText()
{
    $cameraX = -102.0
    $cameraZ = -128.0
    $cameraSurface = [MistfallValleyRaster]::HeightAt($cameraX, $cameraZ)
    $text = [System.Text.StringBuilder]::new(8192)
    Add-Line $text ('Version: 2')
    Add-Line $text ("Name: $WorldDisplayName Persistent Scene")
    Add-Line $text ('ComponentSchemas:')
    Add-Line $text ('- TypeId: 1')
    Add-Line $text ('  Name: Transform')
    Add-Line $text ('  Version: 1')
    Add-Line $text ('  Required: true')
    Add-Line $text ('- TypeId: 2')
    Add-Line $text ('  Name: Camera')
    Add-Line $text ('  Version: 2')
    Add-Line $text ('  Required: true')
    Add-Line $text ('- TypeId: 3')
    Add-Line $text ('  Name: MeshRenderer')
    Add-Line $text ('  Version: 1')
    Add-Line $text ('  Required: true')
    Add-Line $text ('- TypeId: 4')
    Add-Line $text ('  Name: DirectionalLight')
    Add-Line $text ('  Version: 1')
    Add-Line $text ('  Required: true')
    Add-Line $text ('- TypeId: 7')
    Add-Line $text ('  Name: Environment')
    Add-Line $text ('  Version: 1')
    Add-Line $text ('  Required: true')
    Add-Line $text ('Entities:')
    Add-Line $text ('- Guid: 30000000-0000-0000-0000-000000000001')
    Add-Line $text ('  Name: Main Camera')
    Add-Line $text ('  Transform:')
    Add-Line $text ('    Position:')
    Add-Line $text ("      X: $(Format-Scalar $cameraX)")
    Add-Line $text ("      Y: $(Format-Scalar ($cameraSurface + 2.3))")
    Add-Line $text ("      Z: $(Format-Scalar $cameraZ)")
    Add-Line $text ('    Rotation:')
    Add-Line $text ('      X: 0.0383067')
    Add-Line $text ('      Y: -0.2945279')
    Add-Line $text ('      Z: 0.0118165')
    Add-Line $text ('      W: 0.9548017')
    Add-Line $text ('    Scale:')
    Add-Line $text ('      X: 1')
    Add-Line $text ('      Y: 1')
    Add-Line $text ('      Z: 1')
    Add-Line $text ('  Camera:')
    Add-Line $text ('    VerticalFov: 45.0')
    Add-Line $text ('    NearPlane: 0.2')
    Add-Line $text ('    FarPlane: 900.0')
    Add-Line $text ('    IsPerspective: true')
    Add-Line $text ('- Guid: 30000000-0000-0000-0000-000000000002')
    Add-Line $text ('  Name: Dusk Key Light')
    Add-Line $text ('  DirectionalLight:')
    Add-Line $text ('    Direction:')
    Add-Line $text ('      X: -0.7524')
    Add-Line $text ('      Y: 0.1876')
    Add-Line $text ('      Z: 0.6314')
    Add-Line $text ('    Color:')
    Add-Line $text ('      X: 1.0')
    Add-Line $text ('      Y: 0.86')
    Add-Line $text ('      Z: 0.68')
    Add-Line $text ('    Intensity: 2.6')
    Add-Line $text ('    AmbientIntensity: 0.10')
    Add-Line $text ('    Enabled: true')
    Add-Line $text ('  Transform:')
    Add-Line $text ('    Position:')
    Add-Line $text ('      X: 0')
    Add-Line $text ('      Y: 2')
    Add-Line $text ('      Z: 0')
    Add-Line $text ('    Rotation:')
    Add-Line $text ('      X: 0')
    Add-Line $text ('      Y: 0')
    Add-Line $text ('      Z: 0')
    Add-Line $text ('      W: 1')
    Add-Line $text ('    Scale:')
    Add-Line $text ('      X: 1')
    Add-Line $text ('      Y: 1')
    Add-Line $text ('      Z: 1')
    Add-Line $text ('- Guid: 30000000-0000-0000-0000-000000000005')
    Add-Line $text ('  Name: Mistfall Dusk Environment')
    Add-Line $text ('  Environment:')
    Add-Line $text ('    EnvironmentTexture:')
    Add-Line $text ('      Guid: 1fc01236-449b-4097-88c0-223b1ed736ff')
    Add-Line $text ("      PackageId: $PackageId")
    Add-Line $text ('    SkyColor:')
    Add-Line $text ('      X: 0.24')
    Add-Line $text ('      Y: 0.36')
    Add-Line $text ('      Z: 0.62')
    Add-Line $text ('    HorizonColor:')
    Add-Line $text ('      X: 0.86')
    Add-Line $text ('      Y: 0.62')
    Add-Line $text ('      Z: 0.40')
    Add-Line $text ('    GroundColor:')
    Add-Line $text ('      X: 0.10')
    Add-Line $text ('      Y: 0.10')
    Add-Line $text ('      Z: 0.11')
    Add-Line $text ('    AmbientColor:')
    Add-Line $text ('      X: 0.44')
    Add-Line $text ('      Y: 0.52')
    Add-Line $text ('      Z: 0.66')
    Add-Line $text ('    SkyIntensity: 0.30')
    Add-Line $text ('    AmbientIntensity: 0.34')
    Add-Line $text ('    Exposure: 1.0')
    Add-Line $text ('    Enabled: true')
    foreach ($block in @(
        (New-OutcropBlock 'Valley Outcrop Boulder' 6 -118.2 -105.2 2.6 0.32557 0.94552),
        (New-OutcropBlock 'Valley Outcrop Shoulder' 7 -115.6 -104.4 1.9 -0.51504 0.85717),
        (New-OutcropBlock 'Valley Outcrop Cap' 8 -116.3 -108.0 1.5 0.91005 0.41452)))
    {
        foreach ($entry in ($block.TrimEnd("`n") -split "`n"))
        {
            Add-Line $text ($entry)
        }
    }

    return $text.ToString()
}

function Get-CellClusters($report, $cell)
{
    $records = @()
    foreach ($entry in $ScatterEntries)
    {
        $key = Get-ClusterKey $cell.X 0 $cell.Z 'surface' $entry.EntryId
        if (-not $report.ContainsKey($key))
        {
            # The bake produced no instances for this entry inside this cell, so the cell scene
            # authors no cluster for it.
            continue
        }

        $record = $report[$key]
        $expectedRecipeGuid = Get-RecipeGuid $cell.X $cell.Z $entry.EntryId
        if ($record.RecipeGuid -ne $expectedRecipeGuid)
        {
            throw "[MistfallValley] Vegetation cluster report is stale for '$key': baked from recipe " +
                "'$($record.RecipeGuid.ToString('D'))' instead of '$($expectedRecipeGuid.ToString('D'))'. Re-cook " +
                "the runtime assets before regenerating cell scenes."
        }

        if ($record.OriginX -ne [double]$cell.OriginX -or
            $record.OriginY -ne -64.0 -or
            $record.OriginZ -ne [double]$cell.OriginZ)
        {
            throw "[MistfallValley] Vegetation cluster report origin for '$key' does not match the authored cell origin."
        }

        $records += $record
    }

    return @($records | Sort-Object EntryId)
}

# --- Plan -------------------------------------------------------------------------------------

$tileRecords = [System.Collections.Generic.List[object]]::new()
for ($tileZ = 0; $tileZ -lt ($CellsPerAxis * $TilesPerCell); $tileZ++)
{
    for ($tileX = 0; $tileX -lt ($CellsPerAxis * $TilesPerCell); $tileX++)
    {
        $tileRecords.Add([pscustomobject]@{
            X = $tileX
            Z = $tileZ
            Guid = Get-TileGuid $tileX $tileZ
        })
    }
}

$cellRecords = [System.Collections.Generic.List[object]]::new()
for ($cellZ = 0; $cellZ -lt $CellsPerAxis; $cellZ++)
{
    for ($cellX = 0; $cellX -lt $CellsPerAxis; $cellX++)
    {
        $cellTiles = @($tileRecords | Where-Object {
            $_.X -ge ($cellX * $TilesPerCell) -and $_.X -lt (($cellX + 1) * $TilesPerCell) -and
            $_.Z -ge ($cellZ * $TilesPerCell) -and $_.Z -lt (($cellZ + 1) * $TilesPerCell)
        })
        $cellRecords.Add([pscustomobject]@{
            X = $cellX
            Z = $cellZ
            OriginX = $WorldOrigin + ($cellX * $CellMetres)
            OriginZ = $WorldOrigin + ($cellZ * $CellMetres)
            SceneGuid = Get-CellSceneGuid $cellX $cellZ
            Tiles = $cellTiles
        })
    }
}

$startupCell = $cellRecords | Where-Object {
    (-102.0 -ge $_.OriginX) -and (-102.0 -lt ($_.OriginX + $CellMetres)) -and
    (-128.0 -ge $_.OriginZ) -and (-128.0 -lt ($_.OriginZ + $CellMetres))
}

Write-Host "[MistfallValley] World '$WorldDisplayName' guid=$($WorldGuid.ToString('D'))"
Write-Host "[MistfallValley] Cells=$CellsPerAxis TilesPerCell=$TilesPerCell cell=$CellMetres m world=$WorldMetres m origin=$(Format-Scalar $WorldOrigin)"
Write-Host "[MistfallValley] Raster $WorldSamples x $WorldSamples samples ($(Format-Scalar ($WorldSamples * $SampleSpacing)) m) -> $heightPath"
Write-Host "[MistfallValley] Terrain root guid=$($TerrainRootGuid.ToString('D')) tiles=$($tileRecords.Count) -> $rootPath"
Write-Host "[MistfallValley] Cell scenes=$($cellRecords.Count) under $cellSceneRoot"
Write-Host "[MistfallValley] Scatter recipes=$($cellRecords.Count * $ScatterEntries.Count) under $vegetationRoot"
if (Test-Path -LiteralPath $clusterReportPath)
{
    Write-Host "[MistfallValley] Cooked cluster report found: $clusterReportPath"
}
else
{
    Write-Host "[MistfallValley] No cooked cluster report at $clusterReportPath; cell scenes keep their tiles only. Cook the runtime assets and rerun to author the baked clusters."
}

Write-Host "[MistfallValley] Startup camera (-102, -128) resolves to cell ($($startupCell.X),$($startupCell.Z))"
if ($DryRun)
{
    Write-Host '[MistfallValley] Dry run: no file written.'
    return
}

# --- Write raster, root, tiles, cells, world, recipes -----------------------------------------

$plan = [MistfallValleyRaster]::Build($WorldSamples)
Write-Bytes $heightPath ($plan.HeightBytes)
Write-Bytes $weightPath ($plan.WeightBytes)
Write-Text "$heightPath.meta" (New-GeneratedMeta (
    Get-ChildGuid $TerrainRootGuid 'terrain-height-source' 'height') 'TerrainHeightSource' 'Pgm16TerrainHeightImporter' 'terrain-height-source' 'height')
Write-Text "$weightPath.meta" (New-GeneratedMeta (
    Get-ChildGuid $TerrainRootGuid 'terrain-weight-source' 'weights') 'TerrainWeightSource' 'ArisenTerrainWeightSourceImporter' 'terrain-weight-source' 'weights')

Write-Text $rootPath (New-RootText $tileRecords)
Write-Text "$rootPath.meta" (New-SourceMeta $TerrainRootGuid 'TerrainRoot' 'ArisenTerrainRootImporter')

foreach ($tile in $tileRecords)
{
    $stubPath = Join-Path $generatedPath ("x_{0}_z_{1}.ariterraingenerated" -f $tile.X, $tile.Z)
    Write-Text $stubPath ("Generated terrain tile identity. Runtime payload is cooked from $WorldAssetName.aristerrain.`n")
    Write-Text "$stubPath.meta" (New-GeneratedMeta $tile.Guid 'TerrainTile' 'ArisenTerrainTileImporter' 'terrain-tile' ("x={0};z={1}" -f $tile.X, $tile.Z))
}

$clusterReport = Read-VegetationClusterReport $clusterReportPath
$clusterTotal = 0
foreach ($cell in $cellRecords)
{
    $script:cellX = $cell.X
    $script:cellZ = $cell.Z
    $cellClusters = Get-CellClusters $clusterReport $cell
    $clusterTotal += $cellClusters.Count
    $scenePath = Join-Path $cellSceneRoot ("Cell_{0}_{1}.arisenscene" -f $cell.X, $cell.Z)
    Write-Text $scenePath (New-CellSceneText $cell.X $cell.Z $cell.Tiles $cellClusters)
    Write-Text "$scenePath.meta" (New-SourceMeta $cell.SceneGuid 'Scene' 'ArisenSceneImporter')
    foreach ($entry in $ScatterEntries)
    {
        $recipeGuid = Get-RecipeGuid $cell.X $cell.Z $entry.EntryId
        $recipePath = Join-Path $vegetationRoot ("{0}_cell_{1}_{2}.arivegetationscatter" -f $entry.EntryId, $cell.X, $cell.Z)
        $text = [System.Text.StringBuilder]::new(512)
        Add-Line $text ('Version: 1')
        Add-Line $text ("RecipeGuid: $($recipeGuid.ToString('D'))")
        Add-Line $text ("World: { Guid: $($WorldGuid.ToString('D')), PackageId: $PackageId }")
        Add-Line $text ("Biome: { Guid: $($BiomeGuid.ToString('D')), PackageId: $PackageId }")
        Add-Line $text ("TerrainRoot: { Guid: $($TerrainRootGuid.ToString('D')), PackageId: $PackageId }")
        Add-Line $text ("Cell: { X: $($cell.X), Y: 0, Z: $($cell.Z), Layer: surface }")
        Add-Line $text ("EntryId: $($entry.EntryId)")
        Add-Line $text ("UnscaledConservativeRadius: $(Format-Scalar $entry.Radius)")
        Add-Line $text ('Exclusions: []')
        Write-Text $recipePath $text.ToString()
        Write-Text "$recipePath.meta" (New-SourceMeta $recipeGuid 'VegetationScatterRecipe' 'ArisenVegetationScatterRecipeImporter')
    }
}

Write-Text $worldPath (New-WorldText $cellRecords)
Write-Text "$worldPath.meta" (New-SourceMeta $WorldGuid 'World' 'ArisenWorldImporter')
Write-Text $persistentScenePath (New-PersistentSceneText)
Write-Text "$persistentScenePath.meta" (New-SourceMeta $PersistentSceneGuid 'Scene' 'ArisenSceneImporter')

Write-Host "[MistfallValley] Authored $clusterTotal vegetation cluster entity(ies) across the cell scenes."
Write-Host "[MistfallValley] Wrote $(($cellRecords.Count * $ScatterEntries.Count) + $tileRecords.Count + ($cellRecords.Count * 2) + 4) files."

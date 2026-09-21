<#
.SYNOPSIS
    Regenerates the ShowcaseValley terrain sources, root asset, tile stubs, and cell tile entities.

.DESCRIPTION
    The showcase valley is fixture content: one 16-bit PGM height raster, one RGBA8 weight raster,
    the terrain root that binds them, one generated tile identity per tile, and the terrain-tile
    entities that place those tiles inside the canonical world cell. Authoring that set by hand does
    not scale past the original two-by-two grid, so this script owns the bytes.

    The canonical cell (world -256..0 on X and Z, the original 513x513 window) is preserved bit for
    bit from the raster that is already committed, which is what keeps the baked showcase vegetation
    valid. Samples outside that window are generated and blended into the preserved edge so the
    extension reads as the same valley instead of a wall. The preserved window is always the
    top-left 513x513 corner of the current raster, so the script is idempotent: rerunning it with the
    same tile count reproduces the committed bytes, and rerunning it with a smaller count restores
    the smaller raster.

    Generated tile identities use the same rule as
    ArisenEngine.Core.Assets.GeneratedAssetIdentity.CreateChildGuid: SHA-256 over
    "arisen.generated-asset-child.v1\n{root:N}\n{packageId}\n{childKind}\n{childKey}", with the first
    sixteen digest bytes read as a .NET GUID. Terrain-tile entities use
    ArisenEngine.Terrain.TerrainTileEntityIdentity.Create over the cell scene and the tile GUID. Both
    loaders reject persisted records that do not match those derivations, so drift fails a test
    instead of leaving stale identities behind.

.PARAMETER Tiles
    Tiles per axis. Two reproduces the committed 256 m fixture byte for byte and is the regression
    check for this script; four produces a 512 m terrain.

.PARAMETER DryRun
    Compute and report the plan without writing any file.
#>
[CmdletBinding()]
param(
    [ValidateRange(2, 16)]
    [int]$Tiles = 4,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# --- Canonical fixture identity ---------------------------------------------------------------

$RootGuid = [Guid]'6f4d0a1c-0e85-4a42-93fe-34058ef48511'
$CellSceneGuid = [Guid]'506af06e-b16d-4573-b6c9-98548c370e90'
$LayerSetGuid = [Guid]'5dcaa6bd-2b51-498d-9fa9-bd68f4642761'
$PackageId = 'com.arisen.packagegame'
$AssetName = 'ShowcaseValley'
$DisplayName = 'Showcase Valley Terrain'
$TileEntityNameFormat = 'Showcase Valley Terrain Tile {0},{1}'

# --- Raster layout ----------------------------------------------------------------------------

$SampleSpacing = 0.5
$TileResolution = 257
$TileIntervals = $TileResolution - 1
$CanonicalSamples = 513
$Placement = -256.0
$HeightMaximum = 56.0

# The authored tile transforms mirror the original fixture offset. Terrain placement is read from
# the component's WorldPlacement, so the transform is scene bookkeeping only.
$TileEntityOffsetX = 256.0
$TileEntityOffsetY = 64.0
$TileEntityOffsetZ = 256.0

$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$packageRoot = Join-Path $repoRoot 'Arisen\Development\PackageGame\Local\com.arisen.packagegame'
$assetsRoot = Join-Path $packageRoot 'Assets\Terrain'
$heightPath = Join-Path $assetsRoot "Height\$AssetName.pgm"
$weightPath = Join-Path $assetsRoot "$AssetName.ariweights"
$rootPath = Join-Path $assetsRoot "$AssetName.aristerrain"
$generatedPath = Join-Path $assetsRoot "Generated\$AssetName"
$scenePath = Join-Path $packageRoot 'Assets\Scenes\TeapotCenterCell.arisenscene'

$width = ($Tiles * $TileIntervals) + 1
$height = $width
$extentMetres = ($width - 1) * $SampleSpacing

# --- Identity derivation ----------------------------------------------------------------------

function Get-ChildGuid([Guid]$sourceGuid, [string]$childKind, [string]$childKey)
{
    $identity = @(
        'arisen.generated-asset-child.v1'
        $sourceGuid.ToString('N')
        $PackageId.ToLowerInvariant()
        $childKind.ToLowerInvariant()
        $childKey
    ) -join "`n"
    $digest = [System.Security.Cryptography.SHA256]::HashData(
        [System.Text.Encoding]::UTF8.GetBytes($identity))
    $bytes = [byte[]]::new(16)
    [Array]::Copy($digest, $bytes, 16)
    return [Guid]::new($bytes)
}

# .NET GUID text is already the big-endian byte order the terrain identity domains hash.
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

function Get-TileGuid([int]$tileX, [int]$tileZ)
{
    return Get-ChildGuid $RootGuid 'terrain-tile' "x=$tileX;z=$tileZ"
}

# Mirrors ArisenEngine.Terrain.TerrainTileEntityIdentity.Create.
function Get-TileEntityGuid([Guid]$tileGuid)
{
    $domain = [System.Text.Encoding]::ASCII.GetBytes('Arisen.TerrainTileEntity.v1')
    $buffer = [byte[]]::new($domain.Length + 32)
    [Array]::Copy($domain, $buffer, $domain.Length)
    [Array]::Copy((Get-BigEndianBytes $CellSceneGuid), 0, $buffer, $domain.Length, 16)
    [Array]::Copy((Get-BigEndianBytes $tileGuid), 0, $buffer, $domain.Length + 16, 16)
    $digest = [System.Security.Cryptography.SHA256]::HashData($buffer)
    $digest[6] = [byte](($digest[6] -band 0x0F) -bor 0x50)
    $digest[8] = [byte](($digest[8] -band 0x3F) -bor 0x80)
    return Get-GuidFromBigEndianBytes $digest
}
# --- Raster synthesis kernel ------------------------------------------------------------------
# The heavy sample work runs in an embedded C# kernel: the loops cover (tiles * 256 + 1)^2 samples,
# which is far past what a PowerShell loop can chew through, and the kernel keeps the arithmetic in
# the same shape as the engine's own decoders.

$rasterSource = @'
using System;
using System.Globalization;
using System.IO;
using System.Text;

public static class ShowcaseValleyRaster
{
    private const int TileIntervals = 256;
    private const int CanonicalSamples = 513;
    private const int CanonicalLimit = CanonicalSamples - 1;
    private const int CanonicalCount = CanonicalSamples * CanonicalSamples;
    private const int WeightChannels = 4;
    private const int BlendSamples = 256;
    private const double SampleSpacing = 0.5;
    private const double Placement = -256.0;
    private const double HeightMaximum = 56.0;
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

    public static RasterResult Build(string heightPath, string weightPath, int tiles)
    {
        if (tiles < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tiles),
                "At least two tiles per axis are required.");
        }

        int width = (tiles * TileIntervals) + 1;
        ushort[] canonicalCodes = ReadCanonicalHeight(heightPath, out int sourceWidth);
        byte[] canonicalWeights = ReadCanonicalWeights(weightPath, sourceWidth);

        int sampleCount = checked(width * width);
        ushort[] codes = new ushort[sampleCount];
        double[] metres = new double[sampleCount];
        byte[] weights = new byte[checked(sampleCount * WeightChannels)];

        for (int row = 0; row < width; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int sample = (row * width) + column;
                if (column <= CanonicalLimit && row <= CanonicalLimit)
                {
                    int canonical = (row * CanonicalSamples) + column;
                    ushort code = canonicalCodes[canonical];
                    codes[sample] = code;
                    metres[sample] = (code / 65535.0) * HeightMaximum;
                    int source = canonical * WeightChannels;
                    int destination = sample * WeightChannels;
                    for (int channel = 0; channel < WeightChannels; channel++)
                    {
                        weights[destination + channel] = canonicalWeights[source + channel];
                    }

                    continue;
                }

                double x = Placement + (column * SampleSpacing);
                double z = Placement + (row * SampleSpacing);
                int distance = Math.Max(column - CanonicalLimit, row - CanonicalLimit);
                double blend = SmoothStep(distance / (double)BlendSamples);
                int seam = (Math.Min(row, CanonicalLimit) * CanonicalSamples) +
                    Math.Min(column, CanonicalLimit);
                double seamMetres = (canonicalCodes[seam] / 65535.0) * HeightMaximum;
                double value = (seamMetres * (1.0 - blend)) + (ProceduralHeight(x, z) * blend);
                value = Math.Clamp(value, 0.0, HeightMaximum);
                metres[sample] = value;
                codes[sample] = (ushort)Math.Round((value / HeightMaximum) * 65535.0);
                int seamSource = seam * WeightChannels;
                int weightDestination = sample * WeightChannels;
                for (int channel = 0; channel < WeightChannels; channel++)
                {
                    weights[weightDestination + channel] = canonicalWeights[seamSource + channel];
                }
            }
        }
        for (int row = 0; row < width; row++)
        {
            for (int column = 0; column < width; column++)
            {
                if (column <= CanonicalLimit && row <= CanonicalLimit)
                {
                    continue;
                }

                int sample = (row * width) + column;
                double x = Placement + (column * SampleSpacing);
                double z = Placement + (row * SampleSpacing);
                int distance = Math.Max(column - CanonicalLimit, row - CanonicalLimit);
                double blend = SmoothStep(distance / (double)BlendSamples);
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
                int seam = ((Math.Min(row, CanonicalLimit) * CanonicalSamples) +
                    Math.Min(column, CanonicalLimit)) * WeightChannels;
                int destination = sample * WeightChannels;
                weights[destination] = BlendWeight(canonicalWeights[seam], grass, blend);
                weights[destination + 1] = BlendWeight(canonicalWeights[seam + 1], rock, blend);
                weights[destination + 2] = BlendWeight(canonicalWeights[seam + 2], trail, blend);
                weights[destination + 3] = BlendWeight(canonicalWeights[seam + 3], 0.0, blend);
            }
        }

        return new RasterResult(
            width,
            EncodeHeight(width, codes),
            EncodeWeights(width, weights));
    }

    private static byte BlendWeight(byte seam, double target, double blend)
    {
        double blended = (seam * (1.0 - blend)) + (target * 255.0 * blend);
        return (byte)Math.Clamp(Math.Round(blended), 0.0, 255.0);
    }

    private static double SmoothStep(double value)
    {
        double clamped = Math.Clamp(value, 0.0, 1.0);
        return clamped * clamped * (3.0 - (2.0 * clamped));
    }

    private static double ProceduralHeight(double x, double z)
    {
        double ridgeX = Math.Max(0.0, x) / 320.0;
        double ridgeZ = Math.Max(0.0, z) / 320.0;
        double ridge = Math.Min(1.0, ridgeX + ridgeZ);
        double metres = 9.0 + (22.0 * ridge * ridge) +
            (9.0 * FractalNoise(x / 210.0, z / 210.0)) +
            (3.5 * FractalNoise(x / 46.0, z / 46.0));
        return Math.Clamp(metres, 0.0, HeightMaximum);
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
        double a = n00 + ((n10 - n00) * fx);
        double b = n01 + ((n11 - n01) * fx);
        return ((a + ((b - a) * fz)) * 2.0) - 1.0;
    }

    private static double HashUnit(int x, int y, int salt)
    {
        uint value = 0u;
        value ^= unchecked((uint)x * 0x9E3779B1u);
        value ^= unchecked((uint)y * 0x85EBCA77u);
        value ^= unchecked((uint)salt * 0xC2B2AE3Du);
        value ^= value >> 15;
        value *= 0x2545F491u;
        value ^= value >> 13;
        value ^= value >> 16;
        return value / 4294967296.0;
    }
    private static ushort[] ReadCanonicalHeight(string path, out int sourceWidth)
    {
        byte[] encoded = File.ReadAllBytes(path);
        int cursor = 0;
        Expect(encoded, ref cursor, "P5");
        SkipWhitespace(encoded, ref cursor);
        int width = ReadInteger(encoded, ref cursor);
        SkipWhitespace(encoded, ref cursor);
        int height = ReadInteger(encoded, ref cursor);
        SkipWhitespace(encoded, ref cursor);
        int maximum = ReadInteger(encoded, ref cursor);
        SkipWhitespace(encoded, ref cursor);
        if (width != height)
        {
            throw new InvalidDataException(
                $"Height source '{path}' is {width}x{height}; the fixture requires a square raster.");
        }

        if (maximum != 65535)
        {
            throw new InvalidDataException(
                $"Height source '{path}' declares maximum '{maximum}'; expected 65535.");
        }

        if (width < CanonicalSamples || ((width - 1) % TileIntervals) != 0)
        {
            throw new InvalidDataException(
                $"Height source '{path}' is {width} samples wide; expected (tiles * {TileIntervals}) " +
                $"+ 1 with at least {CanonicalSamples} samples.");
        }

        int sampleCount = checked(width * width);
        if (encoded.Length - cursor != sampleCount * 2)
        {
            throw new InvalidDataException(
                $"Height source '{path}' carries {encoded.Length - cursor} raster bytes; expected " +
                $"{sampleCount * 2}.");
        }

        sourceWidth = width;
        ushort[] codes = new ushort[CanonicalCount];
        for (int row = 0; row < CanonicalSamples; row++)
        {
            for (int column = 0; column < CanonicalSamples; column++)
            {
                int source = cursor + (2 * ((row * width) + column));
                codes[(row * CanonicalSamples) + column] =
                    (ushort)((encoded[source] << 8) | encoded[source + 1]);
            }
        }

        return codes;
    }

    private static byte[] ReadCanonicalWeights(string path, int sourceWidth)
    {
        byte[] encoded = File.ReadAllBytes(path);
        int cursor = 0;
        Expect(encoded, ref cursor, "ARIWEIGHTS");
        SkipWhitespace(encoded, ref cursor);
        int version = ReadInteger(encoded, ref cursor);
        if (version != 1)
        {
            throw new InvalidDataException(
                $"Weight source '{path}' declares schema '{version}'; expected 1.");
        }

        SkipWhitespace(encoded, ref cursor);
        int width = ReadInteger(encoded, ref cursor);
        SkipWhitespace(encoded, ref cursor);
        int height = ReadInteger(encoded, ref cursor);
        if (width != sourceWidth || height != sourceWidth)
        {
            throw new InvalidDataException(
                $"Weight source '{path}' is {width}x{height}; expected " +
                $"{sourceWidth}x{sourceWidth} to match the height source.");
        }

        SkipWhitespace(encoded, ref cursor);
        // Only the preserved top-left window is kept; a larger committed raster is still a valid
        // input because its extra samples are validated and skipped, which keeps the script
        // idempotent for any tile count at or above the canonical window.
        byte[] weights = new byte[CanonicalCount * WeightChannels];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                bool canonical = row <= CanonicalLimit && column <= CanonicalLimit;
                int destination = canonical
                    ? ((row * CanonicalSamples) + column) * WeightChannels
                    : -1;
                for (int channel = 0; channel < WeightChannels; channel++)
                {
                    if (cursor + 2 > encoded.Length)
                    {
                        throw new InvalidDataException(
                            $"Weight source '{path}' ended before all samples were read.");
                    }

                    int high = HexDigit(encoded[cursor], path);
                    int low = HexDigit(encoded[cursor + 1], path);
                    if (canonical)
                    {
                        weights[destination + channel] = (byte)((high << 4) | low);
                    }

                    cursor += 2;
                }

                if (cursor < encoded.Length && IsWhitespace(encoded[cursor]))
                {
                    cursor++;
                }
            }
        }

        for (int index = cursor; index < encoded.Length; index++)
        {
            if (!IsWhitespace(encoded[index]))
            {
                throw new InvalidDataException(
                    $"Weight source '{path}' contains trailing sample data.");
            }
        }

        return weights;
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

    private static void Expect(byte[] encoded, ref int cursor, string token)
    {
        for (int index = 0; index < token.Length; index++)
        {
            if (cursor + index >= encoded.Length || encoded[cursor + index] != (byte)token[index])
            {
                throw new InvalidDataException(
                    $"Terrain source is missing the '{token}' header signature.");
            }
        }

        cursor += token.Length;
    }

    private static void SkipWhitespace(byte[] encoded, ref int cursor)
    {
        while (cursor < encoded.Length && IsWhitespace(encoded[cursor]))
        {
            cursor++;
        }
    }

    private static bool IsWhitespace(byte value) =>
        value is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r';

    private static int ReadInteger(byte[] encoded, ref int cursor)
    {
        int start = cursor;
        while (cursor < encoded.Length && encoded[cursor] >= (byte)'0' && encoded[cursor] <= (byte)'9')
        {
            cursor++;
        }

        if (cursor == start)
        {
            throw new InvalidDataException("Terrain source is missing a decimal header field.");
        }

        return int.Parse(
            Encoding.ASCII.GetString(encoded, start, cursor - start),
            CultureInfo.InvariantCulture);
    }

    private static int HexDigit(byte value, string path)
    {
        if (value is >= (byte)'0' and <= (byte)'9')
        {
            return value - '0';
        }

        if (value is >= (byte)'a' and <= (byte)'f')
        {
            return value - 'a' + 10;
        }

        if (value is >= (byte)'A' and <= (byte)'F')
        {
            return value - 'A' + 10;
        }

        throw new InvalidDataException(
            $"Weight source '{path}' contains a non-hexadecimal digit.");
    }
}
'@
# --- Emission ---------------------------------------------------------------------------------

function New-RootSource($tileRecords)
{
    $text = [System.Text.StringBuilder]::new(4096)
    Add-Line $text ('Version: 2')
    Add-Line $text ("TerrainGuid: $($RootGuid.ToString('D'))")
    Add-Line $text ("Name: `"$DisplayName`"")
    Add-Line $text ("WorldPlacement: { X: $Placement, Y: 0, Z: $Placement }")
    Add-Line $text ("SampleSpacing: { X: $SampleSpacing, Z: $SampleSpacing }")
    Add-Line $text ("HeightRange: { Min: 0, Max: $HeightMaximum }")
    Add-Line $text ('HeightSource:')
    Add-Line $text ("  Path: `"Height/$AssetName.pgm`"")
    Add-Line $text ('  Format: Pgm16BigEndianScalar')
    Add-Line $text ('WeightSource:')
    Add-Line $text ("  Path: `"$AssetName.ariweights`"")
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
    Add-Line $text ("  SourceGuid: $($RootGuid.ToString('D'))")
    Add-Line $text ("  SourcePackageId: `"$PackageId`"")
    Add-Line $text ("  ChildKind: `"$childKind`"")
    Add-Line $text ("  ChildKey: `"$childKey`"")
    Add-Line $text ("  GeneratedByImporter: `"$importer`"")
    return $text.ToString()
}

function New-TileStubMeta([int]$tileX, [int]$tileZ)
{
    $childKey = "x=$tileX;z=$tileZ"
    return New-GeneratedMeta (Get-ChildGuid $RootGuid 'terrain-tile' $childKey) `
        'TerrainTile' 'ArisenTerrainTileImporter' 'terrain-tile' $childKey
}

function New-TileEntityBlock([int]$tileX, [int]$tileZ, [Guid]$tileGuid)
{
    $worldX = $Placement + ($tileX * $TileIntervals * $SampleSpacing)
    $worldZ = $Placement + ($tileZ * $TileIntervals * $SampleSpacing)
    $text = [System.Text.StringBuilder]::new(1024)
    Add-Line $text ("- Guid: $((Get-TileEntityGuid $tileGuid).ToString('D'))")
    Add-Line $text ("  Name: $($TileEntityNameFormat -f $tileX, $tileZ)")
    Add-Line $text ('  Transform:')
    Add-Line $text ("    Position: { X: $(Format-Scalar ($worldX + $TileEntityOffsetX)), Y: $(Format-Scalar $TileEntityOffsetY), Z: $(Format-Scalar ($worldZ + $TileEntityOffsetZ)) }")
    Add-Line $text ('    Rotation: { X: 0.0, Y: 0.0, Z: 0.0, W: 1.0 }')
    Add-Line $text ('    Scale: { X: 1.0, Y: 1.0, Z: 1.0 }')
    Add-Line $text ('  TerrainTile:')
    Add-Line $text ("    TerrainRoot: { Guid: $($RootGuid.ToString('D')), PackageId: $PackageId }")
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

# The fixture text uses LF endings and invariant scalars: an authored "0.0" must round-trip as "0.0".
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
    [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))
}

# Rebuilds the cell scene so its terrain-tile entities match the generated tiles while every other
# entity keeps its authored lines.
function Update-SceneTiles([string]$path, $tileRecords)
{
    $lines = [System.IO.File]::ReadAllLines($path)
    $entityStart = -1
    for ($index = 0; $index -lt $lines.Count; $index++)
    {
        if ($lines[$index] -eq 'Entities:')
        {
            $entityStart = $index
            break
        }
    }

    if ($entityStart -lt 0)
    {
        throw "Scene '$path' has no Entities block."
    }

    $before = [System.Collections.Generic.List[string]]::new()
    $after = [System.Collections.Generic.List[string]]::new()
    $current = $null
    $target = $before
    $sawTerrain = $false

    for ($index = $entityStart + 1; $index -lt $lines.Count; $index++)
    {
        $line = $lines[$index]
        if ($line.StartsWith('- Guid: ', [StringComparison]::Ordinal))
        {
            if ($null -ne $current -and -not $current.IsTerrain)
            {
                foreach ($entry in $current.Lines)
                {
                    $target.Add($entry)
                }
            }

            if ($null -ne $current -and $current.IsTerrain)
            {
                $sawTerrain = $true
                $target = $after
            }

            $current = @{
                Lines     = [System.Collections.Generic.List[string]]::new()
                IsTerrain = $false
            }
        }
        elseif ($null -eq $current)
        {
            if (-not [string]::IsNullOrWhiteSpace($line))
            {
                throw "Scene '$path' has content outside an entity block."
            }

            continue
        }

        $current.Lines.Add($line)
        if ($line -eq '  TerrainTile:')
        {
            $current.IsTerrain = $true
        }
    }

    if ($null -ne $current -and -not $current.IsTerrain)
    {
        foreach ($entry in $current.Lines)
        {
            $target.Add($entry)
        }
    }

    $text = [System.Text.StringBuilder]::new(16384)
    for ($index = 0; $index -le $entityStart; $index++)
    {
        Add-Line $text ($lines[$index])
    }

    foreach ($entry in $before)
    {
        Add-Line $text ($entry)
    }

    foreach ($tile in $tileRecords)
    {
        $block = (New-TileEntityBlock $tile.X $tile.Z $tile.Guid).TrimEnd("`n")
        foreach ($entry in $block -split "`n")
        {
            Add-Line $text ($entry)
        }
    }

    foreach ($entry in $after)
    {
        Add-Line $text ($entry)
    }

    Write-Text $path $text.ToString()
}

# --- Run --------------------------------------------------------------------------------------

if (-not ('ShowcaseValleyRaster' -as [type]))
{
    Add-Type -TypeDefinition $rasterSource -Language CSharp
}

$tileRecords = @()
for ($tileZ = 0; $tileZ -lt $Tiles; $tileZ++)
{
    for ($tileX = 0; $tileX -lt $Tiles; $tileX++)
    {
        $tileRecords += [pscustomobject]@{
            X    = $tileX
            Z    = $tileZ
            Guid = Get-TileGuid $tileX $tileZ
        }
    }
}

$raster = [ShowcaseValleyRaster]::Build($heightPath, $weightPath, $Tiles)
if ($raster.Width -ne $width)
{
    throw "Raster kernel produced width $($raster.Width); expected $width."
}

if (-not $DryRun)
{
    if (-not (Test-Path $generatedPath))
    {
        [void](New-Item -ItemType Directory -Path $generatedPath -Force)
    }

    [System.IO.File]::WriteAllBytes($heightPath, $raster.HeightBytes)
    Write-Text "$heightPath.meta" (New-GeneratedMeta `
        (Get-ChildGuid $RootGuid 'terrain-height-source' 'height') `
        'TerrainHeightSource' 'Pgm16TerrainHeightImporter' 'terrain-height-source' 'height')
    [System.IO.File]::WriteAllBytes($weightPath, $raster.WeightBytes)
    Write-Text "$weightPath.meta" (New-GeneratedMeta `
        (Get-ChildGuid $RootGuid 'terrain-weight-source' 'weights') `
        'TerrainWeightSource' 'ArisenTerrainWeightSourceImporter' 'terrain-weight-source' 'weights')
    Write-Text $rootPath (New-RootSource $tileRecords)
    Write-Text "$rootPath.meta" ("Guid: $($RootGuid.ToString('D'))`n" +
        "AssetType: `"TerrainRoot`"`nImporter: `"ArisenTerrainRootImporter`"`n")

    $expected = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($tile in $tileRecords)
    {
        $stubName = "x_$($tile.X)_z_$($tile.Z).ariterraingenerated"
        [void]$expected.Add($stubName)
        [void]$expected.Add("$stubName.meta")
        Write-Text (Join-Path $generatedPath $stubName) `
            "Generated terrain tile identity. Runtime payload is cooked from $AssetName.aristerrain.`n"
        Write-Text (Join-Path $generatedPath "$stubName.meta") (New-TileStubMeta $tile.X $tile.Z)
    }

    foreach ($stale in Get-ChildItem -LiteralPath $generatedPath -File)
    {
        if (-not $expected.Contains($stale.Name))
        {
            Write-Host "[showcase] removing stale tile stub $($stale.Name)"
            [System.IO.File]::Delete($stale.FullName)
        }
    }

    Update-SceneTiles $scenePath $tileRecords
}

Write-Host "[showcase] tiles=$Tiles raster=${width}x${height} extent=$extentMetres m spacing=$SampleSpacing"
Write-Host "[showcase] height bytes=$($raster.HeightBytes.Length) weight bytes=$($raster.WeightBytes.Length) tile count=$($tileRecords.Count)"
foreach ($tile in $tileRecords)
{
    Write-Host ("[showcase] tile ({0},{1}) -> {2} entity {3}" -f `
        $tile.X, $tile.Z, $tile.Guid.ToString('D'), (Get-TileEntityGuid $tile.Guid).ToString('D'))
}
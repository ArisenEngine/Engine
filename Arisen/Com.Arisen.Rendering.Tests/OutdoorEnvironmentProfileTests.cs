using System.Buffers.Binary;
using ArisenEngine.Core.Assets;
using ArisenEngine.Rendering.Resources;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class OutdoorEnvironmentProfileTests
{
    [Fact]
    public void VersionOneSourceDefaultsToPanoramaWithoutAtmosphere()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp, version: 1, outdoorYaml: string.Empty);

        EnvironmentTextureAsset asset = EnvironmentTextureAssetLoader.LoadSource(
            fixture.Database,
            fixture.EnvironmentGuid);

        Assert.Equal(OutdoorEnvironmentProfile.Disabled, asset.OutdoorProfile);
        Assert.Equal(EnvironmentSkyMode.Panorama, asset.OutdoorProfile.SkyMode);
        Assert.False(asset.OutdoorProfile.IsAtmosphereEnabled);
    }

    [Fact]
    public void VersionTwoOutdoorProfileRoundTripsThroughCookedOnlyRuntime()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp, version: 2, ValidOutdoorYaml);
        EnvironmentTextureAsset source = EnvironmentTextureAssetLoader.LoadSource(
            fixture.Database,
            fixture.EnvironmentGuid);

        Assert.Equal(EnvironmentSkyMode.ProceduralOutdoor, source.OutdoorProfile.SkyMode);
        Assert.Equal(EnvironmentExposurePolicy.Fixed, source.OutdoorProfile.ExposurePolicy);
        Assert.True(source.OutdoorProfile.AerialPerspectiveEnabled);
        Assert.True(source.OutdoorProfile.HeightFogEnabled);
        Assert.True(source.OutdoorProfile.IsAtmosphereEnabled);
        Assert.Equal(1.25f, source.OutdoorProfile.ResolveExposure(0.5f));

        CookedEnvironmentTexture cooked = EnvironmentTextureAssetCooker.LoadOrCook(
            fixture.Database,
            source);
        ReadOnlyMemory<byte> cookedBytes = fixture.Database.GetCookedAssetBytes(cooked.Handle);
        Assert.Equal(
            EnvironmentTextureAssetCooker.CookedFormatVersion,
            BinaryPrimitives.ReadInt32LittleEndian(cookedBytes.Span.Slice(8, 4)));
        Assert.Equal(144, cooked.PixelDataOffset);
        Assert.Equal(source.OutdoorProfile, cooked.OutdoorProfile);
        fixture.Database.Release(cooked.Handle);

        fixture.Database.UseReadOnlyRuntime();
        CookedEnvironmentTexture runtime = EnvironmentTextureAssetCooker.LoadCooked(
            fixture.Database,
            fixture.EnvironmentGuid);
        Assert.Equal(source.OutdoorProfile, runtime.Asset.OutdoorProfile);
        Assert.Equal(source.OutdoorProfile, runtime.OutdoorProfile);
        Assert.Equal(64, EnvironmentTextureAssetCooker.GetPixelData(
            fixture.Database.GetCookedAssetBytes(runtime.Handle)).Length);
        fixture.Database.Release(runtime.Handle);
    }

    [Fact]
    public void VersionTwoRejectsOutOfRangeProfileValues()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(
            temp,
            version: 2,
            ValidOutdoorYaml.Replace(
                "SunSkyCoupling: 0.75",
                "SunSkyCoupling: 1.25",
                StringComparison.Ordinal));

        var error = Assert.Throws<InvalidOperationException>(
            () => EnvironmentTextureAssetLoader.LoadSource(
                fixture.Database,
                fixture.EnvironmentGuid));

        Assert.Contains("SunSkyCoupling", error.Message, StringComparison.Ordinal);
        Assert.Contains("[0, 1]", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileValidationRejectsNonFiniteValues()
    {
        OutdoorEnvironmentProfile profile = OutdoorEnvironmentProfile.Disabled with
        {
            HeightFogDensity = float.NaN
        };

        var error = Assert.Throws<InvalidOperationException>(
            () => profile.Validate("non-finite test"));

        Assert.Contains("HeightFogDensity", error.Message, StringComparison.Ordinal);
        Assert.Contains("finite", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyCookedHeaderLoadsWithDisabledOutdoorProfile()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp, version: 1, outdoorYaml: string.Empty);
        CookedEnvironmentTexture current = EnvironmentTextureAssetCooker.LoadOrCook(
            fixture.Database,
            fixture.EnvironmentGuid);
        Assert.True(fixture.Database.TryGetCookedArtifact(
            fixture.EnvironmentGuid,
            current.Variant,
            out CookedAssetRecord artifact));
        byte[] currentBytes = File.ReadAllBytes(artifact.Path);
        byte[] legacyBytes = new byte[64 + current.PixelDataSize];
        currentBytes.AsSpan(0, 64).CopyTo(legacyBytes);
        BinaryPrimitives.WriteInt32LittleEndian(legacyBytes.AsSpan(8, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(legacyBytes.AsSpan(36, 4), 0);
        currentBytes.AsSpan(current.PixelDataOffset, current.PixelDataSize)
            .CopyTo(legacyBytes.AsSpan(64));
        fixture.Database.Release(current.Handle);
        File.WriteAllBytes(artifact.Path, legacyBytes);
        RegisterArtifact(fixture.Database, artifact);

        CookedEnvironmentTexture legacy = EnvironmentTextureAssetCooker.LoadCooked(
            fixture.Database,
            fixture.EnvironmentGuid);

        Assert.Equal(64, legacy.PixelDataOffset);
        Assert.Equal(OutdoorEnvironmentProfile.Disabled, legacy.OutdoorProfile);
        Assert.Equal(64, EnvironmentTextureAssetCooker.GetPixelData(
            fixture.Database.GetCookedAssetBytes(legacy.Handle)).Length);
        fixture.Database.Release(legacy.Handle);
    }

    [Fact]
    public void SourceEnabledCookerRecooksNewerLegacyArtifact()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp, version: 2, ValidOutdoorYaml);
        EnvironmentTextureAsset source = EnvironmentTextureAssetLoader.LoadSource(
            fixture.Database,
            fixture.EnvironmentGuid);
        CookedEnvironmentTexture first = EnvironmentTextureAssetCooker.LoadOrCook(
            fixture.Database,
            source);
        Assert.True(fixture.Database.TryGetCookedArtifact(
            fixture.EnvironmentGuid,
            first.Variant,
            out CookedAssetRecord artifact));
        fixture.Database.Release(first.Handle);

        byte[] staleBytes = File.ReadAllBytes(artifact.Path);
        BinaryPrimitives.WriteInt32LittleEndian(staleBytes.AsSpan(8, 4), 1);
        File.WriteAllBytes(artifact.Path, staleBytes);
        File.SetLastWriteTimeUtc(artifact.Path, DateTime.UtcNow.AddHours(1));
        RegisterArtifact(fixture.Database, artifact);

        CookedEnvironmentTexture recooked = EnvironmentTextureAssetCooker.LoadOrCook(
            fixture.Database,
            source);
        ReadOnlySpan<byte> recookedBytes = fixture.Database
            .GetCookedAssetBytes(recooked.Handle)
            .Span;

        Assert.Equal(
            EnvironmentTextureAssetCooker.CookedFormatVersion,
            BinaryPrimitives.ReadInt32LittleEndian(recookedBytes.Slice(8, 4)));
        Assert.Equal(source.OutdoorProfile, recooked.OutdoorProfile);
        fixture.Database.Release(recooked.Handle);
    }

    private static readonly string ValidOutdoorYaml = """
        Outdoor:
          SkyMode: ProceduralOutdoor
          SunSkyCoupling: 0.75
          HorizonExponent: 0.65
          ZenithExponent: 1.4
          SunAngularRadiusDegrees: 0.53
          SunDiscIntensity: 6.0
          SunGlowIntensity: 0.3
          SunGlowExponent: 72.0
          AerialPerspectiveEnabled: true
          AerialStartDistance: 20.0
          AerialDistance: 120.0
          AerialStrength: 0.45
          HeightFogEnabled: true
          HeightFogBaseHeight: 0.0
          HeightFogDensity: 0.006
          HeightFogFalloff: 0.2
          ExposurePolicy: Fixed
          FixedExposure: 1.25
        """;

    private static EnvironmentFixture CreateFixture(
        TempDirectory temp,
        int version,
        string outdoorYaml)
    {
        Guid environmentGuid = Guid.NewGuid();
        Guid textureGuid = Guid.NewGuid();
        string texturePath = temp.Write(
            "Assets/Environment.ppm",
            "P3\n4 2\n255\n255 128 0  64 128 255  0 32 128  255 255 255\n16 16 32  32 32 64  64 64 96  128 128 160\n");
        string environmentPath = temp.Write(
            "Assets/Environment.arienvironment",
            $$"""
            Version: {{version}}
            Name: Outdoor Test
            SourceTexture:
              Guid: {{textureGuid:D}}
              PackageId: com.arisen.test
            Layout: LatLong
            SourceColorSpace: SRgb
            RuntimeFormat: R16G16B16A16SFloat
            RotationDegrees: 18.0
            Intensity: 1.35
            {{outdoorYaml}}
            """);
        var database = new TestAssetDatabase(
            AssetSourceAccessMode.Diagnostic,
            Path.Combine(temp.Root, "Cooked"));
        database.AddAsset(textureGuid, "Texture2D", texturePath);
        database.AddAsset(environmentGuid, "EnvironmentTexture", environmentPath);
        return new EnvironmentFixture(database, environmentGuid);
    }

    private static void RegisterArtifact(
        TestAssetDatabase database,
        CookedAssetRecord previous)
    {
        var info = new FileInfo(previous.Path);
        database.RegisterCookedArtifact(new CookedAssetRecord(
            previous.Guid,
            previous.AssetType,
            previous.Variant,
            info.FullName,
            info.Length,
            info.LastWriteTimeUtc));
    }

    private readonly record struct EnvironmentFixture(
        TestAssetDatabase Database,
        Guid EnvironmentGuid);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "ArisenOutdoorEnvironmentTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string Write(string relativePath, string contents)
        {
            string path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}

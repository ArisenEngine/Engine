using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using ArisenEngine.Core.Assets;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationCookedInstancePageTests
{
    internal static readonly Guid ClusterGuid =
        Guid.Parse("c1100000-0000-0000-0000-000000000001");
    internal static readonly Guid PageGuid =
        Guid.Parse("c1200000-0000-0000-0000-000000000001");
    internal static readonly Guid SpeciesAGuid =
        Guid.Parse("c1300000-0000-0000-0000-000000000001");
    internal static readonly Guid SpeciesBGuid =
        Guid.Parse("c1300000-0000-0000-0000-000000000002");
    internal const string PackageId = "com.arisen.vegetation.test";

    private static readonly WorldPosition s_Origin = new(4096.25, -32.5, -8192.75);

    [Fact]
    public void InstancePageV1_RoundTripsCanonicalInstancesSpeciesBoundsAndDependencies()
    {
        using var fixture = new PageFixture();
        VegetationInstancePageCookDescriptor forwardDescriptor = CreateDescriptor();
        VegetationInstancePageCookDescriptor permutedDescriptor = PermuteDescriptor(
            forwardDescriptor,
            negateQuaternions: false);
        VegetationInstancePageCookDescriptor oppositeQuaternionDescriptor = PermuteDescriptor(
            forwardDescriptor,
            negateQuaternions: true);

        CookedVegetationInstancePage forward = VegetationInstancePageAssetCooker.BuildForCook(
            fixture.Database,
            forwardDescriptor);
        CookedVegetationInstancePage permuted = VegetationInstancePageAssetCooker.BuildForCook(
            fixture.Database,
            permutedDescriptor);
        CookedVegetationInstancePage oppositeQuaternion =
            VegetationInstancePageAssetCooker.BuildForCook(
                fixture.Database,
                oppositeQuaternionDescriptor);
        byte[] forwardBytes = VegetationInstancePageAssetCooker.WritePayload(forward);
        byte[] permutedBytes = VegetationInstancePageAssetCooker.WritePayload(permuted);
        byte[] oppositeQuaternionBytes =
            VegetationInstancePageAssetCooker.WritePayload(oppositeQuaternion);

        Assert.Equal(forwardBytes, permutedBytes);
        Assert.Equal(forwardBytes, oppositeQuaternionBytes);
        Assert.True(
            VegetationInstancePageAssetCooker.TryReadPayload(
                PageGuid,
                ClusterGuid,
                PackageId,
                forwardBytes,
                "instance-page-memory",
                out CookedVegetationInstancePage loaded,
                out string diagnostic),
            diagnostic);

        Assert.Equal(PageGuid, loaded.Guid);
        Assert.Equal(ClusterGuid, loaded.ClusterGuid);
        Assert.Equal(PackageId, loaded.PackageId);
        Assert.Equal(s_Origin, loaded.Origin);
        Assert.Equal([SpeciesAGuid, SpeciesBGuid], loaded.Species.Select(species => species.Guid));
        Assert.Equal([0x10UL, 0x20UL, 0x30UL], loaded.Instances.Select(instance => instance.StableKey));
        Assert.Equal([0U, 1U, 1U], loaded.Instances.Select(instance => instance.SpeciesIndex));
        Assert.Equal(CalculateBounds(loaded), loaded.Bounds);

        VegetationCookedAssetDependency[] dependencies =
            VegetationInstancePageAssetCooker.BuildDependencies(loaded);
        Assert.Equal(2, dependencies.Length);
        Assert.Equal([SpeciesAGuid, SpeciesBGuid], dependencies.Select(dependency => dependency.Guid));
        Assert.All(dependencies, dependency =>
        {
            Assert.Equal(PackageId, dependency.PackageId);
            Assert.Equal(VegetationAssetTypes.Species, dependency.AssetType);
            Assert.Equal(VegetationSpeciesAssetCooker.RuntimeVariant, dependency.Variant);
            Assert.True(dependency.Required);
        });
    }

    [Theory]
    [InlineData(InvalidPageCase.ZeroStableKey)]
    [InlineData(InvalidPageCase.DuplicateStableKey)]
    [InlineData(InvalidPageCase.NegativeSpeciesIndex)]
    [InlineData(InvalidPageCase.SpeciesIndexOutsideTable)]
    [InlineData(InvalidPageCase.NonFiniteOrigin)]
    [InlineData(InvalidPageCase.NonFiniteLocalPosition)]
    [InlineData(InvalidPageCase.ZeroScale)]
    [InlineData(InvalidPageCase.ZeroRadius)]
    [InlineData(InvalidPageCase.ZeroQuaternion)]
    [InlineData(InvalidPageCase.NonUnitQuaternion)]
    public void InstancePageV1_InvalidDescriptorFailsBeforePayloadPublication(
        InvalidPageCase invalidCase)
    {
        using var fixture = new PageFixture();
        VegetationInstancePageCookDescriptor invalid = CreateInvalidDescriptor(invalidCase);

        Assert.Throws<InvalidOperationException>(() =>
            VegetationInstancePageAssetCooker.BuildForCook(fixture.Database, invalid));
        Assert.False(fixture.Database.TryGetCookedArtifact(
            PageGuid,
            VegetationInstancePageAssetCooker.RuntimeVariant,
            out _));
        Assert.False(Directory.Exists(fixture.Database.CookedRoot));
    }

    [Fact]
    public void InstancePageV1_RejectsRehashedStructuralAndTransformCorruption()
    {
        using var fixture = new PageFixture();
        CookedVegetationInstancePage page = VegetationInstancePageAssetCooker.BuildForCook(
            fixture.Database,
            CreateDescriptor());
        byte[] valid = VegetationInstancePageAssetCooker.WritePayload(page);
        int stringsDescriptor = FindSectionDescriptor(valid, 2);
        int speciesDescriptor = FindSectionDescriptor(valid, 3);
        int instancesDescriptor = FindSectionDescriptor(valid, 4);
        int instancesOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(
            valid.AsSpan(instancesDescriptor + 8)));

        byte[] wrongMagic = valid.ToArray();
        wrongMagic[0] ^= 0xff;
        AssertRejected(wrongMagic);

        byte[] truncated = valid[..^1];
        AssertRejected(truncated);

        byte[] wrongHash = valid.ToArray();
        wrongHash[^1] ^= 0x01;
        AssertRejected(wrongHash);

        byte[] physicallyReordered = valid.ToArray();
        ulong speciesOffset = BinaryPrimitives.ReadUInt64LittleEndian(
            physicallyReordered.AsSpan(speciesDescriptor + 8));
        ulong instancesOffsetValue = BinaryPrimitives.ReadUInt64LittleEndian(
            physicallyReordered.AsSpan(instancesDescriptor + 8));
        BinaryPrimitives.WriteUInt64LittleEndian(
            physicallyReordered.AsSpan(speciesDescriptor + 8),
            instancesOffsetValue);
        BinaryPrimitives.WriteUInt64LittleEndian(
            physicallyReordered.AsSpan(instancesDescriptor + 8),
            speciesOffset);
        Rehash(physicallyReordered);
        AssertRejected(physicallyReordered);

        byte[] alignedGap = valid.ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(
            alignedGap.AsSpan(speciesDescriptor + 8),
            checked(speciesOffset + 8));
        Rehash(alignedGap);
        AssertRejected(alignedGap);

        byte[] zeroSizeAlias = valid.ToArray();
        ulong stringsOffset = BinaryPrimitives.ReadUInt64LittleEndian(
            zeroSizeAlias.AsSpan(stringsDescriptor + 8));
        BinaryPrimitives.WriteUInt64LittleEndian(
            zeroSizeAlias.AsSpan(speciesDescriptor + 8),
            stringsOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(
            zeroSizeAlias.AsSpan(speciesDescriptor + 16),
            0);
        BinaryPrimitives.WriteUInt32LittleEndian(
            zeroSizeAlias.AsSpan(speciesDescriptor + 24),
            0);
        Rehash(zeroSizeAlias);
        AssertRejected(zeroSizeAlias);

        byte[] oppositeHemisphere = valid.ToArray();
        for (int offset = 24; offset < 32; offset += sizeof(short))
        {
            short component = BinaryPrimitives.ReadInt16LittleEndian(
                oppositeHemisphere.AsSpan(instancesOffset + offset));
            BinaryPrimitives.WriteInt16LittleEndian(
                oppositeHemisphere.AsSpan(instancesOffset + offset),
                checked((short)-component));
        }
        Rehash(oppositeHemisphere);
        AssertRejected(oppositeHemisphere);

        byte[] nonUnitQuaternion = valid.ToArray();
        nonUnitQuaternion.AsSpan(instancesOffset + 24, 8).Clear();
        BinaryPrimitives.WriteInt16LittleEndian(
            nonUnitQuaternion.AsSpan(instancesOffset + 30),
            1024);
        Rehash(nonUnitQuaternion);
        AssertRejected(nonUnitQuaternion);

        byte[] negativeZeroScale = valid.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            negativeZeroScale.AsSpan(instancesOffset + 32),
            0x80000000U);
        Rehash(negativeZeroScale);
        AssertRejected(negativeZeroScale);

        byte[] outsideBounds = valid.ToArray();
        BinaryPrimitives.WriteSingleLittleEndian(
            outsideBounds.AsSpan(instancesOffset + 12),
            100_000.0f);
        Rehash(outsideBounds);
        AssertRejected(outsideBounds);
    }

    internal static VegetationInstancePageCookDescriptor CreateDescriptor(
        Guid? pageGuid = null,
        Guid? clusterGuid = null,
        ulong keyOffset = 0)
    {
        CookedVegetationSpeciesReference speciesA = new(SpeciesAGuid, PackageId);
        CookedVegetationSpeciesReference speciesB = new(SpeciesBGuid, PackageId);
        return new VegetationInstancePageCookDescriptor(
            pageGuid ?? PageGuid,
            clusterGuid ?? ClusterGuid,
            PackageId,
            GeneratedSchemaVersion: 1,
            s_Origin,
            Array.AsReadOnly<CookedVegetationSpeciesReference>([speciesB, speciesA]),
            Array.AsReadOnly<VegetationCookedInstanceInput>(
            [
                new(
                    0x30UL + keyOffset,
                    SpeciesIndex: 0,
                    new Vector3(2.25f, 1.5f, -3.75f),
                    Quaternion.CreateFromYawPitchRoll(0.3f, 0.1f, -0.2f),
                    UniformScale: 1.25f,
                    ConservativeRadius: 2.0f),
                new(
                    0x10UL + keyOffset,
                    SpeciesIndex: 1,
                    new Vector3(-1.5f, 0.5f, 4.0f),
                    Quaternion.Identity,
                    UniformScale: 0.75f,
                    ConservativeRadius: 1.0f),
                new(
                    0x20UL + keyOffset,
                    SpeciesIndex: 0,
                    new Vector3(0.25f, -0.75f, 0.5f),
                    Quaternion.CreateFromYawPitchRoll(-0.4f, 0.2f, 0.15f),
                    UniformScale: 2.0f,
                    ConservativeRadius: 0.5f)
            ]));
    }

    internal static VegetationInstancePageCookDescriptor PermuteDescriptor(
        VegetationInstancePageCookDescriptor source,
        bool negateQuaternions)
    {
        CookedVegetationSpeciesReference[] species = source.Species.Reverse().ToArray();
        VegetationCookedInstanceInput[] instances = source.Instances
            .Reverse()
            .Select(instance => instance with
            {
                SpeciesIndex = source.Species.Count - 1 - instance.SpeciesIndex,
                Orientation = negateQuaternions
                    ? Negate(instance.Orientation)
                    : instance.Orientation
            })
            .ToArray();
        return source with
        {
            Species = Array.AsReadOnly(species),
            Instances = Array.AsReadOnly(instances)
        };
    }

    private static VegetationInstancePageCookDescriptor CreateInvalidDescriptor(
        InvalidPageCase invalidCase)
    {
        VegetationInstancePageCookDescriptor descriptor = CreateDescriptor();
        if (invalidCase == InvalidPageCase.NonFiniteOrigin)
        {
            return descriptor with
            {
                Origin = new WorldPosition(double.NaN, 0.0, 0.0)
            };
        }

        VegetationCookedInstanceInput[] instances = descriptor.Instances.ToArray();
        instances[0] = invalidCase switch
        {
            InvalidPageCase.ZeroStableKey => instances[0] with { StableKey = 0 },
            InvalidPageCase.NegativeSpeciesIndex => instances[0] with { SpeciesIndex = -1 },
            InvalidPageCase.SpeciesIndexOutsideTable => instances[0] with
            {
                SpeciesIndex = descriptor.Species.Count
            },
            InvalidPageCase.NonFiniteLocalPosition => instances[0] with
            {
                LocalPosition = new Vector3(float.NaN, 0.0f, 0.0f)
            },
            InvalidPageCase.ZeroScale => instances[0] with { UniformScale = 0.0f },
            InvalidPageCase.ZeroRadius => instances[0] with { ConservativeRadius = 0.0f },
            InvalidPageCase.ZeroQuaternion => instances[0] with { Orientation = default },
            InvalidPageCase.NonUnitQuaternion => instances[0] with
            {
                Orientation = new Quaternion(2.0f, 0.0f, 0.0f, 0.0f)
            },
            _ => instances[0]
        };
        if (invalidCase == InvalidPageCase.DuplicateStableKey)
        {
            instances[1] = instances[1] with { StableKey = instances[0].StableKey };
        }

        return descriptor with { Instances = Array.AsReadOnly(instances) };
    }

    private static WorldBounds CalculateBounds(CookedVegetationInstancePage page)
    {
        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;
        double maxZ = double.NegativeInfinity;
        foreach (CookedVegetationInstance instance in page.Instances)
        {
            double x = page.Origin.X + instance.LocalPosition.X;
            double y = page.Origin.Y + instance.LocalPosition.Y;
            double z = page.Origin.Z + instance.LocalPosition.Z;
            double radius = instance.ConservativeRadius;
            minX = Math.Min(minX, x - radius);
            minY = Math.Min(minY, y - radius);
            minZ = Math.Min(minZ, z - radius);
            maxX = Math.Max(maxX, x + radius);
            maxY = Math.Max(maxY, y + radius);
            maxZ = Math.Max(maxZ, z + radius);
        }

        return new WorldBounds(
            new WorldPosition(minX, minY, minZ),
            new WorldPosition(maxX, maxY, maxZ));
    }

    private static Quaternion Negate(Quaternion value) =>
        new(-value.X, -value.Y, -value.Z, -value.W);

    private static void AssertRejected(byte[] bytes)
    {
        Assert.False(
            VegetationInstancePageAssetCooker.TryReadPayload(
                PageGuid,
                ClusterGuid,
                PackageId,
                bytes,
                "instance-page-corrupt",
                out _,
                out string diagnostic));
        Assert.False(string.IsNullOrWhiteSpace(diagnostic));
    }

    private static int FindSectionDescriptor(byte[] bytes, uint sectionType)
    {
        int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(104));
        for (int index = 0; index < sectionCount; index++)
        {
            int offset = VegetationInstancePageAssetCooker.HeaderSize +
                (index * VegetationCookedContainer.SectionDirectoryEntrySize);
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) == sectionType)
            {
                return offset;
            }
        }

        throw new InvalidOperationException($"Section '{sectionType}' was not found.");
    }

    private static void Rehash(byte[] bytes)
    {
        SHA256.HashData(bytes.AsSpan(VegetationInstancePageAssetCooker.HeaderSize)).CopyTo(
            bytes.AsSpan(
                VegetationInstancePageAssetCooker.HashOffset,
                VegetationCookedContainer.HashSize));
    }

    public enum InvalidPageCase
    {
        ZeroStableKey,
        DuplicateStableKey,
        NegativeSpeciesIndex,
        SpeciesIndexOutsideTable,
        NonFiniteOrigin,
        NonFiniteLocalPosition,
        ZeroScale,
        ZeroRadius,
        ZeroQuaternion,
        NonUnitQuaternion
    }

    private sealed class PageFixture : IDisposable
    {
        public PageFixture()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "ArisenVegetationInstancePageTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Database = new TestAssetDatabase(
                AssetSourceAccessMode.RuntimeAssetCook,
                Path.Combine(Root, "Cooked"));
            AddAsset(PageGuid, VegetationAssetTypes.InstancePage, "page.source");
            AddAsset(ClusterGuid, VegetationAssetTypes.Cluster, "cluster.source");
            AddAsset(SpeciesAGuid, VegetationAssetTypes.Species, "species-a.source");
            AddAsset(SpeciesBGuid, VegetationAssetTypes.Species, "species-b.source");
        }

        public string Root { get; }

        public TestAssetDatabase Database { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private void AddAsset(Guid guid, string assetType, string fileName)
        {
            string path = Path.Combine(Root, "Sources", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "fixture");
            Database.AddAsset(guid, assetType, path, PackageId);
        }
    }
}
